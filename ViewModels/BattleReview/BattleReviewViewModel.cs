using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Views.Battle_Review;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

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
    public class AvailableEntity : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private bool selected;
        public event Action<Entity, bool> EntitiySelectionUpdated = delegate { };
        public void DefaultSeleted()
        {
            selected = true;
        }
        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                EntitiySelectionUpdated(Entity, value);
                OnPropertyChanged();
            }
        }
        public Entity Entity { get; set; }
    }
    public enum DisplayType
    {
        All,
        Damage,
        DamageTaken,
        Healing,
        HealingReceived,
        DeathRecap
    }
    public class BattleReviewViewModel : INotifyPropertyChanged
    {
        private Combat _currentlySelectedCombats;
        private EventHistoryViewModel _eventViewModel;
        private DisplayType selectedDisplayType;
        private string logFilter;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public string LogFilter
        {
            get => logFilter; set
            {
                if (value == logFilter)
                    return;
                logFilter = value;
                _eventViewModel.SetFilter(logFilter);
            }
        }
        public EventHistoryView EventViewContent { get; set; }
        public List<AvailableEntity> AvailableEntities { get; set; } = new List<AvailableEntity>();
        public List<DisplayType> AvailableDisplayTypes { get; set; } = Enum.GetValues(typeof(DisplayType)).Cast<DisplayType>().ToList();
        public DisplayType SelectedDisplayType
        {
            get => selectedDisplayType;
            set
            {
                selectedDisplayType = value;
                _eventViewModel.SetDisplayType(selectedDisplayType);
                UpdateVisuals();
            }
        }
        public BattleReviewViewModel()
        {
            _eventViewModel = new EventHistoryViewModel();
            EventViewContent = new EventHistoryView(_eventViewModel);
        }
        public void CombatSelected(Combat combat)
        {
            _currentlySelectedCombats = combat;

            var entities = combat.AllEntities.Select(e => new AvailableEntity { Entity = e, Selected = false }).ToList();
            entities.ForEach(l => l.EntitiySelectionUpdated += UpdateSelectedEntities);
            ResetEntities(entities);
            OnPropertyChanged("AvailableEntities");
            UpdateVisuals();
        }

        public void Reset()
        {
            _currentlySelectedCombats = new Combat();
            ResetEntities(new List<AvailableEntity>());
            UpdateVisuals();
        }
        private void ResetEntities(List<AvailableEntity> newEntities)
        {
            var entitiesToRemove = AvailableEntities.Except(newEntities, new EntityComparison()).ToList();
            foreach (var entity in entitiesToRemove)
            {
                entity.EntitiySelectionUpdated -= UpdateSelectedEntities;
                AvailableEntities.Remove(entity);
            }
            var addedEntities = newEntities.Except(AvailableEntities, new EntityComparison());
            foreach (var entitiy in addedEntities)
            {
                AvailableEntities.Add(entitiy);
            }
            AvailableEntities = AvailableEntities.OrderBy(e => e.Entity.Name).ToList();
            if (!AvailableEntities.Any(e => e.Entity.Name == "All"))
            {
                var allEntity = new AvailableEntity { Entity = new Entity { Name = "All" } };
                allEntity.EntitiySelectionUpdated += UpdateSelectedEntities;
                AvailableEntities.Insert(0, allEntity);
                allEntity.DefaultSeleted();
                _eventViewModel.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
                UpdateVisuals();
            }
        }
        private void UpdateSelectedEntities(Entity entity, bool selection)
        {
            if (entity.Name == "All" && selection)
            {
                AvailableEntities.Where(e => e.Entity.Name != "All").ToList().ForEach(e => e.Selected = false);
            }
            if (entity.Name != "All" && selection)
            {
                AvailableEntities.First(e => e.Entity.Name == "All").Selected = false;
            }
            _eventViewModel.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
            UpdateVisuals();
        }
        private void UpdateVisuals()
        {
            _eventViewModel.SelectCombat(_currentlySelectedCombats);
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
