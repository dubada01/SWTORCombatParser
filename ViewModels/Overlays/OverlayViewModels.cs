using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Overlays.Personal;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayViewModel : INotifyPropertyChanged
    {

        private List<OverlayInstanceViewModel> _currentOverlays = new List<OverlayInstanceViewModel>();
        private Dictionary<string, OverlayInfo> _overlayDefaults = new Dictionary<string, OverlayInfo>();
        private string _currentCharacterRole = Role.DPS.ToString();
        private bool overlaysLocked = true;
        private LeaderboardType selectedLeaderboardType;
        private TimersCreationViewModel _timersViewModel;
        private ChallengeSetupViewModel _challengesViewModel;
        private OthersOverlaySetupViewModel _otherOverlayViewModel;
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

        public TimersCreationView TimersView { get; set; }
        public ChallengeSetupView ChallengesView { get; set; }
        public OtherOverlaySetupView OthersSetupView { get; set; }

        public ObservableCollection<OverlayOptionViewModel> AvailableDamageOverlays { get; set; } = new ObservableCollection<OverlayOptionViewModel>();
        public ObservableCollection<OverlayOptionViewModel> AvailableHealOverlays { get; set; } = new ObservableCollection<OverlayOptionViewModel>();
        public ObservableCollection<OverlayOptionViewModel> AvailableMitigationOverlays { get; set; } = new ObservableCollection<OverlayOptionViewModel>();
        public ObservableCollection<OverlayOptionViewModel> AvailableGeneralOverlays { get; set; } = new ObservableCollection<OverlayOptionViewModel>();
        public List<LeaderboardType> LeaderboardTypes { get; set; } = new List<LeaderboardType>();
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
                sizeScalarString = value;
                var stringVal = 0d;
                if (double.TryParse(sizeScalarString, out stringVal))
                {
                    SizeScalar = stringVal;
                }
                OnPropertyChanged();
            }
        }
        public double SizeScalar
        {
            get { return sizeScalar; }
            set
            {
                sizeScalar = value;
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
                _currentOverlays.ForEach(overlay => overlay.SizeScalar = sizeScalar);

                SetOverlaysScale();

                Settings.WriteSetting<double>("overlay_bar_scale", sizeScalar);
                OnPropertyChanged();
            }
        }
        private void SetOverlaysScale()
        {
            _otherOverlayViewModel.SetScalar(sizeScalar);
            _timersViewModel.SetScalar(sizeScalar);
            _challengesViewModel.SetScalar(sizeScalar);
            _personalOverlayViewModel.UpdateScale(sizeScalar);
        }
        public OverlayViewModel()
        {
            CombatLogStateBuilder.PlayerDiciplineChanged += UpdateOverlaysForDiscipline;
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
            var enumVals = EnumUtil.GetValues<OverlayType>().OrderBy(d => d.ToString());
            foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
            {
                if (enumVal == OverlayType.DPS || enumVal == OverlayType.Damage || enumVal == OverlayType.BurstDPS || enumVal == OverlayType.FocusDPS || enumVal == OverlayType.NonEDPS || enumVal == OverlayType.RawDamage)
                    AvailableDamageOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.HPS || enumVal == OverlayType.RawHealing || enumVal == OverlayType.EHPS || enumVal == OverlayType.EffectiveHealing || enumVal == OverlayType.BurstEHPS || enumVal == OverlayType.HealReactionTime || enumVal == OverlayType.HealReactionTimeRatio || enumVal == OverlayType.TankHealReactionTime)
                    AvailableHealOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.Mitigation || enumVal == OverlayType.ShieldAbsorb || enumVal == OverlayType.ProvidedAbsorb || enumVal == OverlayType.DamageTaken || enumVal == OverlayType.DamageAvoided || enumVal == OverlayType.DamageSavedDuringCD)
                    AvailableMitigationOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.APM || enumVal == OverlayType.InterruptCount || enumVal == OverlayType.ThreatPerSecond || enumVal == OverlayType.Threat)
                    AvailableGeneralOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
            }
            TimersView = new TimersCreationView();
            _timersViewModel = new TimersCreationViewModel();
            TimersView.DataContext = _timersViewModel;

            ChallengesView = new ChallengeSetupView();
            _challengesViewModel = new ChallengeSetupViewModel();
            ChallengesView.DataContext = _challengesViewModel;

            _otherOverlayViewModel = new OthersOverlaySetupViewModel();
            OthersSetupView = new OtherOverlaySetupView();
            OthersSetupView.DataContext = _otherOverlayViewModel;

            _personalOverlayViewModel = new PersonalOverlayViewModel(sizeScalar);
            usePersonalOverlay = _personalOverlayViewModel.Active;
            _personalOverlayViewModel.ActiveChanged += UpdatePersonalOverlayActive;

            SetOverlaysScale();
            RefreshOverlays();
        }

        private void UpdateOverlaysForDiscipline(Entity character, SWTORClass arg2)
        {
            var nextDiscipline = arg2.Role.ToString();
            if (_currentCharacterRole == nextDiscipline)
                return;
            _currentCharacterRole = arg2.Role.ToString();
            if (historicalParseFinished)
            {
                RefreshOverlays();
            }
        }

        private void RefreshOverlays()
        {
            ResetOverlays();
            _timersViewModel.TryShow();
            if (!DefaultCharacterOverlays.DoesKeyExist(_currentCharacterRole))
            {
                InitializeRoleBasedOverlays();
            }
            if (UseDynamicLayout)
            {
                SetOverlaysToRole();
            }
            else
            {
                SetOverlaysToCustom();
            }
        }

        private void SetOverlaysToCustom()
        {
            SetPersonalByClass(Role.DPS);
            _overlayDefaults = DefaultCharacterOverlays.GetCharacterDefaults("DPS");

            UpdateOverlays();

        }
        private void SetOverlaysToRole()
        {
            SetPersonalByClass(Enum.Parse<Role>(_currentCharacterRole));
            _overlayDefaults = DefaultCharacterOverlays.GetCharacterDefaults(_currentCharacterRole);

            UpdateOverlays();

        }
        private void InitializeRoleBasedOverlays()
        {
            var mostUsedOverlayLayout = DefaultCharacterOverlays.GetMostUsedLayout();
            if (!string.IsNullOrEmpty(mostUsedOverlayLayout))
            {
                DefaultCharacterOverlays.CopyFromKey(mostUsedOverlayLayout, "DPS");
                DefaultCharacterOverlays.CopyFromKey(mostUsedOverlayLayout, "Healer");
                DefaultCharacterOverlays.CopyFromKey(mostUsedOverlayLayout, "Tank");
            }
            else
            {
                DefaultCharacterOverlays.InitializeCharacterDefaults("DPS");
                DefaultCharacterOverlays.InitializeCharacterDefaults("Healer");
                DefaultCharacterOverlays.InitializeCharacterDefaults("Tank");
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
            App.Current.Dispatcher.Invoke(() =>
            {
                if (_overlayDefaults.Count == 0)
                    return;
                if (_overlayDefaults.First().Value.Locked)
                {
                    OverlaysLocked = true;
                    OnPropertyChanged("OverlaysLocked");
                }
                var enumVals = EnumUtil.GetValues<OverlayType>();
                foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
                {
                    if (!_overlayDefaults.ContainsKey(enumVal.ToString()))
                        continue;
                    if (_overlayDefaults[enumVal.ToString()].Acive)
                        CreateOverlay(GetType(enumVal), false);
                }
                _currentOverlays.ForEach(o => o.CharacterDetected(_currentCharacterRole));
            });
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
            UpdateOverlaysForDiscipline(localPlayer, currentDiscipline);
            RefreshOverlays();
        }
        private void HistoricalLogsStarted()
        {
            historicalParseFinished = false;
        }

        public ICommand GenerateOverlay => new CommandHandler(v => CreateOverlay((OverlayOptionViewModel)v, true));

        private void CreateOverlay(OverlayOptionViewModel type, bool canDelete)
        {
            OverlayOptionViewModel overlayType = type;
            if (_currentOverlays.Any(o => o.CreatedType == overlayType.Type))
            {
                if (!canDelete)
                    return;
                var currentOverlay = _currentOverlays.First(o => o.CreatedType == overlayType.Type);
                currentOverlay.RequestClose();
                RemoveOverlay(currentOverlay);
                return;
            }
            overlayType.IsSelected = true;
            var viewModel = new OverlayInstanceViewModel(overlayType.Type);
            DefaultCharacterOverlays.SetActiveStateCharacter(viewModel.Type.ToString(), true, _currentCharacterRole);
            viewModel.OverlayClosed += RemoveOverlay;
            viewModel.OverlaysMoveable = !OverlaysLocked;
            viewModel.SizeScalar = SizeScalar;
            _currentOverlays.Add(viewModel);
            var overlay = new InfoOverlay(viewModel);
            overlay.SetPlayer(_currentCharacterRole);
            var stringType = viewModel.Type.ToString();
            if (_overlayDefaults.ContainsKey(stringType))
            {
                overlay.Top = _overlayDefaults[stringType].Position.Y;
                overlay.Left = _overlayDefaults[stringType].Position.X;
                overlay.Width = _overlayDefaults[stringType].WidtHHeight.X;
                overlay.Height = _overlayDefaults[stringType].WidtHHeight.Y;
            }
            overlay.Show();
            viewModel.Refresh(CombatIdentifier.CurrentCombat);
            if (OverlaysLocked)
                viewModel.LockOverlays();
        }

        private void RemoveOverlay(OverlayInstanceViewModel obj)
        {
            DefaultCharacterOverlays.SetActiveStateCharacter(obj.Type.ToString(), false, _currentCharacterRole);
            _currentOverlays.Remove(obj);
            SetSelected(false, obj.CreatedType);
        }
        public void HideOverlays()
        {
            ResetOverlays();
            _otherOverlayViewModel.HideAll();
        }
        public void ResetOverlays()
        {
            foreach (var overlay in _currentOverlays.ToList())
            {
                SetSelected(false, overlay.CreatedType);
                overlay.RequestClose();
            }
            _timersViewModel.HideTimers();
            _currentOverlays.Clear();
        }
        public bool OverlaysLocked
        {
            get => overlaysLocked;
            set
            {
                overlaysLocked = value;
                _timersViewModel.UpdateLock(value);
                _challengesViewModel.UpdateLock(value);
                _otherOverlayViewModel.UpdateLock(overlaysLocked);
                _personalOverlayViewModel.UpdateLock(overlaysLocked);
                ToggleOverlayLock();
                OverlayLockStateChanged();
                OnPropertyChanged();
            }
        }
        private void UpdatePersonalOverlayActive(bool obj)
        {
            usePersonalOverlay = obj;
            OnPropertyChanged("UsePersonalOverlay");
        }
        public List<string> AvailableTypes { get; private set; } = new List<string> { "Damage", "Heals", "Tank" };
        public string SelectedType
        {
            get => selectedType; set
            {
                if (selectedType != value)
                {
                    selectedType = value;
                    _currentCharacterRole = GetRoleFromSelectedType(selectedType).ToString();
                    RefreshOverlays();
                    DefaultPersonalOverlaysManager.SelectNewDefault(selectedType);
                    OnPropertyChanged();
                }


            }
        }

        public bool UsePersonalOverlay
        {
            get => usePersonalOverlay; set
            {
                _personalOverlayViewModel.Active = value;
                usePersonalOverlay = value;
                OnPropertyChanged();
            }
        }
        private string _previousRole;
        public bool UseDynamicLayout
        {
            get => useDynamicLayout; set
            {
                useDynamicLayout = value;
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
                OnPropertyChanged();
            }
        }
        private void SetSelected(bool selected, OverlayType overlay)
        {
            foreach (var overlayOption in AvailableDamageOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in AvailableHealOverlays)
            {
                if (overlayOption.Type == overlay)
                    overlayOption.IsSelected = selected;
            }
            foreach (var overlayOption in AvailableMitigationOverlays)
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
            foreach (var overlayOption in AvailableDamageOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in AvailableHealOverlays)
            {
                if (overlayOption.Type == overlay)
                    return overlayOption;
            }
            foreach (var overlayOption in AvailableMitigationOverlays)
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
                _currentOverlays.ForEach(o => o.UnlockOverlays());
            else
                _currentOverlays.ForEach(o => o.LockOverlays());
            DefaultCharacterOverlays.SetLockedStateCharacter(OverlaysLocked, _currentCharacterRole);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
