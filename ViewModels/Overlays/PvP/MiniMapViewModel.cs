using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.PvP;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public enum EnemyState
    {
        Unknown,
        Enemy,
        Friend
    }
    public class OpponentMapInfo
    {
        public EnemyState IsEnemy { get; set; }
        public bool IsCurrentInfo { get; set; }
        public bool IsLocalPlayer { get; set; }
        public string Name { get; set; }
        public MenaceTypes Menace { get; set; }
        public PositionData Position { get; set; }
        public bool IsTarget { get; set; }
    }
    public class MiniMapViewModel : BaseOverlayViewModel
    {
        private bool _isActive = false;
        private MiniMapView _miniMapView;

        private DispatcherTimer _dTimer;
        private bool _isTriggered;
        private DateTime _lastUpdate;
        private Combat _mostRecentCombat;
        private Dictionary<string, DateTime> _lastUpdatedPlayer = new Dictionary<string, DateTime>();
        private bool _showFrame;

        public override bool ShouldBeVisible => _showFrame;

        public MiniMapViewModel(string overlayName) : base(overlayName)
        {
            _dTimer = new DispatcherTimer();
            _miniMapView = new MiniMapView(this);
            MainContent = _miniMapView;

            EncounterTimerTrigger.PvPEncounterEntered += OnPvpCombatStarted;
            EncounterTimerTrigger.NonPvpEncounterEntered += OnPvpCombatEnded;
            CombatLogStreamer.NewLineStreamed += NewStreamedLine;
            CombatSelectionMonitor.CombatSelected += NewCombatInfo;
        }
        public event Action<string, bool> OverlayStateChanged = delegate { };
        public List<OpponentMapInfo> CharacterPositionInfos { get; set; } = new List<OpponentMapInfo>();
        private void OnPvpCombatStarted()
        {
            if (!OverlayEnabled || _isTriggered)
                return;
            _isTriggered = true;
            if (GetCurrentActive())
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    ShowFrame = true;
                    _mostRecentCombat = null;
                    CharacterPositionInfos.Clear();
                    _lastUpdatedPlayer.Clear();
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
            CharacterPositionInfos.Clear();
            _lastUpdatedPlayer.Clear();
            Dispatcher.UIThread.Invoke(() =>
            {
                ShowFrame = false;
                _dTimer.Stop();
                _dTimer.Tick -= CheckForNewState;
            });

        }
        public int Buffer { get; set; }
        public bool OverlayEnabled
        {
            get { return _isActive; }
            set
            {
                this.RaiseAndSetIfChanged(ref _isActive, value);
                Active = value;
                OverlayStateChanged("MiniMap", _isActive);
            }
        }

        public bool ShowFrame
        {
            get => _showFrame;
            set
            {
                this.RaiseAndSetIfChanged(ref _showFrame, value);
                UpdateVisibility();
            }
        }

        public void LockOverlays()
        {
            OverlaysMoveable = false;
            if (!GetCurrentActive() || !_isTriggered)
                ShowFrame = false;
        }
        public void UnlockOverlays()
        {
            OverlaysMoveable = true;
            if (GetCurrentActive())
                ShowFrame = true;
        }
        private void NewStreamedLine(ParsedLogEntry newLine)
        {
            if (!_isTriggered)
                return;
            _lastUpdate = newLine.TimeStamp;
            if (newLine.Source.Name != null && newLine.Source.IsCharacter)
            {
                AddOrUpdateEntity(newLine.SourceInfo);
            }
            if (newLine.Target.Name != null && newLine.Target.IsCharacter)
            {
                AddOrUpdateEntity(newLine.TargetInfo);
            }
        }
        private void AddOrUpdateEntity(EntityInfo info)
        {
            _lastUpdatedPlayer[info.Entity.Name] = _lastUpdate;
            if (CharacterPositionInfos.Any(p => p.Name == info.Entity.Name))
            {
                var update = CharacterPositionInfos.First(p => p.Name == info.Entity.Name);
                update.Position = info.Position;
                update.Menace = GetMenaceType(info.Entity.Name);
                update.IsTarget = IsCurrentTarget(info.Entity.Name);
                update.IsEnemy = _mostRecentCombat == null ? EnemyState.Unknown : (CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(info.Entity, _lastUpdate) ? EnemyState.Enemy : EnemyState.Friend);
            }
            else
            {
                if (CharacterPositionInfos.Count == 16)
                {
                    CharacterPositionInfos.Remove(CharacterPositionInfos.MinBy(p => _lastUpdatedPlayer[p.Name]));
                }
                CharacterPositionInfos.Add(new OpponentMapInfo
                {
                    Position = info.Position,
                    Name = info.Entity.Name,
                    Menace = GetMenaceType(info.Entity.Name),
                    IsTarget = IsCurrentTarget(info.Entity.Name),
                    IsLocalPlayer = info.Entity.IsLocalPlayer,
                    IsEnemy = _mostRecentCombat == null ? EnemyState.Unknown : (CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(info.Entity, _lastUpdate) ? EnemyState.Enemy : EnemyState.Friend)

                });
            }

        }
        private void NewCombatInfo(Combat currentCombat)
        {
            _mostRecentCombat = currentCombat;
        }

        private bool IsCurrentInfo(string opponentKey)
        {
            var lastInfoTime = _lastUpdatedPlayer[opponentKey];
            return (DateTime.Now - lastInfoTime).TotalSeconds < 5;
        }

        private void CheckForNewState(object sender, EventArgs e)
        {
            if (_lastUpdatedPlayer.Count == 0)
                return;
            var positionInfo = CharacterPositionInfos.ToList();
            positionInfo.ForEach(p => p.IsCurrentInfo = IsCurrentInfo(p.Name));
            _miniMapView.AddOpponents(positionInfo, _lastUpdatedPlayer.MaxBy(kvp => kvp.Value).Value);
        }

        private MenaceTypes GetMenaceType(string key)
        {
            if (_mostRecentCombat == null || !_mostRecentCombat.EDPS.Where(kvp => CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(kvp.Key, _mostRecentCombat.StartTime)).Any())
                return MenaceTypes.None;
            var maxDPS = _mostRecentCombat.EDPS.Where(kvp => CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(kvp.Key, _mostRecentCombat.StartTime)).MaxBy(d => d.Value);
            if (maxDPS.Key.Name == key)
                return MenaceTypes.Dps;
            //Doesn't seem like the logs have information about healing done by opponents. Can't know who is the healing menace.
            var maxEHPS = _mostRecentCombat.EHPS.Where(kvp => CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(kvp.Key, _mostRecentCombat.StartTime)).MaxBy(d => d.Value);
            if (maxEHPS.Key.Name == key)
                return MenaceTypes.None;
            return MenaceTypes.None;
        }

        private bool IsCurrentTarget(string key)
        {
            var target = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(CombatLogStateBuilder.CurrentState.LocalPlayer, _lastUpdate).Entity;
            if (target == null)
                return false;
            return target.Name == key;
        }

        private double GetLocalPlayerRange()
        {
            var localClass = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(_lastUpdate);
            if (localClass == null)
                return 0;
            var requiredRange = localClass.IsRanged ? 30 : 5;
            return requiredRange;
        }

        private bool GetCurrentActive()
        {
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_overlayName);
            return defaults.Acive;
        }
    }
}
