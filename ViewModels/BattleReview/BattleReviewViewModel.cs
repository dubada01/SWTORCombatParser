using ScottPlot;
using SWTORCombatParser.Views.Battle_Review;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public static class ReviewSliderUpdates
    {
        public static event Action<double> OnSliderUpdated = delegate { };
        public static void UpdateSlider(double value)
        {
            OnSliderUpdated(value);
        }
    }
    public class AvailableEntity:INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private bool selected;
        public event Action EntitiySelectionUpdated = delegate { };
        public bool Selected
        {
            get => selected; 
            set
            {
                selected = value;
                EntitiySelectionUpdated();
            }
        }
        public Entity Entity { get; set; }
    }
    public enum DisplayType
    {
        Damage,
        DamageTaken,
        Healing,
        HealingReceived,
    }
    public class BattleReviewViewModel : INotifyPropertyChanged
    {
        private Combat _currentlySelectedCombats;
        private double _minPlotTime;
        private double _maxPlotTime;
        private MapViewModel _mapViewModel;
        private EventHistoryViewModel _eventViewModel;
        private ReviewPlotViewModel _plotViewModel;
        private double sliderPosition;
        private string movingAverageWindow = "10";
        private DisplayType selectedDisplayType;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public Thickness SliderMargin { get; set; }
        public MapView MapViewContent { get; set; }
        public EventHistoryView EventViewContent { get; set; }
        public ObservableCollection<AvailableEntity> AvailableEntities { get; set; } = new ObservableCollection<AvailableEntity>();
        public List<DisplayType> AvailableDisplayTypes { get; set; } = Enum.GetValues(typeof(DisplayType)).Cast<DisplayType>().ToList();
        public DisplayType SelectedDisplayType
        {
            get => selectedDisplayType;
            set
            {
                selectedDisplayType = value;
                _plotViewModel.SetDisplayType(selectedDisplayType);
                _eventViewModel.SetDisplayType(selectedDisplayType);
                UpdateVisuals();
            }
        }
        public WpfPlot PlotViewContent => _plotViewModel.Plot;
        public BattleReviewViewModel()
        {

            _mapViewModel = new MapViewModel();
            MapViewContent = new MapView(_mapViewModel);

            _eventViewModel = new EventHistoryViewModel();
            EventViewContent = new EventHistoryView(_eventViewModel);

            _plotViewModel = new ReviewPlotViewModel();
            _plotViewModel.OnNewOffset += UpdateOffset;
            _plotViewModel.NewAxisLmits += UpdateAxisRanges;
            _plotViewModel.SetWindowSize(10);
        }

        private void UpdateAxisRanges(double arg1, double arg2)
        {
            _minPlotTime = arg1;
            _maxPlotTime = arg2;
            UpdatedSliderPosition(SliderPosition);
        }

        public void CombatSelected(Combat combat)
        {
            _currentlySelectedCombats = combat;
            
            var entities = combat.AllEntities.Select(e => new AvailableEntity { Entity = e, Selected = false }).ToList();
            entities.ForEach(l => l.EntitiySelectionUpdated += UpdateSelectedEntities);
            ResetEntities(entities);
            
            OnPropertyChanged("AvailableEntities");
            UpdateSelectedEntities();
            UpdateVisuals();
        }
        private void ResetEntities(List<AvailableEntity> newEntities)
        {
            var entitiesToRemove = AvailableEntities.Except(newEntities,new EntityComparison()).ToList();
            foreach (var entity in entitiesToRemove)
            {
                entity.EntitiySelectionUpdated -= UpdateSelectedEntities;
                AvailableEntities.Remove(entity);
            }
            var addedEntities = newEntities.Except(AvailableEntities, new EntityComparison());
            foreach(var entitiy in addedEntities)
            {
                AvailableEntities.Add(entitiy);
            }
            
        }
        private void UpdateSelectedEntities()
        {
            var seletedElements = AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList();
            _plotViewModel.SetViewableEntities(seletedElements);
            _eventViewModel.SetViewableEntities(seletedElements);
            UpdateVisuals();
        }

        public double SliderPosition
        {
            get => sliderPosition; set
            {
                sliderPosition = value;
                UpdatedSliderPosition(sliderPosition);
            }
        }
        public string MovingAverageWindow
        {
            get => movingAverageWindow; set
            {
                movingAverageWindow = value;

                if (!int.TryParse(movingAverageWindow, out var test))
                    return;
                _plotViewModel.SetWindowSize(int.Parse(movingAverageWindow));
                UpdateVisuals();
            }
        }
        private void UpdateVisuals()
        {
            _mapViewModel.SetCombat(_currentlySelectedCombats);
            _eventViewModel.SelectCombat(_currentlySelectedCombats);
            _plotViewModel.DisplayDataForCombat(_currentlySelectedCombats);
        }
        private void UpdateOffset(double obj)
        {
            var scalingFactor = getScalingFactor();

            var SliderOffset = obj / (scalingFactor.Item1 / 96d);
            SliderMargin = new Thickness(SliderOffset - 15, 0, 0, 0);
            OnPropertyChanged("SliderMargin");
        }
        private (int, int) getScalingFactor()
        {
            var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static);
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static);

            var dpiX = (int)dpiXProperty.GetValue(null, null);
            var dpiY = (int)dpiYProperty.GetValue(null, null);
            return (dpiX, dpiY);
        }
        public void UpdatedSliderPosition(double position)
        {
            var range = _maxPlotTime - _minPlotTime;
            ReviewSliderUpdates.UpdateSlider((range * position / 10) + _minPlotTime);
        }
    }

    internal class EntityComparison : IEqualityComparer<AvailableEntity>
    {
        public bool Equals(AvailableEntity x, AvailableEntity y)
        {
            return x.Entity.Name == y.Entity.Name;
        }

        public int GetHashCode([DisallowNull] AvailableEntity obj)
        {
            return obj.Entity.GetHashCode();
        }
    }
}
