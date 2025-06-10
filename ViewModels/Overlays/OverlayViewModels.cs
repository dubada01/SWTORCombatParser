using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Overlays.AbilityList;
using SWTORCombatParser.ViewModels.Overlays.Notes;
using SWTORCombatParser.ViewModels.Overlays.Personal;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using MoreLinq;
using ReactiveUI;
using SWTORCombatParser.ViewModels.Avalonia_TEMP;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayViewModel :ReactiveObject
    {

        private ConcurrentDictionary<OverlayType,OverlayInstanceViewModel> _currentOverlays = new();
        private Dictionary<string, OverlayInfo> _overlayDefaults = new();
        private string _currentCharacterRole = Role.DPS.ToString();
        private string _currentCharacterDiscipline = "";
        private bool overlaysLocked = true;
        private LeaderboardType selectedLeaderboardType;
        public TimersCreationViewModel _timersViewModel;
        public ChallengeSetupViewModel _challengesViewModel;
        private OthersOverlaySetupViewModel _otherOverlayViewModel;
        private AbilityListSetupViewModel _abilityListSetup;
        private double maxScalar = 1.5d;
        private double minScalar = 0.1d;
        private double sizeScalar = 1d;
        private string sizeScalarString = "1";
        private bool historicalParseFinished = false;
        private bool usePersonalOverlay;
        private PersonalOverlayViewModel _personalOverlayViewModel;
        private string selectedType = "Damage";
        private bool useDynamicLayout;

        public event Action OverlayLockStateChanged = delegate { };

        public ObservableCollection<OverlayOptionViewModel> MainDamageOverlays { get; set; } = new();
        public ObservableCollection<OverlayOptionViewModel> MainHealOverlays { get; set; } = new();
        public ObservableCollection<OverlayOptionViewModel> MainMitigationOverlays { get; set; } = new();
        
        public ObservableCollection<OverlayOptionViewModel> AdvancedDamageOverlays { get; set; } = new();
        public ObservableCollection<OverlayOptionViewModel> AdvancedHealOverlays { get; set; } = new();
        public ObservableCollection<OverlayOptionViewModel> AdvancedMitigationOverlays { get; set; } = new();
        
        public ObservableCollection<OverlayOptionViewModel> AvailableGeneralOverlays { get; set; } = new();
        public ObservableCollection<UtilityOverlayOptionViewModel> AvailableUtilityOverlays { get; set; } = new();
        public List<LeaderboardType> LeaderboardTypes { get; set; } = new();
        public LeaderboardType SelectedLeaderboardType
        {
            get => selectedLeaderboardType;
            set
            {
                selectedLeaderboardType = value;
                Leaderboards.UpdateLeaderboardType(selectedLeaderboardType);
            }
        }
        public string SizeScalarString
        {
            get => sizeScalarString; set
            {
                this.RaiseAndSetIfChanged(ref sizeScalarString, value);
                var stringVal = 0d;
                if (double.TryParse(sizeScalarString, out stringVal))
                {
                    SizeScalar = stringVal;
                }
            }
        }
        public double SizeScalar
        {
            get { return sizeScalar; }
            set
            {
                this.RaiseAndSetIfChanged(ref sizeScalar, value);
                if (sizeScalar > maxScalar)
                {
                    SizeScalarString = maxScalar.ToString();
                    return;
                }
                if (sizeScalar < minScalar)
                {
                    SizeScalarString = minScalar.ToString();
                    return;
                }
                _currentOverlays.ForEach(overlay => overlay.Value.SizeScalar = sizeScalar);

                SetOverlaysScale();

                Settings.WriteSetting<double>("overlay_bar_scale", sizeScalar);
            }
        }
        private void SetOverlaysScale()
        {
            _abilityListSetup.SetScalar(sizeScalar);
            _otherOverlayViewModel.SetScalar(sizeScalar);
            _timersViewModel.SetScalar(sizeScalar);
            _challengesViewModel.SetScalar(sizeScalar);
            _personalOverlayViewModel.UpdateScale(sizeScalar);
        }
        public void CombatSeleted(Combat selectedCombat)
        {
            _challengesViewModel.CombatSelected(selectedCombat);

        }
        public void CombatUpdated(Combat combat)
        {
            _challengesViewModel.CombatUpdated(combat);
        }

        public OverlayViewModel()
        {
            CombatLogStateBuilder.PlayerDiciplineChanged += UpdateOverlaysForClass;
            CombatLogStreamer.HistoricalLogsFinished += FinishHistoricalParse;
            CombatLogStreamer.HistoricalLogsStarted += HistoricalLogsStarted;
            sizeScalar = Settings.ReadSettingOfType<double>("overlay_bar_scale");
            useDynamicLayout = Settings.ReadSettingOfType<bool>("DynamicLayout");
            sizeScalarString = sizeScalar.ToString();
            LeaderboardTypes = EnumUtil.GetValues<LeaderboardType>().ToList();
            SelectedLeaderboardType = LeaderboardSettings.ReadLeaderboardSettings();
            DefaultCharacterOverlays.Init();
            DefaultGlobalOverlays.Init();
            DefaultPersonalOverlaysManager.Init();
            DefaultChallengeManager.Init();
            AvaloniaTimelineBuilder.Init();
            var enumVals = EnumUtil.GetValues<OverlayType>().OrderBy(d => d.ToString());
            foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
            {
                if (enumVal == OverlayType.Damage || enumVal == OverlayType.BurstDPS || enumVal == OverlayType.NonEDPS || enumVal == OverlayType.RawDamage || enumVal == OverlayType.SingleTargetDPS || enumVal == OverlayType.InstantaneousDPS)
                    AdvancedDamageOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.RawHealing || enumVal == OverlayType.EffectiveHealing || enumVal == OverlayType.BurstEHPS || enumVal == OverlayType.HealReactionTime || enumVal == OverlayType.SingleTargetEHPS
                    || enumVal == OverlayType.HealReactionTimeRatio || enumVal == OverlayType.TankHealReactionTime || enumVal == OverlayType.InstantaneousEHPS)
                    AdvancedHealOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.ShieldAbsorb || enumVal == OverlayType.ProvidedAbsorb ||  enumVal == OverlayType.DamageAvoided || enumVal == OverlayType.DamageSavedDuringCD)
                    AdvancedMitigationOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.DPS  || enumVal == OverlayType.FocusDPS)
                    MainDamageOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.HPS || enumVal == OverlayType.EHPS)
                    MainHealOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.Mitigation || enumVal == OverlayType.DamageTaken)
                    MainMitigationOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.APM || enumVal == OverlayType.InterruptCount || enumVal == OverlayType.ThreatPerSecond || enumVal == OverlayType.Threat)
                    AvailableGeneralOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
            }
            AvailableUtilityOverlays = new ObservableCollection<UtilityOverlayOptionViewModel>
            {
                new() { Name = "Personal Stats", Type = UtilityOverlayType.Personal},
                new() { Name = "Threat Table", Type = UtilityOverlayType.ThreatTable},
                new() { Name = "Raid HOTS", Type = UtilityOverlayType.RaidHot},
                new() { Name = "Boss HP", Type = UtilityOverlayType.RaidBoss},
                new() { Name = "Challenges", Type = UtilityOverlayType.RaidChallenge},
                new() { Name = "Encounter Timers", Type = UtilityOverlayType.RaidTimer},
                new() { Name = "Discipline Timers", Type = UtilityOverlayType.DisciplineTimer},
                new() { Name = "Alert Timers", Type = UtilityOverlayType.AlertTimer},
                new() { Name = "Room Hazards", Type = UtilityOverlayType.RoomHazard},
                new() { Name = "Timeline", Type = UtilityOverlayType.Timeline},
                new() { Name = "PvP Opponent HP", Type = UtilityOverlayType.PvPHP},
                new() { Name = "PvP Mini-map", Type = UtilityOverlayType.PvPMap},
                new() { Name = "Ability List", Type = UtilityOverlayType.AbilityList},
                new() {Name= "Raid Notes", Type=UtilityOverlayType.RaidNotes},
            };
            _timersViewModel = new TimersCreationViewModel();
            _challengesViewModel = new ChallengeSetupViewModel();
            _abilityListSetup = new AbilityListSetupViewModel();
            _raidNotesSetup = new RaidNotesSetupViewModel();
            _otherOverlayViewModel = new OthersOverlaySetupViewModel();

            _challengesViewModel.ChallengesDisabled += () => {
                AvailableUtilityOverlays.First(t => t.Type == UtilityOverlayType.RaidChallenge).IsSelected = false;
            };
            _abilityListSetup.OnEnabledChanged += b => {
                AvailableUtilityOverlays.First(t => t.Type == UtilityOverlayType.AbilityList).IsSelected = b;
            };
            _raidNotesSetup.OnEnabledChanged += b => {
                AvailableUtilityOverlays.First(t => t.Type == UtilityOverlayType.RaidNotes).IsSelected = b;
            };
            _otherOverlayViewModel._raidHotsConfigViewModel.EnabledChanged += e => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidHot).IsSelected = e;
            };
            _otherOverlayViewModel._bossFrameViewModel.CloseRequested += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidBoss).IsSelected = false;
            };
            _otherOverlayViewModel._roomOverlayViewModel.CloseRequested += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RoomHazard).IsSelected = false;
            };
            _otherOverlayViewModel._threatTableOverlayViewModel.CloseRequested += () =>
            {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.ThreatTable).IsSelected = false;
            };
            _otherOverlayViewModel._PvpOverlaysConfigViewModel.MapClosed += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.PvPMap).IsSelected = false;
            };
            _otherOverlayViewModel._PvpOverlaysConfigViewModel.OpponentClosed += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.PvPHP).IsSelected = false;
            };
            _timersViewModel.DisciplineClosed += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.DisciplineTimer).IsSelected = false;
            };
            _timersViewModel.EncounterClosed += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidTimer).IsSelected = false;
            };
            _timersViewModel.AlertsClosed += () => {
                AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.AlertTimer).IsSelected = false;
            };
           
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidHot).IsSelected = _otherOverlayViewModel._raidHotsConfigViewModel.RaidHotsEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidChallenge).IsSelected = _challengesViewModel.ChallengesEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidBoss).IsSelected = _otherOverlayViewModel._bossFrameViewModel.BossFrameEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.ThreatTable).IsSelected = _otherOverlayViewModel._threatTableOverlayViewModel.Active;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidTimer).IsSelected = _timersViewModel.EncounterTimersActive;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.AlertTimer).IsSelected = _timersViewModel.AlertsActive;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.DisciplineTimer).IsSelected = _timersViewModel.DisciplineTimersActive;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RoomHazard).IsSelected = _otherOverlayViewModel._roomOverlayViewModel.Active;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.Timeline).IsSelected = AvaloniaTimelineBuilder.TimelineEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.PvPHP).IsSelected = _otherOverlayViewModel._PvpOverlaysConfigViewModel.OpponentHPEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.PvPMap).IsSelected = _otherOverlayViewModel._PvpOverlaysConfigViewModel.MiniMapEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.AbilityList).IsSelected = _abilityListSetup.AbilityListEnabled;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.RaidNotes).IsSelected = _raidNotesSetup.RaidNotesEnabled;
            _personalOverlayViewModel = new PersonalOverlayViewModel("Personal");
            usePersonalOverlay = _personalOverlayViewModel.Active;
            AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.Personal).IsSelected = usePersonalOverlay;
            _personalOverlayViewModel.ActiveChanged += UpdatePersonalOverlayActive;

            SetOverlaysScale();
            RefreshOverlays();
            HotkeyHandler.OnLockOverlayHotkey += () => OverlaysLocked = !OverlaysLocked;
        }

        private void UpdateOverlaysForClass(Entity character, SWTORClass arg2)
        {
            var nextDiscipline = arg2.Discipline;
            if(nextDiscipline != _currentCharacterDiscipline)
            {
                _currentCharacterDiscipline = nextDiscipline;
                if (CombatMonitorViewModel.IsLiveParseActive())
                {
                    var timerToggle = AvailableUtilityOverlays.First(v => v.Type == UtilityOverlayType.DisciplineTimer);
                    timerToggle.Name = $"{arg2.Discipline}: Timers";
                }
            }
            var nextRole = arg2.Role.ToString();
            if (_currentCharacterRole == nextRole)
                return;
            _currentCharacterRole = arg2.Role.ToString();
            if (historicalParseFinished)
            {
                RefreshOverlays();
            }
        }

        private void RefreshOverlays()
        {
            Task.Run(() =>
            {
                ResetOverlays();
                _timersViewModel.TryShow();
                if (UseDynamicLayout)
                {
                    SetOverlaysToRole();
                }
                else
                {
                    SetOverlaysToCustom();
                }
            });
        }

        private void SetOverlaysToCustom()
        {
            SetPersonalByClass(Role.DPS);
            TryUpdateDefaultsToCurrentRole();
        }
        private void SetOverlaysToRole()
        {
            SetPersonalByClass(Enum.Parse<Role>(_currentCharacterRole));
            TryUpdateDefaultsToCurrentRole();
        }

        private void TryUpdateDefaultsToCurrentRole()
        {
            if (!DefaultCharacterOverlays.DoesKeyExist(_currentCharacterRole))
            {
                InitializeRoleBasedOverlays(_currentCharacterRole);
            }
            _overlayDefaults = DefaultCharacterOverlays.GetCharacterDefaults(_currentCharacterRole);

            UpdateOverlays();
        }

        private void InitializeRoleBasedOverlays(string currentRole)
        {
            var mostUsedOverlayLayout = DefaultCharacterOverlays.GetMostUsedLayout();
            if (!string.IsNullOrEmpty(mostUsedOverlayLayout))
            {
                DefaultCharacterOverlays.CopyFromKey(mostUsedOverlayLayout, currentRole);
            }
            else
            {
                DefaultCharacterOverlays.InitializeCharacterDefaults(currentRole);
            }
        }
        private void SetPersonalByClass(Role role)
        {
            switch (role)
            {
                case Role.DPS:
                    SelectedType = "Damage";
                    break;
                case Role.Healer:
                    SelectedType = "Heals";
                    break;
                case Role.Tank:
                    SelectedType = "Tank";
                    break;
                default:
                case Role.Unknown:
                    SelectedType = "Damage";
                    break;
            }
        }
        private Role GetRoleFromSelectedType(string type)
        {
            switch (type)
            {
                case "Damage":
                    return Role.DPS;
                case "Heals":
                    return Role.Healer;
                case "Tank":
                    return Role.Tank;
                default:
                    return Role.Unknown;
            }
        }

        private void UpdateOverlays()
        {

                _overlayDefaults = DefaultCharacterOverlays.GetCharacterDefaults(_currentCharacterRole);
                if (_overlayDefaults.Count == 0)
                    return;
                if (_overlayDefaults.First().Value.Locked)
                {
                    OverlaysLocked = true;
                }
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
                {
                    if (!_overlayDefaults.ContainsKey(enumVal.ToString()))
                        continue;
                    if (_overlayDefaults[enumVal.ToString()].Acive)
                        CreateOverlay(GetType(enumVal), false);
                }
                _currentOverlays.ForEach(o => o.Value.RoleChanged(_currentCharacterRole));
           
        }
        private void FinishHistoricalParse(DateTime combatEndTime, bool localPlayerIdentified)
        {
            historicalParseFinished = true;
            if (!localPlayerIdentified)
                return;
            var localPlayer = CombatLogStateBuilder.CurrentState.LocalPlayer;
            var currentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(combatEndTime);
            if (localPlayer == null)
                return;
            UpdateOverlaysForClass(localPlayer, currentDiscipline);
        }
        private void HistoricalLogsStarted()
        {
            historicalParseFinished = false;
        }
        public ReactiveCommand<UtilityOverlayOptionViewModel,Unit> ToggleUtilityCommand => ReactiveCommand.Create<UtilityOverlayOptionViewModel>(ToggleUtility);
        private void ToggleUtility(UtilityOverlayOptionViewModel utility)
        {
            utility.IsSelected = !utility.IsSelected;
            switch (utility.Type)
            {
                case UtilityOverlayType.Personal:
                    UsePersonalOverlay = !UsePersonalOverlay; 
                    break;
                case UtilityOverlayType.RaidHot:
                    _otherOverlayViewModel._raidHotsConfigViewModel.RaidHotsEnabled = !_otherOverlayViewModel._raidHotsConfigViewModel.RaidHotsEnabled;
                    break;
                case UtilityOverlayType.RaidBoss:
                    _otherOverlayViewModel._bossFrameViewModel.BossFrameEnabled = !_otherOverlayViewModel._bossFrameViewModel.BossFrameEnabled;
                    break;
                case UtilityOverlayType.RaidTimer:
                    _timersViewModel.EncounterTimersActive = !_timersViewModel.EncounterTimersActive;
                    break;
                case UtilityOverlayType.DisciplineTimer:
                    _timersViewModel.DisciplineTimersActive = !_timersViewModel.DisciplineTimersActive;
                    break;
                case UtilityOverlayType.AlertTimer:
                    _timersViewModel.AlertsActive = !_timersViewModel.AlertsActive;
                    break;
                case UtilityOverlayType.RoomHazard:
                    _otherOverlayViewModel._roomOverlayViewModel.Active = !_otherOverlayViewModel._roomOverlayViewModel.Active;
                    break;
                case UtilityOverlayType.PvPHP:
                    _otherOverlayViewModel._PvpOverlaysConfigViewModel.OpponentHPEnabled = !_otherOverlayViewModel._PvpOverlaysConfigViewModel.OpponentHPEnabled;
                    break;
                case UtilityOverlayType.PvPMap:
                    _otherOverlayViewModel._PvpOverlaysConfigViewModel.MiniMapEnabled = !_otherOverlayViewModel._PvpOverlaysConfigViewModel.MiniMapEnabled;
                    break;
                case UtilityOverlayType.RaidChallenge:
                    _challengesViewModel.ChallengesEnabled = !_challengesViewModel.ChallengesEnabled;
                    break;
                case UtilityOverlayType.AbilityList:
                    _abilityListSetup.AbilityListEnabled = !_abilityListSetup.AbilityListEnabled;
                    break;
                case UtilityOverlayType.RaidNotes:
                    _raidNotesSetup.RaidNotesEnabled = !_raidNotesSetup.RaidNotesEnabled;
                    break;
                case UtilityOverlayType.Timeline:
                    AvaloniaTimelineBuilder.TimelineEnabled = !AvaloniaTimelineBuilder.TimelineEnabled;
                    break;
                case UtilityOverlayType.ThreatTable:
                    _otherOverlayViewModel._threatTableOverlayViewModel.Active = !_otherOverlayViewModel._threatTableOverlayViewModel.Active;
                    break;
                default:
                    return;

            }
        }
        public ReactiveCommand<OverlayOptionViewModel,Unit> GenerateOverlay => ReactiveCommand.Create<OverlayOptionViewModel>(v => CreateOverlay(v, true));
        private readonly object _overlayCreationLock = new object();
        private void CreateOverlay(OverlayOptionViewModel type, bool canDelete)
        {
            lock (_overlayCreationLock)
            {
                OverlayOptionViewModel overlayType = type;
                if (_currentOverlays.TryGetValue(overlayType.Type, out var currentOverlay))
                {
                    if (!canDelete) return;
                    currentOverlay.RequestClose();
                    RemoveOverlay(currentOverlay);
                    return;
                }
                overlayType.IsSelected = true;
                var viewModel = new OverlayInstanceViewModel(overlayType.Type);
                viewModel.SetRole(_currentCharacterRole);
                viewModel.OverlayClosed += OverlayHidden;
                viewModel.SizeScalar = SizeScalar;
                viewModel.Refresh(CombatIdentifier.CurrentCombat);
                viewModel.OverlaysMoveable = !OverlaysLocked;
                viewModel.Active = true;
                viewModel.ShowOverlayWindow();
                _currentOverlays[overlayType.Type] = viewModel;
            }
        }

        private void OverlayHidden(OverlayInstanceViewModel overlay)
        {
            _currentOverlays.Remove(overlay.CreatedType, out _);
            SetSelected(false, overlay.CreatedType);
        }
        private void RemoveOverlay(OverlayInstanceViewModel obj)
        {
            DefaultCharacterOverlays.SetActiveStateCharacter(obj.CreatedType.ToString(), false, _currentCharacterRole);
            OverlayHidden(obj);
        }
        public void HideOverlays()
        {
            ResetOverlays();
            _otherOverlayViewModel.HideAll();
        }
        public void ResetOverlays()
        {
            foreach (var overlay in _currentOverlays.Values.ToList())
            {
                SetSelected(false, overlay.CreatedType);
                overlay.RequestClose();
                overlay.TemporarilyHide();
            }
            _timersViewModel.HideTimers();
            _currentOverlays.Clear();
        }
        public bool OverlaysLocked
        {
            get => overlaysLocked;
            set
            {
                this.RaiseAndSetIfChanged(ref overlaysLocked, value);
                _timersViewModel.UpdateLock(overlaysLocked);
                _challengesViewModel.UpdateLock(overlaysLocked);
                _otherOverlayViewModel.UpdateLock(overlaysLocked);
                _personalOverlayViewModel.OverlaysMoveable = !OverlaysLocked;
                _abilityListSetup.UpdateLock(overlaysLocked);
                _raidNotesSetup.UpdateLock(overlaysLocked);
                ToggleOverlayLock();
                OverlayLockStateChanged();
                if (value)
                {
                    AvaloniaTimelineBuilder.LockOverlay();
                }         
                else
                {
                    AvaloniaTimelineBuilder.UnlockOverlay();
                }
            }
        }
        private void UpdatePersonalOverlayActive(bool obj)
        {
            AvailableUtilityOverlays.First(t=>t.Type == UtilityOverlayType.Personal).IsSelected = obj;
        }
        public List<string> AvailableTypes { get; private set; } = new() { "Damage", "Heals", "Tank" };
        public string SelectedType
        {
            get => selectedType; set
            {
                if (selectedType != value)
                {
                    this.RaiseAndSetIfChanged(ref selectedType, value);
                    _currentCharacterRole = GetRoleFromSelectedType(selectedType).ToString();
                    RefreshOverlays();
                    DefaultPersonalOverlaysManager.SelectNewDefault(selectedType);
                }


            }
        }

        public bool UsePersonalOverlay
        {
            get => usePersonalOverlay; set
            {
                _personalOverlayViewModel.Active = value;
                this.RaiseAndSetIfChanged(ref usePersonalOverlay, value);
            }
        }
        private string _previousRole;
        private int selectedOverlayTab;
        private RaidNotesSetupViewModel _raidNotesSetup;
        private UserControl _selectedOverlayTabContent;
        private CombatMetricsConfigView _configView;

        public bool UseDynamicLayout
        {
            get => useDynamicLayout; set
            {
                this.RaiseAndSetIfChanged(ref useDynamicLayout, value);
                if (useDynamicLayout && !string.IsNullOrEmpty(_previousRole))
                {
                    _currentCharacterRole = _previousRole;
                }
                if (!useDynamicLayout)
                {
                    _previousRole = _currentCharacterRole;
                    _currentCharacterRole = "Custom";
                }
                Settings.WriteSetting<bool>("DynamicLayout", useDynamicLayout);
                RefreshOverlays();
            }
        }
        private void SetSelected(bool selected, OverlayType overlay)
        {
            foreach (var overlayOption in MainDamageOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in AdvancedDamageOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in MainHealOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in AdvancedHealOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in MainMitigationOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in AdvancedMitigationOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in AvailableGeneralOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
        }
        private OverlayOptionViewModel GetType(OverlayType overlay)
        {
            foreach (var overlayOption in MainDamageOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in AdvancedDamageOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in MainHealOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in AdvancedHealOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in MainMitigationOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in AdvancedMitigationOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in AvailableGeneralOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            return new OverlayOptionViewModel();
        }
        private void ToggleOverlayLock()
        {
            if (!OverlaysLocked)
                _currentOverlays.ForEach(o => o.Value.UnlockOverlays());
            else
                _currentOverlays.ForEach(o => o.Value.LockOverlays());
            DefaultCharacterOverlays.SetLockedStateCharacter(OverlaysLocked, _currentCharacterRole);
        }
    }
}
