using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseModificationViewModel :ReactiveObject
    {
        private Phase _editingPhase;
        private string name;
        private bool hasValue;
        private string _value;
        private string valuePrompt;
        private string targetText;
        private bool hasTarget;
        private string selectedTarget;
        private string selectedSource;
        private PhaseTrigger seletedPhaseStart;
        private PhaseTrigger selectedPhaseEnd;
        private string endtargetText;
        private bool endhasTarget;
        private string endselectedTarget;
        private string endvaluePrompt;
        private bool endhasValue;
        private string _endvalue;
        private string multiValueOption;
        private string endMultiValueOption;
        private bool hasMultiValue;
        private bool endhasMultiValue;
        private bool hasMultiTarget;
        private string multiTargetOption;
        private bool endhasMultiTarget;
        private string endmultiTargetOption;
        private ObservableCollection<RefreshOptionViewModel> _multiValueOptions = new ObservableCollection<RefreshOptionViewModel>();

        public event Action<Phase> OnCancelEdit = delegate { };

        public event Action<Phase> OnNewPhase = delegate { };
        private string _validationMessage = string.Empty;
        public string ValidationMessage
        {
            get => _validationMessage;
            private set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
        }
        public string EditorTitle => _editingPhase == null ? "New phase" : "Edit phase";
        public string SelectedBoss { get; set; }
        public string SelectedEncounter { get; set; }
        public PhaseModificationViewModel(string source)
        {
            Id = Guid.NewGuid();
            this.selectedSource = source;
            SelectedEncounter = source.Split('|')[0];
            SelectedBoss = source.Split('|')[1];
            SelectedPhaseStart = PhaseTrigger.CombatStart;
        }
        public PhaseTrigger SelectedPhaseStart
        {
            get => seletedPhaseStart; set
            {
                this.RaiseAndSetIfChanged(ref seletedPhaseStart, value);
                SetupUI();
            }
        }
        public PhaseTrigger SelectedPhaseEnd
        {
            get => selectedPhaseEnd; set
            {
                this.RaiseAndSetIfChanged(ref selectedPhaseEnd, value);
                SetupUI();
            }
        }
        public Guid Id { get; set; }
        public string Name
        {
            get => name; set => this.RaiseAndSetIfChanged(ref name, value);
        }
        public bool HasMultiValue
        {
            get => hasMultiValue; set => this.RaiseAndSetIfChanged(ref hasMultiValue, value);
        }

        public ObservableCollection<RefreshOptionViewModel> MultiValueOptions
        {
            get => _multiValueOptions;
            set => this.RaiseAndSetIfChanged(ref _multiValueOptions, value);
        }

        public string MultiValueOption
        {
            get => multiValueOption; set => this.RaiseAndSetIfChanged(ref multiValueOption, value);
        }
        public ReactiveCommand<Unit,Unit> SaveRefreshOptionCommand => ReactiveCommand.Create(SaveRefreshCommand);

        private void SaveRefreshCommand()
        {
            if (string.IsNullOrEmpty(MultiValueOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = MultiValueOption };
            newValueOption.RemoveRequested += RemoveRefreshOption;
            MultiValueOptions.Add(newValueOption);
            MultiValueOption = "";
        }

        private void RemoveRefreshOption(RefreshOptionViewModel obj)
        {
            MultiValueOptions.Remove(obj);
        }
        public bool EndHasMultiValue
        {
            get => endhasMultiValue; set => this.RaiseAndSetIfChanged(ref endhasMultiValue, value);
        }
        public ObservableCollection<RefreshOptionViewModel> EndMultiValueOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string EndMultiValueOption
        {
            get => endMultiValueOption; set => this.RaiseAndSetIfChanged(ref endMultiValueOption, value);
        }
        public ReactiveCommand<Unit,Unit> EndSaveRefreshOptionCommand => ReactiveCommand.Create(EndSaveRefreshCommand);

        private void EndSaveRefreshCommand()
        {
            if (string.IsNullOrEmpty(EndMultiValueOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = EndMultiValueOption };
            newValueOption.RemoveRequested += EndRemoveRefreshOption;
            EndMultiValueOptions.Add(newValueOption);
            EndMultiValueOption = "";
        }

        private void EndRemoveRefreshOption(RefreshOptionViewModel obj)
        {
            EndMultiValueOptions.Remove(obj);
        }
        public bool HasValue
        {
            get => hasValue; set => this.RaiseAndSetIfChanged(ref hasValue, value);
        }
        public string Value
        {
            get => _value; set => this.RaiseAndSetIfChanged(ref _value, value);
        }
        public string ValuePrompt
        {
            get => valuePrompt; set => this.RaiseAndSetIfChanged(ref valuePrompt, value);
        }
        public bool EndHasValue
        {
            get => endhasValue; set => this.RaiseAndSetIfChanged(ref endhasValue, value);
        }
        public string EndValue
        {
            get => _endvalue; set => this.RaiseAndSetIfChanged(ref _endvalue, value);
        }
        public string EndValuePrompt
        {
            get => endvaluePrompt; set => this.RaiseAndSetIfChanged(ref endvaluePrompt, value);
        }


        public bool HasMultiTarget
        {
            get => hasMultiTarget; set => this.RaiseAndSetIfChanged(ref hasMultiTarget, value);
        }
        public ObservableCollection<RefreshOptionViewModel> MultiTargetOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string MultiTargetOption
        {
            get => multiTargetOption; set => this.RaiseAndSetIfChanged(ref multiTargetOption, value);
        }
        public ReactiveCommand<Unit,Unit> SaveMultiTargetOption => ReactiveCommand.Create(SaveMultiTargetCommand);

        private void SaveMultiTargetCommand()
        {
            if (string.IsNullOrEmpty(MultiTargetOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = MultiTargetOption };
            newValueOption.RemoveRequested += RemoveCustomTarget;
            MultiTargetOptions.Add(newValueOption);
            MultiTargetOption = "";
        }

        private void RemoveCustomTarget(RefreshOptionViewModel obj)
        {
            MultiTargetOptions.Remove(obj);
        }


        public string TargetText
        {
            get => targetText; set => this.RaiseAndSetIfChanged(ref targetText, value);
        }
        public bool HasTarget
        {
            get => hasTarget; set => this.RaiseAndSetIfChanged(ref hasTarget, value);
        }
        public string SelectedTarget
        {
            get => selectedTarget; set => this.RaiseAndSetIfChanged(ref selectedTarget, value);
        }


        public bool EndHasMultiTarget
        {
            get => endhasMultiTarget; set => this.RaiseAndSetIfChanged(ref endhasMultiTarget, value);
        }
        public ObservableCollection<RefreshOptionViewModel> EndMultiTargetOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string EndMultiTargetOption
        {
            get => endmultiTargetOption; set => this.RaiseAndSetIfChanged(ref endmultiTargetOption, value);
        }
        public ReactiveCommand<Unit,Unit> EndSaveMultiTargetOption => ReactiveCommand.Create(EndSaveMultiTargetCommand);

        private void EndSaveMultiTargetCommand()
        {
            if (string.IsNullOrEmpty(EndMultiTargetOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = EndMultiTargetOption };
            newValueOption.RemoveRequested += EndRemoveCustomTarget;
            EndMultiTargetOptions.Add(newValueOption);
            EndMultiTargetOption = "";
        }

        private void EndRemoveCustomTarget(RefreshOptionViewModel obj)
        {
            EndMultiTargetOptions.Remove(obj);
        }


        public string EndTargetText
        {
            get => endtargetText; set => this.RaiseAndSetIfChanged(ref endtargetText, value);
        }
        public bool EndHasTarget
        {
            get => endhasTarget; set => this.RaiseAndSetIfChanged(ref endhasTarget, value);
        }
        public string EndSelectedTarget
        {
            get => endselectedTarget; set => this.RaiseAndSetIfChanged(ref endselectedTarget, value);
        }
        public List<PhaseTrigger> AvailablePhaseTypes => Enum.GetValues<PhaseTrigger>().ToList();
        public void Cancel()
        {
            if (_editingPhase != null)
                OnCancelEdit(_editingPhase);
        }
        public ReactiveCommand<Unit,Unit> SaveCommand => ReactiveCommand.Create(Save);
        private void Save()
        {
            ValidationMessage = string.Empty;
            if (!TryBuildArguments(false, out var args) || !TryBuildArguments(true, out var endArgs)) return;
            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationMessage = "Enter a phase name.";
                return;
            }
            OnNewPhase(new Phase()
            {
                StartTrigger = seletedPhaseStart,
                EndTrigger = selectedPhaseEnd,
                Name = Name,
                Id = Id,
                StartArgs = args,
                EndArgs = endArgs,
                PhaseSource = selectedSource
            });
        }

        private bool TryBuildArguments(bool end, out PhaseArgs args)
        {
            args = new PhaseArgs();
            var condition = end ? "end" : "start";
            var targets = end ? EndMultiTargetOptions : MultiTargetOptions;
            var singleTarget = end ? EndSelectedTarget : SelectedTarget;
            var targetTexts = (end ? EndHasMultiTarget : HasMultiTarget)
                ? targets.Select(target => target.Name)
                : (end ? EndHasTarget : HasTarget) && !string.IsNullOrEmpty(singleTarget)
                    ? new[] { singleTarget }
                    : Enumerable.Empty<string>();
            foreach (var target in targetTexts)
            {
                if (!long.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entityId))
                {
                    ValidationMessage = $"Enter valid entity IDs for the {condition} condition.";
                    return false;
                }
                args.EntityIds.Add(entityId);
            }
            if (end ? EndHasMultiValue : HasMultiValue)
            {
                var values = (end ? EndMultiValueOptions : MultiValueOptions).Select(value => value.Name).ToList();
                args.AbilityIds = values;
                args.EffectIds = values;
            }
            if (end ? EndHasValue : HasValue)
            {
                var text = end ? EndValue : Value;
                var number = 0d;
                if (!string.IsNullOrEmpty(text) &&
                    (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number) || !double.IsFinite(number)))
                {
                    ValidationMessage = $"Enter a valid number for the {condition} condition.";
                    return false;
                }
                args.HPPercentage = number;
                args.CombatDuration = number;
            }
            return true;
        }
        internal void Edit(Phase phaseEdited)
        {
            _editingPhase = phaseEdited;
            this.RaisePropertyChanged(nameof(EditorTitle));
            Id = Guid.Empty != phaseEdited.Id ? phaseEdited.Id : Id;
            Name = phaseEdited.Name;
            SelectedPhaseStart = phaseEdited.StartTrigger;
            SelectedPhaseEnd = phaseEdited.EndTrigger;
            switch (phaseEdited.StartTrigger)
            {
                case PhaseTrigger.EntitySpawn:
                case PhaseTrigger.EntityDeath:
                case PhaseTrigger.EntityHP:
                    {
                        MultiTargetOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.StartArgs.EntityIds.Select(e => new RefreshOptionViewModel { Name = e.ToString() }));
                        MultiTargetOptions.ToList().ForEach(t => t.RemoveRequested += RemoveCustomTarget);
                        this.RaisePropertyChanged("MultiTargetOptions");
                        if (phaseEdited.StartTrigger == PhaseTrigger.EntityHP)
                            Value = phaseEdited.StartArgs.HPPercentage.ToString(CultureInfo.InvariantCulture);
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        SelectedTarget = phaseEdited.StartArgs.EntityIds.Any() ? phaseEdited.StartArgs.EntityIds.First().ToString() : "";
                        MultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.StartArgs.EffectIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        MultiValueOptions.ToList().ForEach(t => t.RemoveRequested += RemoveRefreshOption);
                        this.RaisePropertyChanged("MultiValueOptions");
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        SelectedTarget = phaseEdited.StartArgs.EntityIds.Any() ? phaseEdited.StartArgs.EntityIds.First().ToString() : "";
                        MultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.StartArgs.AbilityIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        MultiValueOptions.ToList().ForEach(t => t.RemoveRequested += RemoveRefreshOption);
                        this.RaisePropertyChanged("MultiValueOptions");
                        break;
                    }
                case PhaseTrigger.CombatDuration:
                    {
                        Value = phaseEdited.StartArgs.CombatDuration.ToString();
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            switch (phaseEdited.EndTrigger)
            {
                case PhaseTrigger.EntitySpawn:
                case PhaseTrigger.EntityDeath:
                case PhaseTrigger.EntityHP:
                    {
                        EndMultiTargetOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.EndArgs.EntityIds.Select(e => new RefreshOptionViewModel { Name = e.ToString() }));
                        EndMultiTargetOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveCustomTarget);
                        this.RaisePropertyChanged("EndMultiTargetOptions");
                        if (phaseEdited.EndTrigger == PhaseTrigger.EntityHP)
                            EndValue = phaseEdited.EndArgs.HPPercentage.ToString(CultureInfo.InvariantCulture);
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        EndSelectedTarget = phaseEdited.EndArgs.EntityIds.Any() ? phaseEdited.EndArgs.EntityIds.First().ToString() : "";
                        EndMultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.EndArgs.EffectIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        EndMultiValueOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveRefreshOption);
                        this.RaisePropertyChanged("EndMultiValueOptions");
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        EndSelectedTarget = phaseEdited.EndArgs.EntityIds.Any() ? phaseEdited.EndArgs.EntityIds.First().ToString() : "";
                        EndMultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.EndArgs.AbilityIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        EndMultiValueOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveRefreshOption);
                        this.RaisePropertyChanged("EndMultiValueOptions");
                        break;
                    }
                case PhaseTrigger.CombatDuration:
                    {
                        EndValue = phaseEdited.EndArgs.CombatDuration.ToString();
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        private void Reset()
        {
            EndHasTarget = false;
            EndHasValue = false;
            EndValuePrompt = string.Empty;
            EndHasMultiValue = false;
            EndHasMultiTarget = false;

            HasMultiTarget = false;
            HasMultiValue = false;
            HasValue = false;
            HasTarget = false;
            TargetText = string.Empty;
            ValuePrompt = string.Empty;
        }
        private void SetupUI()
        {
            Reset();
            switch (SelectedPhaseStart)
            {
                case PhaseTrigger.EntitySpawn:
                case PhaseTrigger.EntityDeath:
                    {
                        HasMultiTarget = true;
                        TargetText = "Entity IDs";
                        break;
                    }
                case PhaseTrigger.EntityHP:
                    {
                        HasValue = true;
                        ValuePrompt = "Health (%)";
                        HasMultiTarget = true;
                        TargetText = "Entity IDs";
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        HasMultiValue = true;
                        ValuePrompt = "Effects (names or IDs)";
                        HasTarget = true;
                        TargetText = "Affected entity ID (optional)";
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        HasMultiValue = true;
                        ValuePrompt = "Abilities (names or IDs)";
                        HasTarget = true;
                        TargetText = "Caster entity ID (optional)";
                        break;
                    }
                case PhaseTrigger.CombatDuration:
                    {
                        HasValue = true;
                        ValuePrompt = "Seconds into combat";
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            switch (SelectedPhaseEnd)
            {
                case PhaseTrigger.EntitySpawn:
                case PhaseTrigger.EntityDeath:
                    {
                        EndHasMultiTarget = true;
                        EndTargetText = "Entity IDs";
                        break;
                    }
                case PhaseTrigger.EntityHP:
                    {
                        EndHasValue = true;
                        EndValuePrompt = "Health (%)";
                        EndHasMultiTarget = true;
                        EndTargetText = "Entity IDs";
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        EndHasMultiValue = true;
                        EndValuePrompt = "Effects (names or IDs)";
                        EndHasTarget = true;
                        EndTargetText = "Affected entity ID (optional)";
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        EndHasMultiValue = true;
                        EndValuePrompt = "Abilities (names or IDs)";
                        EndHasTarget = true;
                        EndTargetText = "Caster entity ID (optional)";
                        break;
                    }
                case PhaseTrigger.CombatDuration:
                    {
                        EndHasValue = true;
                        EndValuePrompt = "Seconds into phase";
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
    }
}
