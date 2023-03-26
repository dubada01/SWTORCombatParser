using Prism.Commands;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays.Personal
{
    public class PersonalOverlayInstanceViewModel : INotifyPropertyChanged
    {
        private double metricValue;
        private Combat _currentcombat;
        private OverlayType selectedMetric;
        private bool overlayUnlocked;
        private bool hasDefaultValue = true;
        private double height;
        private double defaultHeight = 30;
        private double currentScale;

        public List<OverlayType> AvailableMetrics { get; set; } = Enum.GetValues<OverlayType>().ToList();
        public event Action<PersonalOverlayInstanceViewModel> CellRemoved = delegate { };
        public event Action CellChangedFromNone = delegate { };
        public event Action CellUpdated = delegate { };

        public ICommand RemoveCellCommand => new DelegateCommand(RemoveCell);

        private void RemoveCell()
        {
            CellRemoved(this);
        }
        public double Height => defaultHeight * currentScale;
        public OverlayType SelectedMetric
        {
            get => selectedMetric; set
            {
                bool shouldUpdate = false;
                if (selectedMetric != value)
                {
                    shouldUpdate = true;
                }
                selectedMetric = value;

                if (hasDefaultValue)
                {
                    CellChangedFromNone();
                }
                hasDefaultValue = false;
                if (shouldUpdate)
                    CellUpdated();
                UpdateMetricNumber();
                OnPropertyChanged("ShowDropDown");
                OnPropertyChanged("ShowText");
                OnPropertyChanged("ShowAny");
                OnPropertyChanged("ShowX");
                OnPropertyChanged();
            }
        }
        public bool ShowX => !hasDefaultValue && OverlayUnlocked;
        public bool ShowAny => SelectedMetric == OverlayType.None ? false : true;
        public bool ShowDropDown => OverlayUnlocked;
        public bool ShowText => ShowAny && !OverlayUnlocked;
        public bool OverlayUnlocked
        {
            get => overlayUnlocked; set
            {
                overlayUnlocked = value;
                OnPropertyChanged("ShowDropDown");
                OnPropertyChanged("ShowText");
                OnPropertyChanged("ShowX");
                OnPropertyChanged();
            }
        }
        public string MetricValue => metricValue.ToString("N0");

        public PersonalOverlayInstanceViewModel(bool currentlyUnlocked,double scalar, OverlayType overlay = OverlayType.None)
        {
            currentScale= scalar;
            OverlayUnlocked = currentlyUnlocked;
            if (overlay != OverlayType.None)
            {
                selectedMetric = overlay;
                hasDefaultValue = false;
            }
            CombatIdentifier.NewCombatAvailable += HandleNewCombatInfo;
            HandleNewCombatInfo(CombatIdentifier.CurrentCombat);
        }
        public void UpdateScale(double scalar)
        {
            currentScale= scalar;
            OnPropertyChanged("Height");
        }
        private void HandleNewCombatInfo(Combat newCombat)
        {
            if (newCombat == null || newCombat.StartTime == DateTime.MinValue)
                return;
            _currentcombat = newCombat;
            UpdateMetricNumber();
        }

        private void UpdateMetricNumber()
        {
            if (_currentcombat == null)
            {
                return;
            }
            metricValue = MetricGetter.GetValueForMetric(SelectedMetric, new List<Combat> { _currentcombat }, CombatLogStateBuilder.CurrentState.LocalPlayer);
            OnPropertyChanged("MetricValue");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
