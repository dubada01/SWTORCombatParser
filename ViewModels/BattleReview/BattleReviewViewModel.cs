using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Views.Battle_Review;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using ReactiveUI;

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
    public class AvailableEntity : ReactiveObject
    {
        private bool selected;
        public event Action<Entity, bool> EntitiySelectionUpdated = delegate { };
        public void DefaultSeleted()
        {
            selected = true;
        }
        public void DeselectAll()
        {
            selected = false;
            this.RaisePropertyChanged(nameof(Selected));
        }
        public bool Selected
        {
            get => selected;
            set
            {
                this.RaiseAndSetIfChanged(ref selected, value);
                EntitiySelectionUpdated(Entity, value);
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
        DeathRecap,
        Abilities
    }
    public class BattleReviewViewModel : ReactiveObject
    {
        private Combat _currentlySelectedCombats;
        private EventHistoryViewModel _eventViewModel;
        private DisplayType selectedDisplayType;
        private string logFilter;
        private System.Threading.Timer timer;
        private bool updatePending = false;
        private object lockObject = new object();
        private List<AvailableEntity> _availableEntities = new List<AvailableEntity>();
        private EventHistoryView _eventViewContent;

        public string LogFilter
        {
            get => logFilter; set
            {
                if (value == logFilter)
                    return;
                logFilter = value;
                System.Threading.Tasks.Task.Run(() => {
                    lock (lockObject)
                    {
                        if (!updatePending)
                        {
                            
                            // Start or reset the timer
                            timer.Change(500, Timeout.Infinite);
                            updatePending = true;
                        }
                        _eventViewModel.SetFilter(logFilter);
                    }

                });

            }
        }
        private void TimerCallback(object state)
        {

            // Perform the filter update on the appropriate thread if required
            _eventViewModel.SetFilter(logFilter);
            lock (lockObject)
            {
                updatePending = false;
            }

        }

        public EventHistoryView EventViewContent
        {
            get => _eventViewContent;
            set => this.RaiseAndSetIfChanged(ref _eventViewContent,value);
        }

        public List<AvailableEntity> AvailableEntities
        {
            get => _availableEntities;
            set => this.RaiseAndSetIfChanged(ref _availableEntities, value);
        }

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
            // Initialize the timer but don't start it yet
            timer = new System.Threading.Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }
        public void CombatSelected(Combat combat)
        {
            _currentlySelectedCombats = combat;

            var entities = combat.AllEntities.DistinctBy(e=>e.LogId).Select(e => new AvailableEntity { Entity = e, Selected = false }).ToList();
            entities.ForEach(l => l.EntitiySelectionUpdated += UpdateSelectedEntities);
            ResetEntities(entities);
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
            }
        }
        private void UpdateSelectedEntities(Entity entity, bool selection)
        {
            if (entity.Name == "All" && selection)
            {
                AvailableEntities.Where(e => e.Entity.Name != "All").ToList().ForEach(e => e.DeselectAll());
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
