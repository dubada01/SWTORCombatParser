using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeModificationViewModel : ReactiveObject,INotifyPropertyChanged
    {
        private string selectedSource;
        private ChallengeType selectedChallengeType;
        private Challenge _editedChallenge;
        private bool _isEditing;
        private string name;
        private string _value;
        private string valuePrompt;
        private bool hasValue;
        private Color selectedColor;
        private string currentColorHex;
        private string targetText;
        private bool hasTarget;
        private string selectedTarget;
        private string sourceText;
        private bool hasSource;
        private string selectedSource1;
        private bool useRawValues;
        private bool canBeRate;
        private bool useMaxValue;
        private bool canHaveMax;

        private List<Phase> availablePhases = new List<Phase>();
        private Phase selectedPhase;
        private bool hasPhases;
        private List<string> availableMetrics;
        private string selectedMetricName;

        public event Action<Challenge, bool> OnNewChallenge = delegate { };
        public event Action<Challenge> OnCancelEdit = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool CanBeRate
        {
            get => canBeRate; set
            {
                canBeRate = value;
                OnPropertyChanged();
            }
        }
        public bool CanHaveMax
        {
            get => canHaveMax; set
            {
                canHaveMax = value;
                OnPropertyChanged();
            }
        }
        public string Name
        {
            get => name; set
            {
                name = value;
                OnPropertyChanged();
            }
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

        public string SourceText
        {
            get => sourceText; set
            {
                sourceText = value;
                OnPropertyChanged();
            }
        }
        public bool HasSource
        {
            get => hasSource; set
            {
                hasSource = value;
                OnPropertyChanged();
            }
        }
        public string SelectedSource
        {
            get => selectedSource1; set
            {
                selectedSource1 = value;
                OnPropertyChanged();
            }
        }
        public bool UseMaxValue
        {
            get => useMaxValue; set
            {
                useMaxValue = value;
                OnPropertyChanged();
            }
        }
        public bool UseRawValue
        {
            get => useRawValues; set
            {
                useRawValues = value;
                OnPropertyChanged();
            }
        }
        public bool HasPhases
        {
            get => hasPhases; set
            {
                hasPhases = value;
                OnPropertyChanged();
            }
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
            get => valuePrompt; set
            {
                valuePrompt = value;
                OnPropertyChanged();
            }

        }
        public ChallengeType SelectedChallengeType
        {
            get => selectedChallengeType; set
            {
                selectedChallengeType = value;
                SetupUI();
                OnPropertyChanged();
            }
        }
        public List<string> AvailableMetrics
        {
            get => availableMetrics; set
            {
                availableMetrics = value;
                OnPropertyChanged();
            }
        }
        public string SelectedMetric
        {
            get => selectedMetricName; set
            {
                selectedMetricName = value;
                OnPropertyChanged();
            }
        }
        public OverlayType OverlayType => (OverlayType)new OverlayTypeToReadableNameConverter().ConvertBack(SelectedMetric, null, null, System.Globalization.CultureInfo.InvariantCulture);
        public List<Phase> AvailablePhases
        {
            get => availablePhases; set
            {
                availablePhases = value;
                OnPropertyChanged();
            }
        }
        public Phase SelectedPhase
        {
            get => selectedPhase; set
            {
                selectedPhase = value;
                OnPropertyChanged();
            }
        }
        public List<ChallengeType> AvailableChallengeTypes => Enum.GetValues<ChallengeType>().ToList();
        public string SelectedBoss { get; set; }
        public string SelectedEncounter { get; set; }

        public ChallengeModificationViewModel(string selectedSource)
        {
            _isEditing = false;
            this.selectedSource = selectedSource;
            SelectedEncounter = selectedSource.Split('|')[0];
            SelectedBoss = selectedSource.Split('|')[1];
            SelectedChallengeType = AvailableChallengeTypes.FirstOrDefault();
            SelectedColor = Brushes.CornflowerBlue.Color;
            AvailablePhases = DefaultPhaseManager.GetExisitingPhases().Where(p => p.PhaseSource == selectedSource).ToList();
            if (AvailablePhases.Any())
                SelectedPhase = AvailablePhases.First();
            AvailableMetrics = (List<string>)new OverlayTypeToReadableNameConverter().Convert(Enum.GetValues<OverlayType>().ToList(), null, null, System.Globalization.CultureInfo.InvariantCulture);
            SelectedMetric = AvailableMetrics.First();
        }
        public void Edit(Challenge challengeToEdit)
        {
            _isEditing = true;
            _editedChallenge = challengeToEdit;
            SelectedChallengeType = challengeToEdit.ChallengeType;
            Name = challengeToEdit.Name;

            SelectedSource = challengeToEdit.ChallengeSource;
            SelectedTarget = challengeToEdit.ChallengeTarget;
            UseRawValue = challengeToEdit.UseRawValues;
            UseMaxValue = challengeToEdit.UseMaxValue;

            SelectedPhase = AvailablePhases.FirstOrDefault(p => p.Id == challengeToEdit.PhaseId);
            if (challengeToEdit.PhaseMetric != OverlayType.None)
                SelectedMetric = (string)new OverlayTypeToReadableNameConverter().Convert(challengeToEdit.PhaseMetric, null, null, System.Globalization.CultureInfo.InvariantCulture);

            SelectedColor = challengeToEdit.BackgroundBrush.Color;
            Value = challengeToEdit.Value;
        }
        internal void Cancel()
        {
            if (_isEditing)
                OnCancelEdit(_editedChallenge);
        }
        public ReactiveCommand<Unit,Unit> SaveCommand => ReactiveCommand.Create(Save);
        private void Save()
        {
            OnNewChallenge(new Challenge()
            {
                ChallengeType = SelectedChallengeType,
                ChallengeSource = SelectedSource,
                ChallengeTarget = SelectedTarget,
                Name = Name,
                BackgroundBrush = ColorPreview,
                BackgroundColor = $"{ColorPreview.Color.R},{ColorPreview.Color.G},{ColorPreview.Color.B}",
                Id = Guid.NewGuid(),
                Value = Value,
                Source = selectedSource,
                IsEnabled = true,
                UseRawValues = UseRawValue,
                UseMaxValue = UseMaxValue,
                PhaseId = SelectedPhase != null ? SelectedPhase.Id : Guid.Empty,
                PhaseMetric = OverlayType
            }, _isEditing);
        }
        private void Reset()
        {
            HasValue = false;
            HasSource = false;
            HasTarget = false;
            TargetText = string.Empty;
            SourceText = string.Empty;
            ValuePrompt = string.Empty;
            UseRawValue = false;
            CanBeRate = false;
            CanHaveMax = false;
            UseMaxValue = false;
            HasPhases = false;
        }
        private void SetupUI()
        {
            Reset();
            switch (SelectedChallengeType)
            {
                case ChallengeType.DamageOut:
                    {
                        HasValue = true;
                        HasTarget = true;
                        ValuePrompt = "With Ability: ";
                        TargetText = "Target: ";
                        CanBeRate = true;
                        break;
                    }
                case ChallengeType.DamageIn:
                    {
                        HasValue = true;
                        HasSource = true;
                        ValuePrompt = "From Ability: ";
                        SourceText = "Source: ";
                        CanBeRate = true;
                        break;
                    }
                case ChallengeType.InterruptCount:
                    {
                        HasValue = true;
                        HasTarget = true;
                        ValuePrompt = "Interrupted Ability: ";
                        TargetText = "Target: ";
                        CanBeRate = true;
                        UseRawValue = true;
                        break;
                    }
                case ChallengeType.AbilityCount:
                    {
                        HasValue = true;
                        ValuePrompt = "Ability: ";
                        CanBeRate = true;
                        UseRawValue = true;
                        break;
                    }
                case ChallengeType.MetricDuringPhase:
                    {
                        HasPhases = true;
                        CanBeRate = false;
                        break;
                    }
                case ChallengeType.EffectStacks:
                    {
                        HasValue = true;
                        HasSource = true;
                        ValuePrompt = "From Effect: ";
                        SourceText = "Source: ";
                        CanHaveMax = true;
                        break;
                    }
            }
        }
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
