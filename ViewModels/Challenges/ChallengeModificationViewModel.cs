using Prism.Commands;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeModificationViewModel : INotifyPropertyChanged
    {
        private string selectedSource;
        private ChallengeType selectedChallengeType;
        private Challenge _editedChallenge;
        private bool _isEditing;
        private string name;
        private string _value;
        private string valuePrompt;
        private bool hasValue;
        private ObservableCollection<string> availableTargets = new ObservableCollection<string>();
        private List<string> addedCustomTargets = new List<string>();
        private Color selectedColor;
        private string currentColorHex;
        private string targetText;
        private bool hasTarget;
        private bool hasCustomTarget;
        private string customTarget;
        private string selectedTarget;
        private string sourceText;
        private bool hasSource;
        private bool hasCustomSource;
        private string customSource;
        private string selectedSource1;
        private bool useRawValues;
        private bool canBeRate;
        private bool useMaxValue;
        private bool canHaveMax;

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

            SelectedColor = challengeToEdit.BackgroundBrush.Color;
            Value = challengeToEdit.Value;
        }
        internal void Cancel()
        {
            if (_isEditing)
                OnCancelEdit(_editedChallenge);
        }
        public ICommand SaveCommand => new DelegateCommand(Save);
        private void Save()
        {
            OnNewChallenge(new Challenge()
            {
                ChallengeType = SelectedChallengeType,
                ChallengeSource = SelectedSource,
                ChallengeTarget = SelectedTarget,
                Name = Name,
                BackgroundBrush = ColorPreview,
                Id = Guid.NewGuid(),
                Value = Value,
                Source = selectedSource,
                IsEnabled = true,
                UseRawValues = UseRawValue,
                UseMaxValue = UseMaxValue,
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
                    var newColor = (Color)ColorConverter.ConvertFromString(currentColorHex);
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
