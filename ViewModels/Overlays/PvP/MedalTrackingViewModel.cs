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
using DynamicData;
using ReactiveUI;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.DataStructures.PvP;
using SWTORCombatParser.Model.CombatParsing;

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
    public class MedalTrackingViewModel : BaseOverlayViewModel
    {
        private bool _isActive = false;
        private MedalTrackingView _medalTrackingView;
        
        private EncounterType _encounterType;
        private SWTORClass _currentPlayerClass = new  SWTORClass();
        private bool _isTriggered;
        private Combat _mostRecentCombat;
        private bool _showFrame;
        
        private List<PvPMedal> _allPvPMedals = new List<PvPMedal>();
        private List<PvPMedalProgressViewModel> _availablePvPMedals = new List<PvPMedalProgressViewModel>();
        private EncounterInfo _currentEncounter;

        public List<PvPMedalProgressViewModel> AvailablePvPMedals
        {
            get => _availablePvPMedals;
            set => this.RaiseAndSetIfChanged(ref _availablePvPMedals, value);
        }


        public override bool ShouldBeVisible => _showFrame;
        private object medalFilterLock = new();
        public MedalTrackingViewModel(string overlayName) : base(overlayName)
        {
            _medalTrackingView = new MedalTrackingView(this);
            MainContent = _medalTrackingView;

            EncounterTimerTrigger.PvPEncounterEntered += OnPvPAreaEntered;
            EncounterTimerTrigger.NonPvpEncounterEntered += OnPvPAreaExited;

            CombatSelectionMonitor.CombatSelected += NewCombatSelected;
            CombatSelectionMonitor.OnInProgressCombatSelected += InProgressCombatUpdated;

            _allPvPMedals = PvPMedalLoader.PvPMedals;
        }

        public event Action<string, bool> OverlayStateChanged = delegate { };
        private void OnPvPAreaEntered(DateTime changedTime, EncounterInfo encounterInfo)
        {
            if (!OverlayEnabled)
                return;
            if (_isTriggered && _currentEncounter == encounterInfo)
                return;
            _isTriggered = true;
            if (GetCurrentActive())
            {
                _currentEncounter = encounterInfo;
                _encounterType = _currentEncounter.EncounterType;
                _currentPlayerClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(CombatLogStateBuilder.CurrentState.LocalPlayer, changedTime);
                FilterAvailablePvPMedals();
                Dispatcher.UIThread.Invoke(() =>
                {
                    ShowFrame = true;
                    _mostRecentCombat = null;
                });

            }
        }

        private void FilterAvailablePvPMedals()
        {
            lock (medalFilterLock)
            {
                var filteredMedals = new List<PvPMedalProgressViewModel>();
                var pvpMatchFilteredMedals = _allPvPMedals.Where(m => m.EncounterType == _encounterType || m.EncounterType != EncounterType.Arena);
                if (_currentPlayerClass.Role == Role.DPS)
                {
                    filteredMedals.AddRange(pvpMatchFilteredMedals.Where(m => m.MedalType == MedalType.DPS || m.MedalType == MedalType.General).Select(m=>new PvPMedalProgressViewModel(m)));
                }
                if (_currentPlayerClass.Role == Role.Healer)
                {
                    filteredMedals.AddRange(pvpMatchFilteredMedals.Where(m => m.MedalType == MedalType.DPS || m.MedalType == MedalType.General || m.MedalType == MedalType.Healer).Select(m=>new PvPMedalProgressViewModel(m)));
                }
                if (_currentPlayerClass.Role == Role.Tank)
                {
                    filteredMedals.AddRange(pvpMatchFilteredMedals.Where(m => m.MedalType == MedalType.DPS || m.MedalType == MedalType.General || m.MedalType == MedalType.Tank).Select(m=>new PvPMedalProgressViewModel(m)));
                }
                if(_encounterType != EncounterType.Arena)
                    filteredMedals.Add(new PvPMedalProgressViewModel());
                AvailablePvPMedals = filteredMedals;
                UpdatePvpMedalsVisual();
            }

        }
        private void OnPvPAreaExited(DateTime changedTime, EncounterInfo encounterInfo)
        {
            if (!OverlayEnabled)
                return;

            _isTriggered = false;
            _mostRecentCombat = null;
            Dispatcher.UIThread.Invoke(() =>
            {
                ShowFrame = false;
            });

        }
        public bool OverlayEnabled
        {
            get { return _isActive; }
            set
            {
                this.RaiseAndSetIfChanged(ref _isActive, value);
                Active = value;
                OverlayStateChanged("MedalTracking", _isActive);
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
        private async void NewCombatSelected(Combat currentCombat)
        {
            if (currentCombat.IsPvPCombat)
            {
                _mostRecentCombat = await EncounterMonitor.GetCurrentEncounter().GetOverallCombat();
                if (string.IsNullOrEmpty(_currentPlayerClass.Discipline))
                {
                    _currentPlayerClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(currentCombat.LocalPlayer, currentCombat.StartTime);
                    FilterAvailablePvPMedals();
                }
                OnPvPAreaEntered(currentCombat.StartTime, currentCombat.ParentEncounter);
            }
            else
                OnPvPAreaExited(currentCombat.StartTime,  currentCombat.ParentEncounter);
        }
        
        private void InProgressCombatUpdated(Combat currentCombat)
        {
            if (!_isTriggered)
                return;
            _mostRecentCombat = currentCombat;
            if (string.IsNullOrEmpty(_currentPlayerClass.Discipline))
            {
                _currentPlayerClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(currentCombat.LocalPlayer, currentCombat.StartTime);
                FilterAvailablePvPMedals();
            }
            UpdatePvpMedalsVisual();
        }
        private void UpdatePvpMedalsVisual()
        {
            foreach (var pvpMedalType in AvailablePvPMedals)
            {
                pvpMedalType.UpdateMedalProgress(_mostRecentCombat);
            }
        }
        private bool GetCurrentActive()
        {
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_overlayName);
            return defaults.Acive;
        }
    }
}
