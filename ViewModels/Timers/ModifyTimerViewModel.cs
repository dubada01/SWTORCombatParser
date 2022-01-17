﻿using SWTORCombatParser.DataStructures;
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
        private bool isEditing;
        private List<string> defaultSourceTargets = new List<string> { "Any", "Ignore", "Local Player", "Custom" };
        private List<string> addedCustomSources = new List<string>();
        private List<string> addedCustomTargets = new List<string>();
        private List<BossInfo> _bossInfosForEncounter = new List<BossInfo>();
        private string customSource;
        private string customTarget;
        private string selectedTarget;
        private string selectedSource;

        public event Action<Timer, bool> OnNewTimer = delegate { };
        public event Action OnCancelEdit = delegate { };
        public bool TargetIsLocal;
        public bool SourceIsLocal;


        public event PropertyChangedEventHandler PropertyChanged;

        public bool TimerNameInError = false;
        public SolidColorBrush TimerNameHelpTextColor => TimerNameInError ? Brushes.Red : Brushes.LightGray;
        public string TriggerValueHelpText { get; set; }

        public bool ValueInError = false;
        public SolidColorBrush TriggerValueHelpTextColor => ValueInError ? Brushes.Red : Brushes.LightGray;

        public bool ShowAbilityOption { get; set; }
        public bool ShowEffectOption { get; set; }
        public bool ShowHPOption { get; set; }
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
        public string Ability { get; set; } = "";
        public string Effect { get; set; } = "";
        public double HPPercentage { get; set; }
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

        public ModifyTimerViewModel()
        {
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
            HasSource = false;
            HasTarget = false;
            HasCustomTarget = false;
            HasCustomSource = false;
            Effect = "";
            Ability = "";
            HPPercentage = 0;
            OnPropertyChanged("Effect");
            OnPropertyChanged("Ability");
            OnPropertyChanged("HPPercentage");
            OnPropertyChanged("HasCustomTarget");
            OnPropertyChanged("HasCustomSource");
            OnPropertyChanged("ShowAbilityOption");
            OnPropertyChanged("ShowEffectOption");
            OnPropertyChanged("ShowHPOption");
            OnPropertyChanged("HasSource");
            OnPropertyChanged("HasTarget");
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
            var isValid = Validate();
            if (!isValid)
                return;
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
                IsAlert = IsAlert || DurationSec == 0,
                DurationSec = DurationSec,
                TimerColor = SelectedColor,
                SpecificBoss = SelectedBoss,
                SpecificEncounter = SelectedEncounter

            };
            OnNewTimer(newTimer, isEditing);
        }
        private bool Validate()
        {
            TimerNameInError = false;
            ValueInError = false;
            OnPropertyChanged("TimerNameHelpTextColor");
            OnPropertyChanged("TriggerValueHelpTextColor");
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
                case TimerKeyType.AbilityUsed:
                    {
                        var isValid = true;
                        if (string.IsNullOrEmpty(Name))
                        {
                            TimerNameInError = true;
                            OnPropertyChanged("TimerNameHelpTextColor");
                            isValid= false;
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
                        break;
                    }
                case TimerKeyType.AbilityUsed:
                    {
                        ShowAbilityOption = true;
                        HasSource = true;
                        HasTarget = true;
                        SourceText = "When Used By";
                        TargetText = "When Used On";
                        TriggerValueHelpText = "Name or Id";
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
                        ShowEffectOption = true;
                        HasSource = true;
                        HasTarget = true;
                        SourceText = "When Applied By";
                        TargetText = "When Applied To";
                        TriggerValueHelpText = "Name or Id";
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
                        TargetText = "When Lost By";
                        TriggerValueHelpText = "Name or Id";
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
                        OnPropertyChanged("TriggerValueHelpText");
                        OnPropertyChanged("TargetText");
                        OnPropertyChanged("ShowHPOption");
                        OnPropertyChanged("HasTarget");
                        InitSourceAndTargetValues();
                        break;
                    }
            }
        }
        private void InitSourceAndTargetValues()
        {
            SetTargetsBasedOnEncouters();
            SelectedSource = "Any";
            SelectedTarget = "Ignore";
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
