using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.PvP;
using SWTORCombatParser.Views.Overlay.Room;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public class OpponentMapInfo
    {
        public string Name { get; set; }
        public MenaceTypes Menace { get; set; }
        public PositionData Position { get; set; }
        public bool IsTarget { get; set; }
    }
    public class MiniMapViewModel:INotifyPropertyChanged
    {
        private bool _isActive = false;
        private MiniMapView _miniMapView;
        private OverlayInfo _settings;

        private DispatcherTimer _dTimer;
        private bool _isTriggered;
        private DateTime _lastUpdate;
        private Combat _mostRecentCombat;
        private Dictionary<string, DateTime> _lastUpdatedPlayer = new Dictionary<string, DateTime>();
        public MiniMapViewModel()
        {
            _dTimer = new DispatcherTimer();
            _miniMapView = new MiniMapView(this);
            _miniMapView.Show();
            _settings = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_MiniMap");

            EncounterTimerTrigger.PvPEncounterEntered += OnPvpCombatStarted;
            EncounterTimerTrigger.NonPvpEncounterEntered += OnPvpCombatEnded;

            CombatLogStreamer.NewLineStreamed += NewStreamedLine;
            CombatIdentifier.NewCombatAvailable += NewCombatInfo;
            SetInitialPosition();
        }
        public List<OpponentMapInfo> OpponentPositionInfo { get; set; } = new List<OpponentMapInfo>();
        private void OnPvpCombatStarted()
        {
            if (!OverlayEnabled || _isTriggered)
                return;
            _isTriggered = true;
            if (_settings.Acive)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    ShowFrame = true;
                    _mostRecentCombat = null;
                    OpponentPositionInfo.Clear();
                    OnPropertyChanged("ShowFrame");
                    _dTimer.Start();
                    _dTimer.Interval = TimeSpan.FromSeconds(0.1);
                    _dTimer.Tick += CheckForNewState;
                });

            }
        }
        private void OnPvpCombatEnded()
        {
            if (!OverlayEnabled)
                return;

            _isTriggered = false;
            _mostRecentCombat = null;
            OpponentPositionInfo.Clear();
            App.Current.Dispatcher.Invoke(() =>
            {
                ShowFrame = false;
                _dTimer.Stop();
                _dTimer.Tick -= CheckForNewState;
                OnPropertyChanged("ShowFrame");
            });

        }
        public int Buffer { get; set; }
        public bool OverlayEnabled
        {
            get { return _isActive; }
            set
            {
                DefaultGlobalOverlays.SetActive("PvP_MiniMap", value);
                _isActive = value;
                if (!_isActive)
                {
                    _miniMapView.Hide();
                }
                else
                {
                    _miniMapView.Show();
                    if (OverlaysMoveable)
                    {
                        ShowFrame = true;
                        OnPropertyChanged("ShowFrame");
                    }
                    
                }
                OnPropertyChanged();
            }
        }
        public string CharImagePath => "../../../resources/RoomOverlays/PlayerLocation.png";
        public event Action<bool> OnLocking = delegate { };
        public bool OverlaysMoveable { get; set; }
        public bool ShowFrame { get; set; }
        public void LockOverlays()
        {
            OnLocking(true);
            OverlaysMoveable = false;
            ShowFrame = false;
            OnPropertyChanged("ShowFrame");
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            if (_settings.Acive)
                ShowFrame = true;
            OnPropertyChanged("ShowFrame");
            OnPropertyChanged("OverlaysMoveable");
        }
        private void NewStreamedLine(ParsedLogEntry newLine)
        {
            if (_mostRecentCombat == null)
                return;
            _lastUpdate = newLine.TimeStamp;
            if (newLine.Source == newLine.Target && CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(newLine.Source, newLine.TimeStamp) && newLine.Source.IsCharacter)
            {
                AddOrUpdateEntity(newLine.SourceInfo);
                return;
            }
            if(CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(newLine.Source, newLine.TimeStamp) && newLine.Source.Name != null && newLine.Source.IsCharacter)
            {
                AddOrUpdateEntity(newLine.SourceInfo);
                return;
            }
            if (CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(newLine.Target, newLine.TimeStamp) && newLine.Target.Name != null && newLine.Target.IsCharacter)
            {
                AddOrUpdateEntity(newLine.TargetInfo);
                return;
            }
        }
        private void AddOrUpdateEntity(EntityInfo info)
        {
            _lastUpdatedPlayer[info.Entity.Name] = _lastUpdate;
            if (OpponentPositionInfo.Any(p=>p.Name == info.Entity.Name))
            {
                var update = OpponentPositionInfo.First(p => p.Name == info.Entity.Name);
                update.Position = info.Position;
                update.Menace = GetMenaceType(info.Entity.Name);
                update.IsTarget = IsCurrentTarget(info.Entity.Name);
            }
            else
            {
                if(OpponentPositionInfo.Count == 8)
                {
                    OpponentPositionInfo.Remove(OpponentPositionInfo.MinBy(p => _lastUpdatedPlayer[p.Name]));
                }
                OpponentPositionInfo.Add(new OpponentMapInfo
                {
                    Position = info.Position,
                    Name = info.Entity.Name,
                    Menace = GetMenaceType(info.Entity.Name),
                    IsTarget = IsCurrentTarget(info.Entity.Name)
                });
            }
            
        }
        private void NewCombatInfo(Combat currentCombat)
        {
            _mostRecentCombat = currentCombat;
            //var participants = currentCombat.CharacterParticipants;
            //var opponents = participants.Where(p => CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(p,currentCombat.StartTime));

            //foreach (var opponent in opponents)
            //{
            //    OpponentPositionInfo.Add(new OpponentMapInfo { 
            //        Position = CombatLogStateBuilder.CurrentState.CurrentCharacterPositions[opponent], 
            //        Name = opponent.Name, 
            //        Menace = GetMenaceType(opponent.Name),
            //        IsTarget = IsCurrentTarget(opponent.Name)
            //    });
            //}
        }



        private void CheckForNewState(object sender, EventArgs e)
        {
            var localInfo = CombatLogStateBuilder.CurrentState.CurrentLocalCharacterPosition;
            _miniMapView.UpdateCharacter(localInfo.Facing);
            _miniMapView.AddOpponents(OpponentPositionInfo.ToList(), localInfo, GetLocalPlayerRange(), Buffer);
        }

        private MenaceTypes GetMenaceType(string key)
        {
            if(_mostRecentCombat == null || !_mostRecentCombat.EDPS.Where(kvp => CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(kvp.Key, _mostRecentCombat.StartTime)).Any())
                return MenaceTypes.None;
            var maxDPS = _mostRecentCombat.EDPS.Where(kvp=>CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(kvp.Key,_mostRecentCombat.StartTime)).MaxBy(d => d.Value);
            if (maxDPS.Key.Name == key)
                return MenaceTypes.Dps;
            var maxEHPS = _mostRecentCombat.EHPS.Where(kvp => CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(kvp.Key, _mostRecentCombat.StartTime)).MaxBy(d => d.Value);
            if (maxEHPS.Key.Name == key)
                return MenaceTypes.Healer;
            return MenaceTypes.None;
        }

        private bool IsCurrentTarget(string key)
        {
            var target = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(CombatLogStateBuilder.CurrentState.LocalPlayer, _lastUpdate);
            if (target == null)
                return false;
            return target.Name == key;
        }

        private double GetLocalPlayerRange()
        {
            var localClass = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(_lastUpdate);
            var requiredRange = localClass.IsRanged ? 30 : 5;
            return requiredRange;
        }

        private void SetInitialPosition()
        {
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType("PvP_MiniMap");
            OverlayEnabled = defaults.Acive;
            _miniMapView.Top = defaults.Position.Y;
            _miniMapView.Left = defaults.Position.X;
            _miniMapView.Width = defaults.WidtHHeight.X;
            _miniMapView.Height = defaults.WidtHHeight.Y;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
