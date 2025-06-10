using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.PvP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.PvP
{
    public class OpponentOverlayViewModel : BaseOverlayViewModel
    {
        private bool _isActive = false;
        private readonly OpponentHpOverlay _opponentHPView;

        private DispatcherTimer _dTimer;
        private bool _isTriggered;
        private DateTime _lastUpdate;
        private Combat _mostRecentCombat;
        private Dictionary<string, double> _currentHps = new Dictionary<string, double>();
        private Dictionary<string, DateTime> _lastUpdatedPlayer = new Dictionary<string, DateTime>();
        private object _combatUpdateLock = new object();
        private bool _showFrame;
        private ObservableCollection<OpponentHPBarViewModel> _opponentHpBars = new ObservableCollection<OpponentHPBarViewModel>();
        public override bool ShouldBeVisible => _showFrame;

        public OpponentOverlayViewModel(string overlayName) : base(overlayName)
        {
            _dTimer = new DispatcherTimer();
            _opponentHPView = new OpponentHpOverlay(this);
            MainContent = _opponentHPView;
            EncounterTimerTrigger.NonPvpEncounterEntered += OnPvpCombatEnded;
            EncounterTimerTrigger.PvPEncounterEntered += OnPvpCombatStarted;
            CombatLogStreamer.NewLineStreamed += NewLineStreamed;
            CombatSelectionMonitor.CombatSelected += NewCombatInfo;
        }


        public event Action<string, bool> OverlayStateChanged = delegate { };

        public ObservableCollection<OpponentHPBarViewModel> OpponentHpBars
        {
            get => _opponentHpBars;
            set => this.RaiseAndSetIfChanged(ref _opponentHpBars, value);
        }

        private void OnPvpCombatStarted()
        {
            
            if (!OverlayEnabled || _isTriggered)
                return;
            _isTriggered = true;
            if (GetCurrentActive())
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    ResetUI();
                    ShowFrame = true;
                    _mostRecentCombat = null;
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
            lock (_combatUpdateLock)
            {
                _isTriggered = false;
                _mostRecentCombat = null;
            }


            Dispatcher.UIThread.Invoke(() =>
            {           
                ResetUI();
                ShowFrame = false;
                _dTimer.Stop();
                _dTimer.Tick -= CheckForNewState;
            });

        }

        private void ResetUI()
        {
            OpponentHpBars.Clear();
            _currentHps.Clear();
            _lastUpdatedPlayer.Clear();
        }
        public bool OverlayEnabled
        {
            get => _isActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _isActive, value);
                Active = _isActive;
                OverlayStateChanged("OpponentHP", _isActive);
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
        private void NewCombatInfo(Combat currentCombat)
        {
            lock (_combatUpdateLock)
                _mostRecentCombat = currentCombat;
        }
        private void NewLineStreamed(ParsedLogEntry newLine)
        {
            lock (_combatUpdateLock)
            {
                if (newLine.Effect.EffectType == EffectType.AreaEntered && !EncounterTimerTrigger.CurrentEncounterIsPVP)
                {
                    OnPvpCombatEnded();
                    return;
                }
                if (_mostRecentCombat == null)
                    return;
                _lastUpdate = newLine.TimeStamp;


                if (newLine.Source == newLine.Target && CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(newLine.Source, newLine.TimeStamp) && newLine.Source.IsCharacter)
                {
                    _lastUpdatedPlayer[newLine.Source.Name] = _lastUpdate;
                    _currentHps[newLine.Source.Name] = newLine.SourceInfo.CurrentHP / (double)newLine.SourceInfo.MaxHP;
                    RemoveOldPlayers();
                    return;
                }
                if (CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(newLine.Source, newLine.TimeStamp) && newLine.Source.Name != null && newLine.Source.IsCharacter)
                {
                    _lastUpdatedPlayer[newLine.Source.Name] = _lastUpdate;
                    _currentHps[newLine.Source.Name] = newLine.SourceInfo.CurrentHP / (double)newLine.SourceInfo.MaxHP;
                    RemoveOldPlayers();
                    return;
                }
                if (CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(newLine.Target, newLine.TimeStamp) && newLine.Target.Name != null && newLine.Target.IsCharacter)
                {
                    _lastUpdatedPlayer[newLine.Target.Name] = _lastUpdate;
                    _currentHps[newLine.Target.Name] = newLine.TargetInfo.CurrentHP / (double)newLine.TargetInfo.MaxHP;
                    RemoveOldPlayers();
                    return;
                }
            }
        }
        private void RemoveOldPlayers()
        {
            if (_currentHps.Count > 8)
            {
                _currentHps.Remove(_currentHps.MinBy(p => _lastUpdatedPlayer[p.Key]).Key);
            }
        }

        private void CheckForNewState(object sender, EventArgs e)
        {
            var sorted = (from entry in _currentHps orderby entry.Key ascending select entry).ToList();
            foreach (var opponent in sorted)
            {
                var opponentToUpdate = OpponentHpBars.FirstOrDefault(x => x.PlayerName == opponent.Key);
                if (opponentToUpdate == null)
                {
                    var newOpponent = new OpponentHPBarViewModel(opponent.Key)
                    {
                        Value = opponent.Value,
                        InRange = IsInRangeOfLocalPlayer(opponent.Key),
                        IsCurrentInfo = IsCurrentInfo(opponent.Key),
                        IsTargeted = IsCurrentTarget(opponent.Key),
                        Menace = GetMenaceType(opponent.Key)
                    };
                    OpponentHpBars.Add(newOpponent);
                }
                else
                {
                    opponentToUpdate.Value = opponent.Value;
                    opponentToUpdate.InRange = IsInRangeOfLocalPlayer(opponent.Key);
                    opponentToUpdate.IsCurrentInfo = IsCurrentInfo(opponent.Key);
                    opponentToUpdate.IsTargeted = IsCurrentTarget(opponent.Key);
                    opponentToUpdate.Menace = GetMenaceType(opponent.Key);
                }
            }
        }

        private bool IsCurrentInfo(string opponentKey)
        {
            if (!_lastUpdatedPlayer.ContainsKey(opponentKey))
                return false;
            var lastInfoTime = _lastUpdatedPlayer[opponentKey];
            return (DateTime.Now - lastInfoTime).TotalSeconds < 5;
        }

        private MenaceTypes GetMenaceType(string key)
        {
            lock (_combatUpdateLock)
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
        }

        private bool IsCurrentTarget(string key)
        {
            var target = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(CombatLogStateBuilder.CurrentState.LocalPlayer, _lastUpdate).Entity;
            if (target == null)
                return false;
            return target.Name == key;
        }

        private bool IsInRangeOfLocalPlayer(string key)
        {
            var characterPosition = CombatLogStateBuilder.CurrentState.CurrentLocalCharacterPosition;
            if (!CombatLogStateBuilder.CurrentState.CurrentCharacterPositions.Any(c => c.Key.Name == key))
            {
                return false;
            }
            var targetPositionInfo = CombatLogStateBuilder.CurrentState.CurrentCharacterPositions.First(c => c.Key.Name == key).Value;
            var localClass = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(_lastUpdate);
            var requiredRange = localClass.IsRanged ? 30 : 5;
            var distanceBeween = Math.Sqrt(Math.Pow(targetPositionInfo.X - characterPosition.X, 2) + Math.Pow(targetPositionInfo.Y - characterPosition.Y, 2));
            return distanceBeween <= requiredRange;
        }

        private bool GetCurrentActive()
        {
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_overlayName);
            return defaults.Acive;
        }
    }
}
