﻿using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseModificationViewModel :ReactiveObject, INotifyPropertyChanged
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
        private bool hasMultiValue;
        private bool endhasMultiValue;
        private bool hasMultiTarget;
        private string multiTargetOption;
        private bool endhasMultiTarget;
        private string endmultiTargetOption;

        public event Action<Phase> OnCancelEdit = delegate { };

        public event Action<Phase> OnNewPhase = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public string SelectedBoss { get; set; }
        public string SelectedEncounter { get; set; }
        public PhaseModificationViewModel(string source)
        {
            Id = Guid.NewGuid();
            this.selectedSource = source;
            SelectedEncounter = source.Split('|')[0];
            SelectedBoss = source.Split('|')[1];
        }
        public PhaseTrigger SelectedPhaseStart
        {
            get => seletedPhaseStart; set
            {
                seletedPhaseStart = value;
                SetupUI();
                OnPropertyChanged();
            }
        }
        public PhaseTrigger SelectedPhaseEnd
        {
            get => selectedPhaseEnd; set
            {
                selectedPhaseEnd = value;
                SetupUI();
                OnPropertyChanged();
            }
        }
        public Guid Id { get; set; }
        public string Name
        {
            get => name; set
            {
                name = value;
                OnPropertyChanged();
            }
        }
        public bool HasMultiValue
        {
            get => hasMultiValue; set
            {
                hasMultiValue = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<RefreshOptionViewModel> MultiValueOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string MultiValueOption
        {
            get => multiValueOption; set
            {
                multiValueOption = value;
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<Unit,Unit> SaveRefreshOptionCommand => ReactiveCommand.Create(SaveRefreshCommand);

        private void SaveRefreshCommand()
        {
            if (string.IsNullOrEmpty(MultiValueOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = MultiValueOption };
            newValueOption.RemoveRequested += RemoveRefreshOption;
            MultiValueOptions.Add(newValueOption);
            OnPropertyChanged("MultiValueOptions");
            MultiValueOption = "";
        }

        private void RemoveRefreshOption(RefreshOptionViewModel obj)
        {
            MultiValueOptions.Remove(obj);
        }
        public bool EndHasMultiValue
        {
            get => endhasMultiValue; set
            {
                endhasMultiValue = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<RefreshOptionViewModel> EndMultiValueOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string EndMultiValueOption
        {
            get => multiValueOption; set
            {
                multiValueOption = value;
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<Unit,Unit> EndSaveRefreshOptionCommand => ReactiveCommand.Create(EndSaveRefreshCommand);

        private void EndSaveRefreshCommand()
        {
            if (string.IsNullOrEmpty(EndMultiValueOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = EndMultiValueOption };
            newValueOption.RemoveRequested += EndRemoveRefreshOption;
            EndMultiValueOptions.Add(newValueOption);
            OnPropertyChanged("EndMultiValueOption");
            EndMultiValueOption = "";
        }

        private void EndRemoveRefreshOption(RefreshOptionViewModel obj)
        {
            EndMultiValueOptions.Remove(obj);
        }
        public bool HasValue
        {
            get => hasValue; set
            {
                hasValue = value;
                OnPropertyChanged();
            }
        }
        public string Value
        {
            get => _value; set
            {
                _value = value;
                OnPropertyChanged();
            }
        }
        public string ValuePrompt
        {
            get => endvaluePrompt; set
            {
                endvaluePrompt = value;
                OnPropertyChanged();
            }

        }
        public bool EndHasValue
        {
            get => endhasValue; set
            {
                endhasValue = value;
                OnPropertyChanged();
            }
        }
        public string EndValue
        {
            get => _endvalue; set
            {
                _endvalue = value;
                OnPropertyChanged();
            }
        }
        public string EndValuePrompt
        {
            get => endvaluePrompt; set
            {
                endvaluePrompt = value;
                OnPropertyChanged();
            }

        }


        public bool HasMultiTarget
        {
            get => hasMultiTarget; set
            {
                hasMultiTarget = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<RefreshOptionViewModel> MultiTargetOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string MultiTargetOption
        {
            get => multiTargetOption; set
            {
                multiTargetOption = value;
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<Unit,Unit> SaveMultiTargetOption => ReactiveCommand.Create(SaveMultiTargetCommand);

        private void SaveMultiTargetCommand()
        {
            if (string.IsNullOrEmpty(MultiTargetOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = MultiTargetOption };
            newValueOption.RemoveRequested += RemoveCustomTarget;
            MultiTargetOptions.Add(newValueOption);
            OnPropertyChanged("MultiTargetOptions");
            MultiTargetOption = "";
        }

        private void RemoveCustomTarget(RefreshOptionViewModel obj)
        {
            MultiTargetOptions.Remove(obj);
        }


        public string TargetText
        {
            get => targetText; set
            {
                targetText = value;
                OnPropertyChanged();
            }
        }
        public bool HasTarget
        {
            get => hasTarget; set
            {
                hasTarget = value;
                OnPropertyChanged();
            }
        }
        public string SelectedTarget
        {
            get => selectedTarget; set
            {
                selectedTarget = value;
                OnPropertyChanged();
            }
        }


        public bool EndHasMultiTarget
        {
            get => endhasMultiTarget; set
            {
                endhasMultiTarget = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<RefreshOptionViewModel> EndMultiTargetOptions { get; set; } = new ObservableCollection<RefreshOptionViewModel>();

        public string EndMultiTargetOption
        {
            get => endmultiTargetOption; set
            {
                endmultiTargetOption = value;
                OnPropertyChanged();
            }
        }
        public ReactiveCommand<Unit,Unit> EndSaveMultiTargetOption => ReactiveCommand.Create(EndSaveMultiTargetCommand);

        private void EndSaveMultiTargetCommand()
        {
            if (string.IsNullOrEmpty(EndMultiTargetOption))
                return;
            var newValueOption = new RefreshOptionViewModel() { Name = EndMultiTargetOption };
            newValueOption.RemoveRequested += EndRemoveCustomTarget;
            EndMultiTargetOptions.Add(newValueOption);
            OnPropertyChanged("EndMultiTargetOptions");
            EndMultiTargetOption = "";
        }

        private void EndRemoveCustomTarget(RefreshOptionViewModel obj)
        {
            EndMultiTargetOptions.Remove(obj);
        }


        public string EndTargetText
        {
            get => endtargetText; set
            {
                endtargetText = value;
                OnPropertyChanged();
            }
        }
        public bool EndHasTarget
        {
            get => endhasTarget; set
            {
                endhasTarget = value;
                OnPropertyChanged();
            }
        }
        public string EndSelectedTarget
        {
            get => endselectedTarget; set
            {
                endselectedTarget = value;
                OnPropertyChanged();
            }
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
            var args = new PhaseArgs
            {
                EntityIds = MultiTargetOptions.Any() ? MultiTargetOptions.Select(o => long.Parse(o.Name)).ToList() : (!string.IsNullOrEmpty(SelectedTarget) ? new List<long> { long.Parse(SelectedTarget) } : new List<long>()),
                AbilityIds = MultiValueOptions.Select(t => t.Name).ToList(),
                EffectIds = MultiValueOptions.Select(t => t.Name).ToList(),
                HPPercentage = !string.IsNullOrEmpty(Value) ? double.Parse(Value) : 0,
                CombatDuration = !string.IsNullOrEmpty(Value) ? double.Parse(Value) : 0
            };
            var endArgs = new PhaseArgs
            {
                EntityIds = EndMultiTargetOptions.Any() ? EndMultiTargetOptions.Select(o => long.Parse(o.Name)).ToList() : (!string.IsNullOrEmpty(EndSelectedTarget) ? new List<long> { long.Parse(EndSelectedTarget) } : new List<long>()),
                AbilityIds = EndMultiValueOptions.Select(t => t.Name).ToList(),
                EffectIds = EndMultiValueOptions.Select(t => t.Name).ToList(),
                HPPercentage = !string.IsNullOrEmpty(EndValue) ? double.Parse(EndValue) : 0,
                CombatDuration = !string.IsNullOrEmpty(EndValue) ? double.Parse(EndValue) : 0
            };
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
        internal void Edit(Phase phaseEdited)
        {
            _editingPhase = phaseEdited;
            Id = Guid.Empty != phaseEdited.Id ? phaseEdited.Id : Id;
            Name = phaseEdited.Name;
            SelectedPhaseStart = phaseEdited.StartTrigger;
            SelectedPhaseEnd = phaseEdited.EndTrigger;
            switch (phaseEdited.StartTrigger)
            {
                case PhaseTrigger.EntitySpawn:
                case PhaseTrigger.EntityDeath:
                    {
                        MultiTargetOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.StartArgs.EntityIds.Select(e => new RefreshOptionViewModel { Name = e.ToString() }));
                        MultiTargetOptions.ToList().ForEach(t => t.RemoveRequested += RemoveCustomTarget);
                        break;
                    }
                case PhaseTrigger.EntityHP:
                    {
                        SelectedTarget = phaseEdited.StartArgs.EntityIds.Any() ? phaseEdited.StartArgs.EntityIds.First().ToString() : "";
                        Value = phaseEdited.StartArgs.HPPercentage.ToString();
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        SelectedTarget = phaseEdited.StartArgs.EntityIds.Any() ? phaseEdited.StartArgs.EntityIds.First().ToString() : "";
                        MultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.StartArgs.EffectIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        MultiValueOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveRefreshOption);
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        SelectedTarget = phaseEdited.StartArgs.EntityIds.Any() ? phaseEdited.StartArgs.EntityIds.First().ToString() : "";
                        MultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.StartArgs.AbilityIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        MultiValueOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveRefreshOption);
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
                    {
                        EndMultiTargetOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.EndArgs.EntityIds.Select(e => new RefreshOptionViewModel { Name = e.ToString() }));
                        EndMultiTargetOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveCustomTarget);
                        break;
                    }
                case PhaseTrigger.EntityHP:
                    {
                        EndSelectedTarget = phaseEdited.EndArgs.EntityIds.Any() ? phaseEdited.EndArgs.EntityIds.First().ToString() : "";
                        EndValue = phaseEdited.EndArgs.HPPercentage.ToString();
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        EndSelectedTarget = phaseEdited.EndArgs.EntityIds.Any() ? phaseEdited.EndArgs.EntityIds.First().ToString() : "";
                        EndMultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.EndArgs.EffectIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        EndMultiValueOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveRefreshOption);
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        EndSelectedTarget = phaseEdited.EndArgs.EntityIds.Any() ? phaseEdited.EndArgs.EntityIds.First().ToString() : "";
                        EndMultiValueOptions = new ObservableCollection<RefreshOptionViewModel>(phaseEdited.EndArgs.AbilityIds.Select(e => new RefreshOptionViewModel { Name = e }));
                        EndMultiValueOptions.ToList().ForEach(t => t.RemoveRequested += EndRemoveRefreshOption);
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
            EndValue = string.Empty;
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
                        TargetText = "Entity Ids";
                        break;
                    }
                case PhaseTrigger.EntityHP:
                    {
                        HasValue = true;
                        ValuePrompt = "At HP %: ";
                        HasMultiTarget = true;
                        TargetText = "Entity Ids: ";
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        HasMultiValue = true;
                        ValuePrompt = "Effect Ids";
                        HasTarget = true;
                        TargetText = "Entity Id: ";
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        HasMultiValue = true;
                        ValuePrompt = "Ability Is";
                        HasTarget = true;
                        TargetText = "Entity Id: ";
                        break;
                    }
                case PhaseTrigger.CombatDuration:
                    {
                        HasValue = true;
                        ValuePrompt = "Combat Duration: ";
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
                        EndTargetText = "Entity Ids";
                        break;
                    }
                case PhaseTrigger.EntityHP:
                    {
                        EndHasValue = true;
                        EndValuePrompt = "At HP %: ";
                        EndHasMultiTarget = true;
                        EndTargetText = "Entity Ids: ";
                        break;
                    }
                case PhaseTrigger.EffectGain:
                case PhaseTrigger.EffectLoss:
                    {
                        EndHasMultiValue = true;
                        EndValuePrompt = "Effect Ids";
                        EndHasTarget = true;
                        EndTargetText = "Entity Id: ";
                        break;
                    }
                case PhaseTrigger.AbilityUsage:
                case PhaseTrigger.AbilityCancel:
                    {
                        EndHasMultiValue = true;
                        EndValuePrompt = "Ability Ids";
                        EndHasTarget = true;
                        EndTargetText = "Entity Id: ";
                        break;
                    }
                case PhaseTrigger.CombatDuration:
                    {
                        EndHasValue = true;
                        EndValuePrompt = "Combat Duration: ";
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
