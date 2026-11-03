using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.Personal
{
    public class PersonalOverlayInstanceViewModel : ReactiveObject, INotifyPropertyChanged
    {
        private double metricValue;
        private Combat _currentcombat;
        private OverlayType selectedMetric;
        private bool overlayUnlocked;
        private bool hasDefaultValue = true;
        private double height;
        private double defaultHeight = 30;
        private double currentScale;
        private string selectedVariable;

        public List<OverlayType> AvailableMetrics { get; set; } = Enum.GetValues<OverlayType>().ToList();
        public event Action<PersonalOverlayInstanceViewModel> CellRemoved = delegate { };
        public event Action CellChangedFromNone = delegate { };
        public event Action CellUpdated = delegate { };

        public ReactiveCommand<Unit,Unit> RemoveCellCommand => ReactiveCommand.Create(RemoveCell);

        private void RemoveCell()
        {
            CellRemoved(this);
        }
        public GridLength Height => new GridLength(defaultHeight * currentScale);
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
                if (SelectedMetric != OverlayType.CustomVariable)
                {
                    SelectedVariable = "";
                }
                if (hasDefaultValue)
                {
                    CellChangedFromNone();
                }
                hasDefaultValue = false;
                if (shouldUpdate)
                    CellUpdated();
                UpdateMetricNumber();
                OnPropertyChanged("SelectingCustomVariables");
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
        public bool ShowText => ShowAny && !OverlayUnlocked && !ShowVariable;
        public bool ShowVariable => SelectedMetric == OverlayType.CustomVariable && ShowAny && !OverlayUnlocked;
        public bool SelectingCustomVariables => SelectedMetric == OverlayType.CustomVariable && OverlayUnlocked;
        public List<string> AvailableVariables => OrbsVariableManager.GetVariables();
        public string SelectedVariable
        {
            get => selectedVariable; set
            {
                bool shouldUpdate = false;
                if (selectedVariable != value)
                {
                    shouldUpdate = true;
                }
                selectedVariable = value;
                if (shouldUpdate)
                    CellUpdated();
                OnPropertyChanged();
            }
        }
        public bool OverlayUnlocked
        {
            get => overlayUnlocked; set
            {
                overlayUnlocked = value;
                OnPropertyChanged("SelectingCustomVariables");
                OnPropertyChanged("ShowDropDown");
                OnPropertyChanged("ShowText");
                OnPropertyChanged("ShowVariable");
                OnPropertyChanged("ShowX");
                OnPropertyChanged();
            }
        }
        public CellInfo CurrentCellInfo => new CellInfo { CellType = selectedMetric, CustomVariable = selectedVariable };
        public string MetricValue => SelectedMetric != OverlayType.CombatTimer ? metricValue.ToString("N0") : $"{((int)CombatDuration.TotalMinutes == 0 ? "" : (int)CombatDuration.TotalMinutes+"m")} {CombatDuration.Seconds}s";
        public TimeSpan CombatDuration { get; set; }

        public PersonalOverlayInstanceViewModel(bool currentlyUnlocked, double scalar, CellInfo overlay = null)
        {
            currentScale = scalar;
            OverlayUnlocked = currentlyUnlocked;
            if (overlay != null)
            {
                selectedMetric = overlay.CellType;
                selectedVariable = overlay.CustomVariable;
                hasDefaultValue = false;
            }
            CombatLogStreamer.NewLineStreamed += TryUpdateVariable;
            CombatLogStreamer.CombatUpdated += CheckCombatState;
            CombatSelectionMonitor.OnInProgressCombatSelected += HandleNewCombatInfo;
            CombatSelectionMonitor.CombatSelected += HandleNewCombatInfo;
            CombatSelectionMonitor.PhaseSelected += HandleNewCombatInfo;
            HandleNewCombatInfo(CombatIdentifier.CurrentCombat);
            MetricColorLoader.OnOverlayTypeColorUpdated += TryUpdateColor;
        }

        private void TryUpdateColor(OverlayType type)
        {
            if(type == SelectedMetric)
            {
                OnPropertyChanged("SelectedMetric");
            }
        }

        private void CheckCombatState(CombatStatusUpdate obj)
        {
            if (obj.Type == UpdateType.Start)
            {
                metricValue = 0;
                OnPropertyChanged("MetricValue");
            }
        }

        private void TryUpdateVariable(ParsedLogEntry obj)
        {
            if (!string.IsNullOrEmpty(SelectedVariable))
            {
                UpdateMetricVariable();
            }
        }

        public void UpdateScale(double scalar)
        {
            currentScale = scalar;
            OnPropertyChanged("Height");
        }
        private void HandleNewCombatInfo(Combat newCombat)
        {
            if (newCombat == null || newCombat.StartTime == DateTime.MinValue)
                return;
            _currentcombat = newCombat;
            if (string.IsNullOrEmpty(SelectedVariable) && SelectedMetric != OverlayType.CombatTimer)
            {
                UpdateMetricNumber();
            }
            if(string.IsNullOrEmpty(SelectedVariable) && SelectedMetric == OverlayType.CombatTimer)
            {
                CombatDuration = newCombat.EndTime - newCombat.StartTime;
                OnPropertyChanged("MetricValue");
            }
        }

        private void UpdateMetricVariable()
        {
            metricValue = OrbsVariableManager.GetValue(SelectedVariable);
            OnPropertyChanged("MetricValue");
        }

        private void UpdateMetricNumber()
        {
            if (_currentcombat == null)
            {
                return;
            }
            metricValue = MetricGetter.GetValueForMetric(SelectedMetric,  _currentcombat, CombatLogStateBuilder.CurrentState.LocalPlayer);
            OnPropertyChanged("MetricValue");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
