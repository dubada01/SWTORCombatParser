using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using ReactiveUI;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public enum TimerTargetType
    {
        Any,
        Players,
        NotPlayers,
        LocalPlayer,
        NotLocalPlayer,
        Boss,
        NotBoss,
        CurrentTarget,
        Custom
    }
    public class ModifyTimerViewModel :ReactiveObject, INotifyPropertyChanged
    {
        private TimerKeyType selectedTriggerType;
        private bool isAlert;
        private bool isPeriodic;
        private Timer _editedTimer;
        private string selectedEncounter;
        private string selectedDifficulty;
        private string selectedBoss;

        private bool _isBuiltInDCD;
        private bool _isBuiltInOCD;
        private bool _isBuiltInHOT;
        private bool _isBuiltInDOT;
        private bool _isBuiltInMechanic;

        private bool isEditing;
        private List<string> addedCustomSources = new List<string>();
        private List<string> addedCustomTargets = new List<string>();
        private string customSource;
        private string customTarget;
        private string selectedTarget;
        private string selectedSource;
        private string selectedAudioType;
        private bool useAudio;
        private bool isModifyingVariables;
        private static string _missingTimerValue = "None";
        private string _currentSelectedPlayer;
        private Timer _clause1;
        private Timer _clause2;
        private string currentColorHex = Colors.CornflowerBlue.ToString();


        public event Action<Timer, bool, bool> OnNewTimer = delegate { };
        public event Action<Timer> OnCancelEdit = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool TimerNameInError;
        public SolidColorBrush TimerNameHelpTextColor => TimerNameInError ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.LightGray);
        public string TriggerValueHelpText { get; set; }

        public bool ValueInError = false;
        private string selectedExternalTimerName = _missingTimerValue;
        private bool canBeRefreshed;
        private string effect = "";
        private string customRefreshOption;
        private double _hPPercentageUpper;
        private string hPTriggerText = "Lower Bound";
        private Color selectedColor = Colors.CornflowerBlue;
        private string selectedCancelTimer;
        private string hideUntilTime = "";
        private double hideUntilSeconds;
        private double durationSec;
        private double _absorbValue;
        private bool _showAbsorbOption;
        private bool isCheckingVariable;
        private bool isCheckingSingleValue = true;
        private VariableComparisons selectedComparison;
        private int variableComparisonVal;
        private int variableMinVal;
        private int variableMaxVal;
        private bool showColor = true;
        private bool showDurationOrAlert = true;
        private bool hasTarget;
        private VariableModifications selectedAction = VariableModifications.Add;
        private int variableModificationValue;
        private string selectedModifyVariable;
        private bool includeTimerVisuals = true;

        public SolidColorBrush TriggerValueHelpTextColor => ValueInError ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.LightGray);
        public bool IsMechanicTimer { get; set; }
        public string ParentTimerId { get; set; }
        public bool UseAudio
        {
            get => useAudio; set
            {
                useAudio = value;
                if (string.IsNullOrEmpty(SelectedAudioType))
                    SelectedAudioType = "Built In";
                OnPropertyChanged();
            }
        }
        public List<string> AudioTypes { get; set; } = new List<string> { "Built In" };
        public string SelectedAudioType
        {
            get => selectedAudioType; set
            {
                selectedAudioType = value;
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<Unit,Task> LoadAudioCommand => ReactiveCommand.Create(LoadAudio);

        private async Task LoadAudio()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select an audio file",
                Directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AllowMultiple = false, // If you want to allow selecting multiple files, set this to true
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Wav Files", Extensions = { "wav" } },
                    new FileDialogFilter { Name = "Mp3 Files", Extensions = { "mp3" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                }
            };
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await dialog.ShowAsync(desktop.MainWindow); // Assuming 'obj' is a reference to the parent Window

                if (result != null && result.Length > 0)
                {
                    AudioTypes.Add(result[0]); // Add the first selected file
                    SelectedAudioType = result[0];
                    Settings.WriteSetting<List<string>>("custom_audio_paths", AudioTypes);
                }
            }
        }
        public int CustomAudioPlayTime { get; set; } = 4;

        public bool ShowAbilityOption { get; set; }
        public bool ShowEffectOption { get; set; }
        public bool ShowEffectRefreshOption { get; set; }
        public bool ShowHPOption { get; set; }

        public bool ShowAbsorbOption
        {
            get => _showAbsorbOption;
            set
            {
                _showAbsorbOption = value;
                OnPropertyChanged();
            }
        }

        public bool MultiClauseTrigger { get; set; }


        public string HideUntilTime
        {
            get => hideUntilTime; set
            {
                hideUntilTime = value;
                var isVal = double.TryParse(hideUntilTime, out hideUntilSeconds);
                if (isVal && hideUntilSeconds > DurationSec)
                {
                    HideUntilTime = DurationSec.ToString();
                }
                OnPropertyChanged();
            }
        }
        public bool ShowExternalTriggerOption { get; set; }
        public bool ShowCombatDurationOption { get; set; }
        public List<Timer> AvailableTimersForCharacter { get; set; } = new List<Timer>();
        public List<string> AvailableTimerNames => new List<string> { "None" }.Concat(AvailableTimersForCharacter.Select(t => t.Name)).ToList();
        public string SelectedExternalTimerId { get; set; }
        public string SelectedExternalTimerName
        {
            get => selectedExternalTimerName; set
            {
                selectedExternalTimerName = value;
                if (string.IsNullOrEmpty(selectedExternalTimerName) || selectedExternalTimerName == _missingTimerValue)
                {
                    SelectedExternalTimerId = "";
                }
                else
                {
                    SelectedExternalTimerId = AvailableTimersForCharacter.First(t => t.Name == selectedExternalTimerName).Id;
                }

                OnPropertyChanged();
            }
        }
        public string SelectedCancelTimerId { get; set; }
        public string SelectedCancelTimer
        {
            get => selectedCancelTimer; set
            {
                selectedCancelTimer = value;
                if (string.IsNullOrEmpty(selectedCancelTimer) || selectedCancelTimer == _missingTimerValue)
                {
                    SelectedCancelTimerId = "";
                }
                else
                    SelectedCancelTimerId = AvailableTimersForCharacter.First(t => t.Name == selectedCancelTimer).Id;
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
                OnPropertyChanged("ShowRepeats");
                UpdateUIForTriggerType();
            }
        }

        public bool ResetOnEffectLoss { get; set; }
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
        public string HPTriggerText
        {
            get => hPTriggerText; set
            {
                hPTriggerText = value;
                OnPropertyChanged();
            }
        }
        public double HPPercentage { get; set; }
        public double HPPercentageUpper
        {
            get => _hPPercentageUpper; set
            {
                _hPPercentageUpper = value;
                OnPropertyChanged();
            }
        }

        public double AbsorbValue
        {
            get => _absorbValue;
            set
            {
                _absorbValue = value;
                OnPropertyChanged();
            }
        }

        public double CombatDuration { get; set; }
        public ObservableCollection<string> AvailableSources { get; set; } = new ObservableCollection<string>();
        public string SelectedSource
        {
            get => selectedSource; set
            {
                selectedSource = value;
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
        public ReactiveCommand<object,Unit> SaveSourceCommand => ReactiveCommand.Create<object>(SaveSource);

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
        public bool IsCheckingVariable
        {
            get => isCheckingVariable; set
            {
                isCheckingVariable = value;
                OnPropertyChanged();
            }
        }
        public bool ShowValueComparisons
        {
            get => showValueComparisons; set
            {
                showValueComparisons = value;
                OnPropertyChanged();
            }
        }
        public bool ShowStackVariableOptions
        {
            get => _showStackVariableOptions;
            set
            {
                _showStackVariableOptions = value;
                OnPropertyChanged();
            }
        }
        public bool SetVariableWithCharge
        {
            get => _setVariableWithCharge; set
            {
                _setVariableWithCharge = value;
                OnPropertyChanged();
                OnPropertyChanged("IsModifyingVariables");
                OnPropertyChanged("IncludeTimerVisuals");
            }
        }
        public string ChargeVariable { get; set; }
        public string SourceText { get; set; }
        public ObservableCollection<string> AvailableTargets { get; set; } = new ObservableCollection<string>();
        public string SelectedTarget
        {
            get => selectedTarget; set
            {
                selectedTarget = value;
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
        public bool ShowTargetOnTimerUI { get; set; }

        public bool DisplayTargetToggle => HasTarget;

        public ReactiveCommand<object,Unit> SaveTargetCommand => ReactiveCommand.Create<object>(SaveTarget);

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
        public bool HasTarget
        {
            get => hasTarget; set
            {
                hasTarget = value;
                OnPropertyChanged();
                OnPropertyChanged("DisplayTargetToggle");
            }
        }
        public string TargetText { get; set; }
        public bool CanBeRefreshed
        {
            get => canBeRefreshed; set
            {
                canBeRefreshed = value;
                OnPropertyChanged();
            }
        }
        public bool DontRefresh
        {
            get => dontRefresh; set
            {
                dontRefresh = value;
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
        public ReactiveCommand<object,Unit> SaveRefreshOptionCommand => ReactiveCommand.Create<object>(SaveRefreshCommand);

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
        public bool IsModifyingVariables
        {
            get => isModifyingVariables && !SetVariableWithCharge; set
            {
                isModifyingVariables = value;
                IncludeTimerVisuals = !IsModifyingVariables;
                OnPropertyChanged("ShowColor");
                OnPropertyChanged("ShowDurationOrAlert");
                OnPropertyChanged("DisplayTargetToggle");
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<object,Unit> AddCustomVariableCommand => ReactiveCommand.Create<object>(AddCustomVariable);

        private void AddCustomVariable(object obj)
        {
            if (string.IsNullOrEmpty(AddedVariable) || AvailableVariables.Any(v => v.ToLower() == AddedVariable.ToLower()))
                return;
            OrbsVariableManager.SetVariable(AddedVariable, 0);
            AvailableVariables = OrbsVariableManager.GetVariables();
            SelectedModifyVariable = AddedVariable;
            AddedVariable = "";
            OnPropertyChanged("AvailableVariables");
            OnPropertyChanged("SelectedModifyVariable");
            OnPropertyChanged("AddedVariable");
        }

        public string AddedVariable { get; set; }
        public string SelectedModifyVariable
        {
            get => selectedModifyVariable; set
            {
                selectedModifyVariable = value;
                OnPropertyChanged();
            }
        }
        public List<VariableModifications> AvailableActions => Enum.GetValues<VariableModifications>().ToList();
        public VariableModifications SelectedAction
        {
            get => selectedAction; set
            {
                selectedAction = value;
                OnPropertyChanged();
            }
        }
        public int VariableModificationValue
        {
            get => variableModificationValue; set
            {
                variableModificationValue = value;
                OnPropertyChanged();
            }
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
                    AvailableBosses = EncounterLister.GetBossesForEncounter(SelectedEncounter);
                    AvailableBosses.Insert(0, "All");
                    SetTargetsBasedOnEncouters();
                }
                SelectedBoss = "All";
                OnPropertyChanged("SelectedEncounter");
                OnPropertyChanged("SelectedBoss");
                OnPropertyChanged("AvailableBosses");
            }
        }
        public bool ActiveForStory { get; set; }
        public bool ActiveForVeteran { get; set; }
        public bool ActiveForMaster { get; set; }
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
        public string AlertText { get; set; }
        public double DurationSec
        {
            get => durationSec; set
            {
                durationSec = value;
                if (hideUntilSeconds > durationSec)
                {
                    HideUntilTime = "";
                    hideUntilSeconds = 0;
                }
                OnPropertyChanged();
            }
        }
        public bool ShowDuration => !IsAlert && SelectedTriggerType != TimerKeyType.AbsorbShield;
        public List<string> AvailableVariables { get; set; } = new List<string>();
        public string SelectedVariable { get; set; }
        public List<VariableComparisons> AvailableComparisons => Enum.GetValues<VariableComparisons>().ToList();
        public VariableComparisons SelectedComparison
        {
            get => selectedComparison; set
            {
                selectedComparison = value;
                if (selectedComparison == VariableComparisons.Between)
                    IsCheckingSingleValue = false;
                else
                    IsCheckingSingleValue = true;
            }
        }
        public int VariableComparisonVal
        {
            get => variableComparisonVal; set
            {
                variableComparisonVal = value;
                OnPropertyChanged();
            }
        }
        public int VariableMinVal
        {
            get => variableMinVal; set
            {
                variableMinVal = value;
                OnPropertyChanged();
            }
        }
        public int VariableMaxVal
        {
            get => variableMaxVal; set
            {
                variableMaxVal = value;
                OnPropertyChanged();
            }
        }
        public bool IsCheckingSingleValue
        {
            get => isCheckingSingleValue; set
            {
                isCheckingSingleValue = value;
                OnPropertyChanged();
            }
        }
        public bool IncludeTimerVisuals
        {
            get => includeTimerVisuals && !SetVariableWithCharge; set
            {
                includeTimerVisuals = value;
                OnPropertyChanged();
            }
        }
        public bool ShowColor { get => showColor; set => showColor = value; }
        public bool ShowDurationOrAlert { get => showDurationOrAlert; set => showDurationOrAlert = value; }
        public bool IsPeriodic
        {
            get => isPeriodic; set
            {
                isPeriodic = value;
                if (SelectedTriggerType == TimerKeyType.EntityHP)
                {
                    if (IsPeriodic)
                        HPTriggerText = "Every";
                    else
                        HPTriggerText = "Target";
                }
                OnPropertyChanged("IsPeriodic");
                OnPropertyChanged("ShowRepeats");
            }
        }
        public bool ShowRepeats => IsPeriodic && SelectedTriggerType != TimerKeyType.EntityHP;
        public int Repeats { get; set; }
        public bool IsHot { get; set; }
        public SolidColorBrush ColorPreview => new SolidColorBrush(SelectedColor);
        public Color SelectedColor
        {
            get => selectedColor;
            set
            {
                selectedColor = value;
                currentColorHex = selectedColor.ToString();
                OnPropertyChanged();
                OnPropertyChanged("CurrentColorHex");
                OnPropertyChanged("ColorPreview");
            }
        }
        public string CurrentColorHex
        {
            get => currentColorHex; set
            {
                currentColorHex = value;
                try
                {
                    var newColor = Color.Parse(currentColorHex);
                    if (newColor != SelectedColor)
                        SelectedColor = newColor;
                }
                catch (FormatException e) { };

                OnPropertyChanged();
            }
        }
        public bool IsSubTrigger { get; set; }
        public ReactiveCommand<object,Unit> AddOrEditACommand => ReactiveCommand.Create<object>(AddOrEditA);

        private void AddOrEditA(object obj)
        {
            var vm = new ModifyTimerViewModel(_currentSelectedPlayer);
            vm.Name = "Clause 1";
            vm.ShowColor = false;
            vm.ShowDurationOrAlert = false;
            vm.IsSubTrigger = true;
            vm.ParentTimerId = Id;
            vm.OnNewTimer += (timer, editing, import) =>
            {
                NewClauseTimer(timer, 1);
            };
            if (_clause1 != null)
            {
                vm.Edit(_clause1);
            }
            var window = new TimerModificationWindow(vm);
            window.Show();
        }
        public ReactiveCommand<object,Unit> AddOrEditBCommand => ReactiveCommand.Create<object>(AddOrEditB);

        private void AddOrEditB(object obj)
        {
            var vm = new ModifyTimerViewModel(_currentSelectedPlayer);
            vm.Name = "Clause 2";
            vm.IsSubTrigger = true;
            vm.ShowColor = false;
            vm.ShowDurationOrAlert = false;
            vm.ParentTimerId = Id;
            vm.OnNewTimer += (timer, editing, import) =>
            {
                NewClauseTimer(timer, 2);
            };
            if (_clause2 != null)
            {
                vm.Edit(_clause2);
            }
            var window = new TimerModificationWindow(vm);
            window.Show();
        }

        private void NewClauseTimer(Timer t, int clause)
        {
            if (clause == 1)
                _clause1 = t;
            else
                _clause2 = t;
        }

        public ModifyTimerViewModel(string timerSource = "")
        {
            Id = Guid.NewGuid().ToString();
            _currentSelectedPlayer = timerSource;
            if (timerSource.Contains('|'))
            {
                var parts = timerSource.Split('|');
                AvailableEncounters = new List<string> { parts[0] };
                AvailableBosses = new List<string> { parts[1] };
                selectedEncounter = parts[0];
                SelectedBoss = parts[1];
                IsMechanicTimer = true;
            }
            else
            {
                AvailableEncounters = EncounterLister.SortedEncounterNames;
                AvailableBosses.Insert(0, "All");
                SelectedEncounter = "All";
                SelectedBoss = "All";
                IsMechanicTimer = false;
                IsHot = timerSource == "HOTS";
            }
            var customAudio = Settings.GetListSetting<string>("custom_audio_paths");
            if (customAudio != null && customAudio.Count > 0)
            {
                AudioTypes.AddRange(customAudio.Distinct().ToList());
                AudioTypes = AudioTypes.Distinct().ToList();
            }

            AvailableTimersForCharacter = DefaultOrbsTimersManager.GetDefaults(_currentSelectedPlayer).Timers.Where(t => t.Id != Id).ToList();
            AvailableTriggerTypes = Enum.GetValues<TimerKeyType>().OrderBy(v => v.ToString()).ToList();
            SelectedComparison = AvailableComparisons.First();
            AvailableVariables = OrbsVariableManager.GetVariables();
            if (AvailableVariables.Any())
            {
                SelectedVariable = AvailableVariables.First();
                SelectedModifyVariable = AvailableVariables.First();
            }

            HPPercentageUpper = 5;
            OnPropertyChanged("AvailableTimerNames");
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
            ShowAbsorbOption = false;
            ShowCombatDurationOption = false;
            ShowExternalTriggerOption = false;
            TrackOutsideOfCombat = false;
            HasSource = false;
            HasTarget = false;
            HasCustomTarget = false;
            HasCustomSource = false;
            UseAudio = false;
            Effect = "";
            Ability = "";
            AlertText = "";
            SelectedVariable = "";
            SelectedModifyVariable = "";
            HPPercentage = 0;
            CombatDuration = 0;
            ShowEffectRefreshOption = false;
            MultiClauseTrigger = false;
            ResetOnEffectLoss = false;
            IsCheckingVariable = false;
            ShowValueComparisons = false;
            ShowStackVariableOptions = false;
            if (!IsSubTrigger)
            {
                ShowColor = true;
                ShowDurationOrAlert = true;
            }
            OnPropertyChanged("ResetOnEffectLoss");
            OnPropertyChanged("ShowColor");
            OnPropertyChanged("ShowDurationOrAlert");
            OnPropertyChanged("Effect");
            OnPropertyChanged("Ability");
            OnPropertyChanged("CombatDuration");
            OnPropertyChanged("HPPercentage");
            OnPropertyChanged("HasCustomTarget");
            OnPropertyChanged("HasCustomSource");
            OnPropertyChanged("ShowAbilityOption");
            OnPropertyChanged("ShowEffectRefreshOption");
            OnPropertyChanged("ShowEffectOption");
            OnPropertyChanged("ShowCombatDurationOption");
            OnPropertyChanged("ShowExternalTriggerOption");
            OnPropertyChanged("ShowHPOption");
            OnPropertyChanged("HasSource");
            OnPropertyChanged("HasTarget");
            OnPropertyChanged("UseAudio");
            OnPropertyChanged("HasCustomAudio");
            OnPropertyChanged("TrackOutsideOfCombat");
            OnPropertyChanged("AlertText");
            OnPropertyChanged("MultiClauseTrigger");
            OnPropertyChanged("ShowDuration");
        }
        private int editRev = 0;
        private bool showValueComparisons;
        private bool _showStackVariableOptions;
        private bool _setVariableWithCharge;
        private bool dontRefresh;

        public void Edit(Timer timerToEdit)
        {
            _isBuiltInDCD = timerToEdit.IsBuiltInDefensive;
            _isBuiltInOCD = timerToEdit.IsBuiltInOffensive;
            _isBuiltInDOT = timerToEdit.IsBuiltInDot;
            IsHot = timerToEdit.IsHot;
            _isBuiltInMechanic = timerToEdit.IsMechanic && !timerToEdit.IsUserAddedTimer;

            _editedTimer = timerToEdit;
            isEditing = true;
            Id = string.IsNullOrEmpty(timerToEdit.Id) ? Guid.NewGuid().ToString() : timerToEdit.Id;
            Name = timerToEdit.Name;
            SelectedTriggerType = timerToEdit.TriggerType;
            CombatDuration = timerToEdit.CombatTimeElapsed;
            SelectedColor = timerToEdit.TimerColor;
            IsAlert = timerToEdit.IsAlert;
            AlertText = timerToEdit.AlertText;
            IsPeriodic = timerToEdit.IsPeriodic;
            Repeats = timerToEdit.Repeats;
            Effect = timerToEdit.Effect;
            ResetOnEffectLoss = timerToEdit.ResetOnEffectLoss;
            Ability = timerToEdit.Ability;

            TrackOutsideOfCombat = timerToEdit.TrackOutsideOfCombat;
            if (timerToEdit.TriggerType == TimerKeyType.TimerExpired)
            {
                SelectedExternalTimerId = timerToEdit.ExperiationTimerId;
                SelectedExternalTimerName = string.IsNullOrEmpty(SelectedExternalTimerId) ? _missingTimerValue : (!string.IsNullOrEmpty(SelectedExternalTimerId) && AvailableTimersForCharacter.All(t => t.Id != SelectedExternalTimerId)) ? _missingTimerValue : AvailableTimersForCharacter.First(t => t.Id == SelectedExternalTimerId).Name;
            }
            if (timerToEdit.TriggerType == TimerKeyType.IsTimerTriggered)
            {
                SelectedExternalTimerId = timerToEdit.SeletedTimerIsActiveId;
                SelectedExternalTimerName = string.IsNullOrEmpty(SelectedExternalTimerId) ? _missingTimerValue : (!string.IsNullOrEmpty(SelectedExternalTimerId) && AvailableTimersForCharacter.All(t => t.Id != SelectedExternalTimerId)) ? _missingTimerValue : AvailableTimersForCharacter.First(t => t.Id == SelectedExternalTimerId).Name;
            }

            SelectedCancelTimerId = timerToEdit.SelectedCancelTimerId;
            SelectedCancelTimer = string.IsNullOrEmpty(SelectedCancelTimerId) ? _missingTimerValue : (!string.IsNullOrEmpty(SelectedCancelTimerId) && AvailableTimersForCharacter.All(t => t.Id != SelectedCancelTimerId)) ? _missingTimerValue : AvailableTimersForCharacter.First(t => t.Id == SelectedCancelTimerId).Name;
            DurationSec = timerToEdit.DurationSec;
            AbsorbValue = timerToEdit.AbsorbValue;
            HideUntilTime = timerToEdit.HideUntilSec.ToString();
            HPPercentage = timerToEdit.HPPercentage;
            HPPercentageUpper = timerToEdit.HPPercentageUpper;
            SelectedEncounter = timerToEdit.SpecificEncounter;
            SelectedBoss = timerToEdit.SpecificBoss;
            ActiveForStory = timerToEdit.ActiveForStory;
            ActiveForVeteran = timerToEdit.ActiveForVeteran;
            ActiveForMaster = timerToEdit.ActiveForMaster;
            CanBeRefreshed = timerToEdit.CanBeRefreshed;
            DontRefresh = timerToEdit.DontRefresh;
            ShowTargetOnTimerUI = timerToEdit.ShowTargetOnTimerUI;
            UseAudio = timerToEdit.UseAudio;
            _clause1 = timerToEdit.Clause1;
            SelectedVariable = timerToEdit.VariableName;
            VariableComparisonVal = timerToEdit.ComparisonVal;
            VariableMaxVal = timerToEdit.ComparisonValMax;
            VariableMinVal = timerToEdit.ComparisonValMin;
            SelectedComparison = timerToEdit.ComparisonAction;
            SelectedModifyVariable = timerToEdit.ModifyVariableName;
            VariableModificationValue = timerToEdit.VariableModificationValue;
            SelectedAction = timerToEdit.ModifyVariableAction;
            IsModifyingVariables = timerToEdit.ShouldModifyVariable;
            ChargeVariable = timerToEdit.ChargesSetVariableName;
            SetVariableWithCharge = timerToEdit.ChargesSetVariable;
            if (timerToEdit.TimerRev > 0)
                editRev = timerToEdit.TimerRev;
            if (IsModifyingVariables)
            {
                if (!timerToEdit.UseVisualsAndModify)
                {
                    IncludeTimerVisuals = false;
                }
                else
                {
                    IncludeTimerVisuals = true;
                }
            }
            if (_clause1 != null)
            {
                _clause1.ParentTimerId = Id;
                _clause1.IsSubTimer = true;
            }
            _clause2 = timerToEdit.Clause2;
            if (_clause2 != null)
            {
                _clause2.ParentTimerId = Id;
                _clause2.IsSubTimer = true;
            }
            if (timerToEdit.CustomAudioPath != null)
            {
                if (!AudioTypes.Contains(timerToEdit.CustomAudioPath))
                {
                    AudioTypes.Add(timerToEdit.CustomAudioPath);
                }
                SelectedAudioType = timerToEdit.CustomAudioPath;
            }
            else
            {
                SelectedAudioType = "Built In";
            }
            CustomAudioPlayTime = timerToEdit.AudioStartTime;

            var addedAbilities = timerToEdit.AbilitiesThatRefresh.Select(a => new RefreshOptionViewModel() { Name = a }).ToList();
            addedAbilities.ForEach(a => a.RemoveRequested += RemoveRefreshOption);
            AvailableRefreshOptions = new ObservableCollection<RefreshOptionViewModel>(addedAbilities);
            if (!AvailableTargets.Contains(timerToEdit.Target))
            {
                AvailableTargets.Add(timerToEdit.Target);
                SelectedTarget = timerToEdit.Target;
            }
            else
            {
                SelectedTarget = timerToEdit.Target;
            }
            if (!AvailableSources.Contains(timerToEdit.Source))
            {
                AvailableSources.Add(timerToEdit.Source);
                SelectedSource = timerToEdit.Source;
            }
            else
            {
                SelectedSource = timerToEdit.Source;
            }
            AvailableTimersForCharacter = DefaultOrbsTimersManager.GetDefaults(_currentSelectedPlayer).Timers.Where(t => t.Id != Id).ToList();
            OnPropertyChanged("AvailableTimerNames");
            OnPropertyChanged("ShowTargetOnTimerUI");
            OnPropertyChanged("AvailableRefreshOptions");
            OnPropertyChanged("Name");
            OnPropertyChanged("AlertText");
            OnPropertyChanged("CombatDuration");
            OnPropertyChanged("TrackOutsideOfCombat");
            OnPropertyChanged("SelectedTriggerType");
            OnPropertyChanged("SelectedCancelTimer");
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
            OnPropertyChanged("ActiveForStory");
            OnPropertyChanged("ActiveForVeteran");
            OnPropertyChanged("ActiveForMaster");
            OnPropertyChanged("SelectedTarget");
            OnPropertyChanged("IsHot");
            OnPropertyChanged("UseAudio");
            OnPropertyChanged("SelectedAudioType");
            OnPropertyChanged("CustomAudioPlayTime");
            OnPropertyChanged("ResetOnEffectLoss");
            OnPropertyChanged("SelectedExternalTimerName");
        }
        public void Cancel()
        {
            if (isEditing)
                OnCancelEdit.InvokeSafely(_editedTimer);
        }

        public ReactiveCommand<object,Unit> SaveCommand => ReactiveCommand.Create<object>(Save);

        private void Save(object obj)
        {
            var isValid = Validate();
            if (!isValid || SelectedAudioType == "Custom")
                return;
            var newTimer = new Timer()
            {
                Id = Id,
                SelectedCancelTimerId = SelectedCancelTimerId,
                ParentTimerId = ParentTimerId,
                IsSubTimer = IsSubTrigger,
                TimerSource = _currentSelectedPlayer,
                Name = Name,
                ResetOnEffectLoss = ResetOnEffectLoss,
                TrackOutsideOfCombat = TrackOutsideOfCombat,
                CombatTimeElapsed = CombatDuration,
                IsEnabled = true,
                Source = SelectedSource,
                Target = SelectedTarget,
                HPPercentage = HPPercentage,
                HPPercentageUpper = HPPercentageUpper,
                AbsorbValue = AbsorbValue,
                TriggerType = SelectedTriggerType,
                ExperiationTimerId = SelectedExternalTimerId,
                SeletedTimerIsActiveId = SelectedExternalTimerId,
                Ability = Ability,
                Effect = Effect,
                IsPeriodic = IsPeriodic,
                Repeats = Repeats,
                IsAlert = IsAlert || (DurationSec == 0 && SelectedTriggerType != TimerKeyType.EntityHP && SelectedTriggerType != TimerKeyType.AbsorbShield && IncludeTimerVisuals),
                AlertText = AlertText,
                DurationSec = DurationSec,
                HideUntilSec = hideUntilSeconds,
                TimerColor = IncludeTimerVisuals ? SelectedColor : Colors.Transparent,
                SpecificBoss = SelectedBoss,
                SpecificEncounter = SelectedEncounter,
                ActiveForStory = ActiveForStory,
                ActiveForVeteran = ActiveForVeteran,
                ActiveForMaster = ActiveForMaster,
                CanBeRefreshed = CanBeRefreshed,
                DontRefresh = DontRefresh,
                ShowTargetOnTimerUI = ShowTargetOnTimerUI,
                UseAudio = UseAudio,
                Clause1 = _clause1,
                Clause2 = _clause2,
                CustomAudioPath = selectedAudioType != "Built In" ? selectedAudioType : null,
                AudioStartTime = CustomAudioPlayTime,
                IsMechanic = SelectedBoss != "All",
                AbilitiesThatRefresh = AvailableRefreshOptions.Select(r => r.Name).ToList(),
                VariableName = SelectedVariable,
                ComparisonVal = VariableComparisonVal,
                ComparisonAction = SelectedComparison,
                ComparisonValMax = VariableMaxVal,
                ComparisonValMin = VariableMinVal,
                ModifyVariableAction = SelectedAction,
                ModifyVariableName = SelectedModifyVariable,
                VariableModificationValue = VariableModificationValue,
                ShouldModifyVariable = IsModifyingVariables,
                UseVisualsAndModify = IsModifyingVariables && IncludeTimerVisuals,
                TimerRev = editRev,
                IsUserAddedTimer = _isBuiltInMechanic || _isBuiltInDCD || _isBuiltInDOT || _isBuiltInHOT || _isBuiltInOCD ? false : true,
                IsBuiltInDot = _isBuiltInDOT,
                IsHot = IsHot,
                IsBuiltInDefensive = _isBuiltInDCD,
                IsBuiltInOffensive = _isBuiltInOCD,
                ChargesSetVariable = SetVariableWithCharge,
                ChargesSetVariableName = ChargeVariable
            };

            OnNewTimer.InvokeSafely(newTimer, isEditing, false);
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
            if ((!string.IsNullOrEmpty(SelectedBoss) && SelectedBoss != "All") && !ActiveForStory && !ActiveForVeteran && !ActiveForMaster)
            {
                TimerNameInError = true;
                OnPropertyChanged("TimerNameHelpTextColor");
                return false;
            }
            if (IsModifyingVariables && string.IsNullOrEmpty(SelectedModifyVariable))
            {
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
                case TimerKeyType.NewEntitySpawn:
                case TimerKeyType.EntityDeath:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(SourceText))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TriggerValueHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.AbsorbShield:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(SourceText))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TriggerValueHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(Ability))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TriggerValueHelpTextColor");
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
                case TimerKeyType.DamageTaken:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(Ability))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.HasEffect:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(Effect))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.IsFacing:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(SourceText))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(TargetText))
                        {
                            ValueInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.And:
                case TimerKeyType.Or:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (_clause1 == null || _clause2 == null)
                        {
                            ValueInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.VariableCheck:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(SelectedVariable))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        return true;
                    }
                case TimerKeyType.EffectCharges:
                    {
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(Effect))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            return false;
                        }
                        if (string.IsNullOrEmpty(SelectedSource))
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
                        ShowEffectRefreshOption = true;
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
                        OnPropertyChanged("ShowEffectRefreshOption");
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
                        HasSource = true;
                        HasTarget = true;
                        CanChangeCombatTracking = true;
                        SourceText = "When Applied By";
                        TargetText = "When Lost From";
                        TriggerValueHelpText = "Name or Id";
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("ShowEffectRefreshOption");
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
                        ShowDurationOrAlert = false;
                        OnPropertyChanged("ShowDurationOrAlert");
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
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowExternalTriggerOption");
                        break;
                    }
                case TimerKeyType.FightDuration:
                    {
                        ShowCombatDurationOption = true;
                        CanChangeCombatTracking = false;
                        OnPropertyChanged("TrackOutsideOfCombat");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowCombatDurationOption");
                        break;
                    }
                case TimerKeyType.DamageTaken:
                    {
                        HasTarget = true;
                        HasSource = true;
                        ShowAbilityOption = true;
                        CanChangeCombatTracking = false;
                        TargetText = "When Is Damaged";
                        SourceText = "When Damaged By";
                        TriggerValueHelpText = "Name or Id";
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("ShowAbilityOption");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("TrackOutsideOfCombat");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowCombatDurationOption");
                        break;
                    }
                case TimerKeyType.HasEffect:
                    {
                        HasTarget = true;
                        ShowEffectOption = true;
                        CanChangeCombatTracking = true;
                        TargetText = "When Has Effect";
                        TriggerValueHelpText = "Effect Id";
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("TrackOutsideOfCombat");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowCombatDurationOption");
                        break;
                    }
                case TimerKeyType.IsFacing:
                    {
                        HasTarget = true;
                        HasSource = true;
                        CanChangeCombatTracking = true;
                        TargetText = "When Faced";
                        SourceText = "When Faced By";
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TrackOutsideOfCombat");
                        break;
                    }
                case TimerKeyType.Or:
                case TimerKeyType.And:
                    {
                        CanChangeCombatTracking = true;
                        MultiClauseTrigger = true;
                        OnPropertyChanged("MultiClauseTrigger");
                        OnPropertyChanged("CanChangeCombatTracking");
                        break;
                    }
                case TimerKeyType.NewEntitySpawn:
                    {
                        HasSource = true;
                        CanChangeCombatTracking = false;
                        SourceText = "When Spawned";
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TrackOutsideOfCombat");
                        break;
                    }
                case TimerKeyType.EntityDeath:
                    {
                        HasSource = true;
                        CanChangeCombatTracking = false;
                        SourceText = "When Died";
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("TrackOutsideOfCombat");
                        break;
                    }
                case TimerKeyType.AbsorbShield:
                    {
                        HasSource = true;
                        SourceText = "Entity";
                        ShowAbsorbOption = true;
                        CanChangeCombatTracking = false;
                        ShowAbilityOption = true;
                        ShowDurationOrAlert = false;
                        OnPropertyChanged("ShowDuration");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("ShowDurationOrAlert");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowAbilityOption");
                        break;
                    }
                case TimerKeyType.EffectCharges:
                    {
                        HasSource = true;
                        SourceText = "Source";
                        HasTarget = true;
                        TargetText = "Target";
                        CanChangeCombatTracking = true;
                        ShowEffectOption = true;
                        ShowValueComparisons = true;
                        ShowStackVariableOptions = true;
                        OnPropertyChanged("ShowDuration");
                        OnPropertyChanged("HasSource");
                        OnPropertyChanged("SourceText");
                        OnPropertyChanged("HasTarget");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("CanChangeCombatTracking");
                        OnPropertyChanged("ShowEffectOption");
                        break;
                    }
                case TimerKeyType.VariableCheck:
                    {
                        ShowStackVariableOptions = true;
                        IsCheckingVariable = true;
                        ShowValueComparisons = true;
                        break;
                    }
                case TimerKeyType.IsTimerTriggered:
                    {
                        ShowExternalTriggerOption = true;
                        OnPropertyChanged("ShowExternalTriggerOption");
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
            AvailableSources = new ObservableCollection<string>(Enum.GetNames<TimerTargetType>().Concat(addedCustomSources));
            AvailableTargets = new ObservableCollection<string>(Enum.GetNames<TimerTargetType>().Concat(addedCustomTargets));
            if (SelectedEncounter != "All")
            {
                List<string> targetsInFight;
                if (selectedBoss != "All")
                {
                    targetsInFight = EncounterLister.GetTargetsOfBossFight(SelectedEncounter, SelectedBoss);
                }
                else
                {
                    targetsInFight = EncounterLister.GetAllTargetsForEncounter(SelectedEncounter);
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
