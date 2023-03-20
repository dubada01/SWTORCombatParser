using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using SWTORCombatParser.ViewModels.Overlays.Room;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overlay.Room;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using SWTORCombatParser.Views.Overlay.BossFrame;
using SWTORCombatParser.Views.Overlay.PvP;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using SWTORCombatParser.ViewModels.Overlays.PvP;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.Model.Challenge;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayViewModel : INotifyPropertyChanged
    {

        private List<OverlayInstanceViewModel> _currentOverlays = new List<OverlayInstanceViewModel>();
        private Dictionary<string, OverlayInfo> _overlayDefaults = new Dictionary<string, OverlayInfo>();
        private string _currentCharacterDiscipline = "None";
        private bool overlaysLocked = true;
        private LeaderboardType selectedLeaderboardType;
        private TimersCreationViewModel _timersViewModel;
        private ChallengeSetupViewModel _challengesViewModel;
        private OthersOverlaySetupViewModel _otherOverlayViewModel;
        private double maxScalar = 1.5d;
        private double minScalar = 0.5d;
        private double sizeScalar = 1d;
        private string sizeScalarString = "1";
        private bool historicalParseFinished = false;
        private string sizeScalarString1;

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
        }
        public OverlayViewModel()
        {
            CombatLogStateBuilder.PlayerDiciplineChanged += UpdateOverlaysForDiscipline;
            CombatLogStreamer.HistoricalLogsFinished += FinishHistoricalParse;
            CombatLogStreamer.HistoricalLogsStarted += HistoricalLogsStarted;
            sizeScalar = Settings.ReadSettingOfType<double>("overlay_bar_scale");
            
            sizeScalarString = sizeScalar.ToString();
            LeaderboardTypes = EnumUtil.GetValues<LeaderboardType>().ToList();
            SelectedLeaderboardType = LeaderboardSettings.ReadLeaderboardSettings();
            DefaultCharacterOverlays.Init();
            DefaultGlobalOverlays.Init();
            DefaultChallengeManager.Init();
            var enumVals = EnumUtil.GetValues<OverlayType>().OrderBy(d => d.ToString());
            foreach (var enumVal in enumVals.Where(e => e != OverlayType.None))
            {
                if (enumVal == OverlayType.DPS ||enumVal == OverlayType.Damage || enumVal == OverlayType.BurstDPS || enumVal == OverlayType.FocusDPS || enumVal == OverlayType.NonEDPS || enumVal == OverlayType.RawDamage)
                    AvailableDamageOverlays.Add(new OverlayOptionViewModel() { Type = enumVal });
                if (enumVal == OverlayType.HPS  || enumVal == OverlayType.RawHealing|| enumVal == OverlayType.EHPS || enumVal == OverlayType.EffectiveHealing || enumVal == OverlayType.BurstEHPS || enumVal == OverlayType.HealReactionTime || enumVal == OverlayType.HealReactionTimeRatio || enumVal == OverlayType.TankHealReactionTime)
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

            SetOverlaysScale();
        }

        private void UpdateOverlaysForDiscipline(Entity character, SWTORClass arg2)
        {
            var nextDiscipline = character.Name + "_" + arg2.Discipline;
            if (_currentCharacterDiscipline == nextDiscipline)
                return;
            ResetOverlays();
            _currentCharacterDiscipline = nextDiscipline;
            _timersViewModel.TryShow();
            _overlayDefaults = DefaultCharacterOverlays.GetCharacterDefaults(_currentCharacterDiscipline);
            if (historicalParseFinished)
            {
                UpdateOverlays();
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
                _currentOverlays.ForEach(o => o.CharacterDetected(_currentCharacterDiscipline));
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
            UpdateOverlays();
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
            DefaultCharacterOverlays.SetActiveStateCharacter(viewModel.Type.ToString(), true, _currentCharacterDiscipline);
            viewModel.OverlayClosed += RemoveOverlay;
            viewModel.OverlaysMoveable = !OverlaysLocked;
            viewModel.SizeScalar = SizeScalar;
            _currentOverlays.Add(viewModel);
            var overlay = new InfoOverlay(viewModel);
            overlay.SetPlayer(_currentCharacterDiscipline);
            var stringType = viewModel.Type.ToString();
            if (_overlayDefaults.ContainsKey(stringType))
            {
                overlay.Top = _overlayDefaults[stringType].Position.Y;
                overlay.Left = _overlayDefaults[stringType].Position.X;
                overlay.Width = _overlayDefaults[stringType].WidtHHeight.X;
                overlay.Height = _overlayDefaults[stringType].WidtHHeight.Y;
                overlay.SetWindowState(_overlayDefaults[stringType].UseAsWindow);
            }
            overlay.Show();
            viewModel.Refresh(CombatIdentifier.CurrentCombat);
            if (OverlaysLocked)
                viewModel.LockOverlays();
        }

        private void RemoveOverlay(OverlayInstanceViewModel obj)
        {
            DefaultCharacterOverlays.SetActiveStateCharacter(obj.Type.ToString(), false, _currentCharacterDiscipline);
            _currentOverlays.Remove(obj);
            SetSelected(false, obj.Type);
        }
        public void HideOverlays()
        {
            ResetOverlays();
            _otherOverlayViewModel.HideAll();
        }
        public void ResetOverlays()
        {
            _currentCharacterDiscipline = "";
            foreach (var overlay in _currentOverlays.ToList())
            {
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
                if (overlaysLocked)
                {
                    _otherOverlayViewModel.UpdateLock(overlaysLocked);
                }
                else
                {
                    _otherOverlayViewModel.UpdateLock(overlaysLocked);
                }

                ToggleOverlayLock();
                OverlayLockStateChanged();
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
            DefaultCharacterOverlays.SetLockedStateCharacter(OverlaysLocked, _currentCharacterDiscipline);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void LiveParseStarted(bool state)
        {
            _otherOverlayViewModel.UpdateLiveParse(state);
        }
    }
}
