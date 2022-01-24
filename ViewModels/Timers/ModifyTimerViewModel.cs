using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures.RaidInfos;
using System.Collections.ObjectModel;
using SWTORCombatParser.Model.Timers;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class ModifyTimerViewModel : INotifyPropertyChanged
    {
        private TimerKeyType selectedTriggerType;
        private List<EncounterInfo> _encounters;
        private bool isAlert;
        private bool isPeriodic;
        private string selectedEncounter;
        private string selectedBoss;
        private bool isEditing;
        private List<string> defaultSourceTargets = new List<string> { "Any", "Local Player", "Custom" };
        private List<string> addedCustomSources = new List<string>();
        private List<string> addedCustomTargets = new List<string>();
        private List<BossInfo> _bossInfosForEncounter = new List<BossInfo>();
        private string customSource;
        private string customTarget;
        private string selectedTarget;
        private string selectedSource;
        private string _currentSelectedPlayer;

        public event Action<Timer, bool> OnNewTimer = delegate { };
        public event Action OnCancelEdit = delegate { };
        public bool TargetIsLocal;
        public bool SourceIsLocal;


        public event PropertyChangedEventHandler PropertyChanged;

        public bool TimerNameInError = false;
        public SolidColorBrush TimerNameHelpTextColor => TimerNameInError ? Brushes.Red : Brushes.LightGray;
        public string TriggerValueHelpText { get; set; }

        public bool ValueInError = false;
        private string selectedExternalTimerName;
        private bool canBeRefreshed;
        private string effect = "";
        private string customRefreshOption;

        public SolidColorBrush TriggerValueHelpTextColor => ValueInError ? Brushes.Red : Brushes.LightGray;

        public bool ShowAbilityOption { get; set; }
        public bool ShowEffectOption { get; set; }
        public bool ShowHPOption { get; set; }
        public bool ShowExternalTriggerOption { get; set; }
        public bool ShowCombatDurationOption { get; set; }
        public List<Timer> AvailableTimersForCharacter { get; set; } = new List<Timer>();
        public List<string> AvailableTimerNames => AvailableTimersForCharacter.Select(t => t.Name).ToList();
        public string SelectedExternalTimerId { get; set; }
        public string SelectedExternalTimerName
        {
            get => selectedExternalTimerName; set
            {
                selectedExternalTimerName = value;
                if (string.IsNullOrEmpty(selectedExternalTimerName))
                    return;
                SelectedExternalTimerId = AvailableTimersForCharacter.First(t => t.Name == selectedExternalTimerName).Id;
                OnPropertyChanged();
            }
        }
        public Timer ExternalTimer { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
        public List<TimerKeyType> AvailableTriggerTypes { get; set; } = new List<TimerKeyType>();
        public TimerKeyType SelectedTriggerType
        {
            get => selectedTriggerType; set

            {
                selectedTriggerType = value;
                UpdateUIForTriggerType();
            }
        }
        public bool TrackOutsideOfCombat { get; set; }
        public bool CanChangeCombatTracking { get; set; }
        public string Ability { get; set; } = "";
        public string Effect
        {
            get => effect; set
            {
                effect = value;
                if (!string.IsNullOrEmpty(effect))
                {
                    if (AvailableRefreshOptions.Any(r => r.Name == effect))
                        return;
                    var selfRefreshOption = new RefreshOptionViewModel() { Name = effect };
                    selfRefreshOption.RemoveRequested += RemoveRefreshOption;
                    AvailableRefreshOptions.Add(selfRefreshOption);
                }
            }
        }
        public double HPPercentage { get; set; }
        public double CombatDuration { get; set; }
        public ObservableCollection<string> AvailableSources { get; set; } = new ObservableCollection<string>();
        public string SelectedSource
        {
            get => selectedSource; set
            {
                selectedSource = value;
                if (SelectedSource == "Local Player")
                    SourceIsLocal = true;
                else
                    SourceIsLocal = false;
                if (selectedSource == "Custom")
                {
                    HasCustomSource = true;
                }
                else
                {
                    HasCustomSource = false;
                }
                if (value == null)
                    selectedSource = "Ignore";
                OnPropertyChanged("HasCustomSource");
            }
        }
        public string CustomSource
        {
            get => customSource; set
            {
                customSource = value;
            }
        }
        public ICommand SaveSourceCommand => new CommandHandler(SaveSource);

        internal void SaveSource(object obj = null)
        {
            addedCustomSources.Add(CustomSource);
            AvailableSources.Add(CustomSource);
            SelectedSource = CustomSource;
            CustomSource = "";
            HasCustomSource = false;
            OnPropertyChanged("HasCustomSource");
            OnPropertyChanged("SelectedSource");
            OnPropertyChanged("CustomSource");
            OnPropertyChanged("AvailableSources");
        }

        public bool HasCustomSource { get; set; }
        public bool HasSource { get; set; }
        public string SourceText { get; set; }
        public ObservableCollection<string> AvailableTargets { get; set; } = new ObservableCollection<string>();
        public string SelectedTarget
        {
            get => selectedTarget; set
            {
                selectedTarget = value;
                if (SelectedTarget == "Local Player")
                    TargetIsLocal = true;
                else
                    TargetIsLocal = false;
                if (selectedTarget == "Custom")
                {
                    HasCustomTarget = true;
                }
                else
                {
                    HasCustomTarget = false;
                }
                if (value == null)
                    selectedTarget = "Ignore";
                OnPropertyChanged("HasCustomTarget");
            }
        }
        public string CustomTarget
        {
            get => customTarget; set
            {
                customTarget = value;
            }
        }

        public ICommand SaveTargetCommand => new CommandHandler(SaveTarget);

        internal void SaveTarget(object obj = null)
        {
            addedCustomTargets.Add(CustomTarget);
            AvailableTargets.Add(CustomTarget);
            SelectedTarget = CustomTarget;
            CustomTarget = "";
            HasCustomTarget = false;
            OnPropertyChanged("SelectedTarget");
            OnPropertyChanged("HasCustomTarget");
            OnPropertyChanged("CustomTarget");
            OnPropertyChanged("AvailableTargets");
        }
        public bool HasCustomTarget { get; set; }
        public bool HasTarget { get; set; }
        public string TargetText { get; set; }
        public bool CanBeRefreshed
        {
            get => canBeRefreshed; set
            {
                canBeRefreshed = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<RefreshOptionViewModel> AvailableRefreshOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string CustomRefreshOption
        {
            get => customRefreshOption; set
            {
                customRefreshOption = value;
                OnPropertyChanged();
            }
        }
        public ICommand SaveRefreshOptionCommand => new CommandHandler(SaveRefreshCommand);

        private void SaveRefreshCommand(object obj)
        {
            if (string.IsNullOrEmpty(CustomRefreshOption))
                return;
            var newRefreshOption = new RefreshOptionViewModel() { Name = CustomRefreshOption };
            newRefreshOption.RemoveRequested += RemoveRefreshOption;
            AvailableRefreshOptions.Add(newRefreshOption);
            OnPropertyChanged("AvailableRefreshOptions");
            CustomRefreshOption = "";
        }

        private void RemoveRefreshOption(RefreshOptionViewModel obj)
        {
            AvailableRefreshOptions.Remove(obj);
        }

        public List<string> AvailableEncounters { get; set; } = new List<string>();
        public string SelectedEncounter
        {
            get => selectedEncounter; set
            {
                if (string.IsNullOrEmpty(value))
                    return;
                selectedEncounter = value;
                if (selectedEncounter == "All" || selectedEncounter.Contains("--"))
                {
                    AvailableBosses = new List<string>();
                    AvailableBosses.Insert(0, "All");
                    InitSourceAndTargetValues();
                }
                else
                {
                    _bossInfosForEncounter = _encounters.First(e => e.Name == selectedEncounter).BossInfos;
                    AvailableBosses = _bossInfosForEncounter.Select(bi => bi.EncounterName).ToList();
                    AvailableBosses.Insert(0, "All");
                    SetTargetsBasedOnEncouters();
                }
                SelectedBoss = "All";
                OnPropertyChanged("SelectedEncounter");
                OnPropertyChanged("SelectedBoss");
                OnPropertyChanged("AvailableBosses");
            }
        }

        public List<string> AvailableBosses { get; set; } = new List<string>();
        public string SelectedBoss
        {
            get => selectedBoss; set
            {
                if (string.IsNullOrEmpty(value))
                    selectedBoss = "All";
                else
                    selectedBoss = value;
                SetTargetsBasedOnEncouters();
            }
        }

        public bool IsAlert
        {
            get => isAlert; set
            {
                isAlert = value;
                OnPropertyChanged("ShowDuration");
            }
        }
        public double DurationSec { get; set; }
        public bool ShowDuration => !IsAlert;
        public bool IsPeriodic
        {
            get => isPeriodic; set
            {
                isPeriodic = value;
                OnPropertyChanged("IsPeriodic");
            }
        }
        public int Repeats { get; set; }

        public Color SelectedColor { get; set; } = Colors.CornflowerBlue;

        public ModifyTimerViewModel(string selectedPlayer)
        {
            _currentSelectedPlayer = selectedPlayer;
            AvailableTimersForCharacter = DefaultTimersManager.GetDefaults(_currentSelectedPlayer).Timers;
            OnPropertyChanged("AvailableTimerNames");
            Id = Guid.NewGuid().ToString();
            AvailableTriggerTypes = Enum.GetValues<TimerKeyType>().ToList();
            _encounters = RaidNameLoader.SupportedEncounters;
            var flashpoints = _encounters.Where(e => e.EncounterType == EncounterType.Flashpoint).OrderBy(f => f.Name);
            var flashpointNames = flashpoints.Select(f => f.Name);
            var operations = _encounters.Where(e => e.EncounterType == EncounterType.Operation).OrderBy(o => o.Name);
            var operationNames = operations.Select(o => o.Name);
            var lairs = _encounters.Where(e => e.EncounterType == EncounterType.Lair).OrderBy(l => l.Name);
            var lairNames = lairs.Select(l => l.Name);
            var listOfEncounters = new List<string>();
            listOfEncounters.Add("All");
            listOfEncounters.Add("--Operations--");
            listOfEncounters.AddRange(operationNames);
            listOfEncounters.Add("--Lairs--");
            listOfEncounters.AddRange(lairNames);
            listOfEncounters.Add("--Flashpoints--");
            listOfEncounters.AddRange(flashpointNames);
            AvailableEncounters = listOfEncounters;
            AvailableBosses.Insert(0, "All");
            SelectedEncounter = "All";
            SelectedBoss = "All";
            OnPropertyChanged("SelectedEncounter");
            OnPropertyChanged("SelectedBoss");
            OnPropertyChanged("AvailableBosses");
            OnPropertyChanged("AvailableEncounters");
        }


        private void ResetUI()
        {
            ShowAbilityOption = false;
            ShowEffectOption = false;
            ShowHPOption = false;
            ShowCombatDurationOption = false;
            ShowExternalTriggerOption = false;
            HasSource = false;
            HasTarget = false;
            HasCustomTarget = false;
            HasCustomSource = false;
            Effect = "";
            Ability = "";
            HPPercentage = 0;
            CombatDuration = 0;
            OnPropertyChanged("Effect");
            OnPropertyChanged("Ability");
            OnPropertyChanged("CombatDuration");
            OnPropertyChanged("HPPercentage");
            OnPropertyChanged("HasCustomTarget");
            OnPropertyChanged("HasCustomSource");
            OnPropertyChanged("ShowAbilityOption");
            OnPropertyChanged("ShowEffectOption");
            OnPropertyChanged("ShowCombatDurationOption");
            OnPropertyChanged("ShowExternalTriggerOption");
            OnPropertyChanged("ShowHPOption");
            OnPropertyChanged("HasSource");
            OnPropertyChanged("HasTarget");
        }

        public void Edit(Timer timerToEdit)
        {
            isEditing = true;
            Id = string.IsNullOrEmpty(timerToEdit.Id) ? Guid.NewGuid().ToString() : timerToEdit.Id;
            Name = timerToEdit.Name;
            SelectedTriggerType = timerToEdit.TriggerType;
            CombatDuration = timerToEdit.CombatTimeElapsed;
            SelectedColor = timerToEdit.TimerColor;
            IsAlert = timerToEdit.IsAlert;
            IsPeriodic = timerToEdit.IsPeriodic;
            Repeats = timerToEdit.Repeats;
            Effect = timerToEdit.Effect;
            Ability = timerToEdit.Ability;
            TrackOutsideOfCombat = timerToEdit.TrackOutsideOfCombat;
            SelectedExternalTimerId = timerToEdit.ExperiationTimerId;
            SelectedExternalTimerName = string.IsNullOrEmpty(SelectedExternalTimerId) ? "" : AvailableTimersForCharacter.First(t => t.Id == SelectedExternalTimerId).Name;
            DurationSec = timerToEdit.DurationSec;
            HPPercentage = timerToEdit.HPPercentage;
            SelectedEncounter = timerToEdit.SpecificEncounter;
            SelectedBoss = timerToEdit.SpecificBoss;
            CanBeRefreshed = timerToEdit.CanBeRefreshed;
            var addedAbilities = timerToEdit.AbilitiesThatRefresh.Select(a => new RefreshOptionViewModel() { Name = a }).ToList();
            addedAbilities.ForEach(a => a.RemoveRequested += RemoveRefreshOption);
            AvailableRefreshOptions = new ObservableCollection<RefreshOptionViewModel>(addedAbilities);
            if (timerToEdit.SourceIsLocal)
                SelectedSource = "Local Player";
            else
            {
                SelectedSource = timerToEdit.Source;
            }
            if (timerToEdit.TargetIsLocal)
                SelectedTarget = "Local Player";
            else
            {
                SelectedTarget = timerToEdit.Target;
            }
            if(!AvailableTargets.Contains(timerToEdit.Target))
            {
                AvailableTargets.Add(timerToEdit.Target);
                SelectedTarget = timerToEdit.Target;
            }
            if (!AvailableSources.Contains(timerToEdit.Source))
            {
                AvailableSources.Add(timerToEdit.Source);
                SelectedSource = timerToEdit.Source;
            }
            OnPropertyChanged("AvailableRefreshOptions");
            OnPropertyChanged("Name");
            OnPropertyChanged("CombatDuration");
            OnPropertyChanged("TrackOutsideOfCombat");
            OnPropertyChanged("SelectedTriggerType");
            OnPropertyChanged("SelectedColor");
            OnPropertyChanged("IsAlert");
            OnPropertyChanged("IsPeriodic");
            OnPropertyChanged("Repeats");
            OnPropertyChanged("Effect");
            OnPropertyChanged("Ability");
            OnPropertyChanged("DurationSec");
            OnPropertyChanged("HPPercentage");
            OnPropertyChanged("SelectedEncounter");
            OnPropertyChanged("SelectedBoss");
            OnPropertyChanged("SelectedSource");
            OnPropertyChanged("SelectedTarget");
        }
        public void Cancel()
        {
            if (isEditing)
                OnCancelEdit();
        }

        public ICommand SaveCommand => new CommandHandler(Save);

        private void Save(object obj)
        {
            var isValid = Validate();
            if (!isValid)
                return;
            var newTimer = new Timer()
            {
                Id = Id,
                CharacterOwner = _currentSelectedPlayer,
                Name = Name,
                TrackOutsideOfCombat = TrackOutsideOfCombat,
                CombatTimeElapsed = CombatDuration,
                IsEnabled = true,
                Source = SelectedSource,
                SourceIsLocal = SourceIsLocal,
                Target = SelectedTarget,
                TargetIsLocal = TargetIsLocal,
                HPPercentage = HPPercentage,
                TriggerType = SelectedTriggerType,
                ExperiationTimerId = SelectedExternalTimerId,
                Ability = Ability,
                Effect = Effect,
                IsPeriodic = IsPeriodic,
                Repeats = Repeats,
                IsAlert = IsAlert || DurationSec == 0,
                DurationSec = DurationSec,
                TimerColor = SelectedColor,
                SpecificBoss = SelectedBoss,
                SpecificEncounter = SelectedEncounter,
                CanBeRefreshed = CanBeRefreshed,
                AbilitiesThatRefresh = AvailableRefreshOptions.Select(r=>r.Name).ToList()
                

            };
            OnNewTimer(newTimer, isEditing);
        }
        private bool Validate()
        {
            TimerNameInError = false;
            ValueInError = false;
            OnPropertyChanged("TimerNameHelpTextColor");
            OnPropertyChanged("TriggerValueHelpTextColor");
            if (AvailableTimerNames.Contains(Name) && !isEditing)
            {
                Name = "";
                TimerNameInError = true;
                OnPropertyChanged("TimerNameHelpTextColor");
                return false;
            }
            switch (selectedTriggerType)
            {
                case TimerKeyType.CombatStart:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.TargetChanged:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.AbilityUsed:
                    {
                        var isValid = true;
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            isValid = false;
                        }
                        if (string.IsNullOrEmpty(Ability))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TriggerValueHelpTextColor");
                            isValid = false;
                        }
                        if (!isValid)
                            return false;
                        return true;
                    }
                case TimerKeyType.EffectGained:
                    {
                        var isValid = true;
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            isValid = false;
                        }
                        if (string.IsNullOrEmpty(Effect))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TriggerValueHelpTextColor");
                            isValid = false;
                        }
                        if (!isValid)
                            return false;
                        return true;
                    }
                case TimerKeyType.EffectLost:
                    {
                        var isValid = true;
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            isValid = false;
                        }
                        if (string.IsNullOrEmpty(Effect))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TriggerValueHelpTextColor");
                            isValid = false;
                        }
                        if (!isValid)
                            return false;
                        return true;
                    }
                case TimerKeyType.EntityHP:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.TimerExpired:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(SelectedExternalTimerName))
                            return false;
                        return true;
                    }
                case TimerKeyType.FightDuration:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                default:
                    return false;
            }
        }
        private void UpdateUIForTriggerType()
        {
            ResetUI();
            switch (selectedTriggerType)
            {
                case TimerKeyType.CombatStart:
                    {
                        CanChangeCombatTracking = false;
                        TrackOutsideOfCombat = false;
                        OnPropertyChanged("TrackOutsideOfCombat");
                        OnPropertyChanged("CanChangeCombatTracking");
                        break;
                    }
                case TimerKeyType.AbilityUsed:
                    {
                        ShowAbilityOption = true;
                        HasSource = true;
                        HasTarget = true;
                        CanChangeCombatTracking = true;
                        SourceText = "When Used By";
                        TargetText = "When Used On";
                        TriggerValueHelpText = "Name or Id";
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("ShowAbilityOption");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("SourceText");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.EffectGained:
                    {
                        AvailableRefreshOptions.Clear();
                        ShowEffectOption = true;
                        HasSource = true;
                        HasTarget = true;
                        CanChangeCombatTracking = true;
                        SourceText = "When Applied By";
                        TargetText = "When Applied To";
                        TriggerValueHelpText = "Name or Id";
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("SourceText");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.TargetChanged:
                    {
                        AvailableRefreshOptions.Clear();
                        ShowEffectOption = false;
                        HasSource = true;
                        HasTarget = true;
                        CanChangeCombatTracking = true;
                        SourceText = "Targeted By";
                        TargetText = "Is Targeted";
                        TriggerValueHelpText = "Name or Id";
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("SourceText");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.EffectLost:
                    {
                        ShowEffectOption = true;
                        HasTarget = true;
                        CanChangeCombatTracking = true;
                        TargetText = "When Lost By";
                        TriggerValueHelpText = "Name or Id";
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("HasTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.EntityHP:
                    {
                        ShowHPOption = true;
                        HasTarget = true;
                        TargetText = "Entity";
                        TriggerValueHelpText = "";
                        CanChangeCombatTracking = false;
                        TrackOutsideOfCombat = false;
                        OnPropertyChanged("TrackOutsideOfCombat");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("ShowHPOption");
                        OnPropertyChanged("HasTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.TimerExpired:
                    {
                        ShowExternalTriggerOption = true;
                        CanChangeCombatTracking = true;
                        TrackOutsideOfCombat = false;
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowExternalTriggerOption");
                        break;
                    }
                case TimerKeyType.FightDuration:
                    {
                        ShowCombatDurationOption = true;
                        CanChangeCombatTracking = false;
                        TrackOutsideOfCombat = false;
                        OnPropertyChanged("TrackOutsideOfCombat");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowCombatDurationOption");
                        break;
                    }
            }
        }
        private void InitSourceAndTargetValues()
        {
            SetTargetsBasedOnEncouters();
            SelectedSource = "Any";
            SelectedTarget = "Any";
            OnPropertyChanged("AvailableSources");
            OnPropertyChanged("SelectedSource");
            OnPropertyChanged("AvailableTargets");
            OnPropertyChanged("SelectedTarget");
        }
        private void SetTargetsBasedOnEncouters()
        {
            AvailableSources = new ObservableCollection<string>(defaultSourceTargets.Concat(addedCustomSources));
            AvailableTargets = new ObservableCollection<string>(defaultSourceTargets.Concat(addedCustomTargets));
            var targetsInFight = new List<string>();
            if (SelectedEncounter != "All")
            {
                if (selectedBoss != "All")
                {
                    var bossInfo = _bossInfosForEncounter.First(bi => bi.EncounterName == selectedBoss);
                    targetsInFight = bossInfo.TargetNames;
                }
                else
                {
                    targetsInFight = _bossInfosForEncounter.SelectMany(bi => bi.TargetNames).ToList();
                }
                foreach (var boss in targetsInFight)
                {
                    AvailableSources.Add(boss);
                    AvailableTargets.Add(boss);
                }

            }
            OnPropertyChanged("AvailableSources");
            OnPropertyChanged("AvailableTargets");
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
