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
        private string selectedSource;
        private bool isEditing;
        private List<string> defaultSourceTargets = new List<string> { "Any", "Ignore", "Local Player", "Custom" };

        public event Action<Timer,bool> OnNewTimer = delegate { };
        public event Action OnCancelEdit = delegate { };
        public bool TargetIsLocal;
        public bool SourceIsLocal;
        private string selectedTarget;

        public event PropertyChangedEventHandler PropertyChanged;


        public bool ShowAbilityOption { get; set; }
        public bool ShowEffectOption { get; set; }
        public bool ShowHPOption { get; set; }
        public bool HasSourceAndTarget { get; set; }
        public string Name { get; set; }
        public List<TimerKeyType> AvailableTriggerTypes { get; set; } = new List<TimerKeyType>();
        public TimerKeyType SelectedTriggerType
        {
            get => selectedTriggerType; set

            {
                selectedTriggerType = value;
                UpdateUIForTriggerType();
            }
        }
        public string Ability { get; set; }
        public string Effect { get; set; }
        public double HPPercentage { get; set; }
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
        public string CustomSource { get; set; }
        public bool HasCustomSource { get; set; }
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
        public string CustomTarget { get; set; }
        public bool HasCustomTarget { get; set; }
        public List<string> AvailableEncounters { get; set; } = new List<string>();
        public string SelectedEncounter
        {
            get => selectedEncounter; set
            {
                if (string.IsNullOrEmpty(value))
                    return;
                selectedEncounter = value;
                if (selectedEncounter == "All")
                {
                    AvailableBosses = new List<string>();
                    AvailableBosses.Insert(0, "All");
                    SelectedBoss = "All";
                }
                else
                {
                    AvailableBosses = _encounters.First(e => e.Name == selectedEncounter).BossNames;
                    AvailableSources = new ObservableCollection<string>(defaultSourceTargets);
                    AvailableTargets = new ObservableCollection<string>(defaultSourceTargets);
                    foreach(var boss in AvailableBosses)
                    {
                        AvailableSources.Add(boss);
                        AvailableTargets.Add(boss);
                    }
                    OnPropertyChanged("AvailableSources");
                    OnPropertyChanged("AvailableTargets");
                }
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

        public Color SelectedColor { get; set; }

        public ModifyTimerViewModel()
        {
            AvailableTriggerTypes = Enum.GetValues<TimerKeyType>().ToList();
            _encounters = RaidNameLoader.SupportedEncounters;
            AvailableEncounters = _encounters.Select(c => c.Name).ToList();
            AvailableEncounters.Insert(0, "All");
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
            HasSourceAndTarget = false;
            HasCustomTarget = false;
            HasCustomSource = false;
            OnPropertyChanged("HasCustomTarget");
            OnPropertyChanged("HasCustomSource");
            OnPropertyChanged("ShowAbilityOption");
            OnPropertyChanged("ShowEffectOption");
            OnPropertyChanged("ShowHPOption");
            OnPropertyChanged("HasSourceAndTarget");
        }

        public void Edit(Timer timerToEdit)
        {
            isEditing = true;
            Name = timerToEdit.Name;
            SelectedTriggerType = timerToEdit.TriggerType;
            SelectedColor = timerToEdit.TimerColor;
            IsAlert = timerToEdit.IsAlert;
            IsPeriodic = timerToEdit.IsPeriodic;
            Effect = timerToEdit.Effect;
            Ability = timerToEdit.Ability;
            DurationSec = timerToEdit.DurationSec;
            HPPercentage = timerToEdit.HPPercentage;
            SelectedEncounter = timerToEdit.SpecificEncounter;
            SelectedBoss = timerToEdit.SpecificBoss;
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
            OnPropertyChanged("Name");
            OnPropertyChanged("SelectedTriggerType");
            OnPropertyChanged("SelectedColor");
            OnPropertyChanged("IsAlert");
            OnPropertyChanged("IsPeriodic");
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
            var newTimer = new Timer()
            {
                Name = Name,
                IsEnabled = true,
                Source = SelectedSource,
                SourceIsLocal = SourceIsLocal,
                Target = SelectedTarget,
                TargetIsLocal = TargetIsLocal,
                HPPercentage = HPPercentage,
                TriggerType = SelectedTriggerType,
                Ability = Ability,
                Effect = Effect,
                IsPeriodic = IsPeriodic,
                IsAlert = IsAlert,
                DurationSec = DurationSec,
                TimerColor = SelectedColor,
                SpecificBoss = SelectedBoss,
                SpecificEncounter = SelectedEncounter

            };
            OnNewTimer(newTimer,isEditing);
        }

        private void UpdateUIForTriggerType()
        {
            ResetUI();
            switch (selectedTriggerType)
            {
                case TimerKeyType.CombatStart:
                    {
                        break;
                    }
                case TimerKeyType.AbilityUsed:
                    {
                        ShowAbilityOption = true;
                        HasSourceAndTarget = true;
                        OnPropertyChanged("ShowAbilityOption");
                        OnPropertyChanged("HasSourceAndTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.EffectGained:
                    {
                        ShowEffectOption = true;
                        HasSourceAndTarget = true;
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("HasSourceAndTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.EffectLost:
                    {
                        ShowEffectOption = true;
                        HasSourceAndTarget = true;
                        OnPropertyChanged("ShowEffectOption");
                        OnPropertyChanged("HasSourceAndTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
                case TimerKeyType.EntityHP:
                    {
                        ShowHPOption = true;
                        HasSourceAndTarget = true;
                        OnPropertyChanged("ShowHPOption");
                        OnPropertyChanged("HasSourceAndTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
            }
        }
        private void InitSourceAndTargetValues()
        {
            AvailableSources = new ObservableCollection<string>(defaultSourceTargets);
            AvailableTargets = new ObservableCollection<string>(defaultSourceTargets);
            if (SelectedEncounter != "All")
            {
                foreach (var boss in AvailableBosses)
                {
                    AvailableSources.Add(boss);
                    AvailableTargets.Add(boss);
                }
            }
            SelectedSource = "Any";
            SelectedTarget = "Ignore";
            OnPropertyChanged("AvailableSources");
            OnPropertyChanged("SelectedSource");
            OnPropertyChanged("AvailableTargets");
            OnPropertyChanged("SelectedTarget");
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
