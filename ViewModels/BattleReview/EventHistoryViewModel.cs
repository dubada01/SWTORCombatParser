using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public class EventHistoryViewModel:INotifyPropertyChanged
    {
        private IDisposable _sliderUpdateSubscription;
        private Combat _currentlySelectedCombat;
        private DateTime _startTime;
        private List<Entity> _viewingEntities = new List<Entity>();
        private DisplayType _typeSelected;
        public event PropertyChangedEventHandler PropertyChanged;
        public IObservable<double> LogsUpdatedObservable;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public ObservableCollection<ParsedLogEntry> LogsToDisplay { get; set; }
        public EventHistoryViewModel()
        {
            _sliderUpdateSubscription = Observable.FromEvent<double>(
    handler => ReviewSliderUpdates.OnSliderUpdated += handler,
    handler => ReviewSliderUpdates.OnSliderUpdated -= handler).Sample(TimeSpan.FromSeconds(0.1)).Subscribe(newPos => { UpdateLogsForTime(newPos); });
        }
        public void SelectCombat(Combat combatSeleted)
        {
            _startTime = combatSeleted.StartTime;
            _currentlySelectedCombat = combatSeleted;
        }
        public void SetViewableEntities(List<Entity> entitiesToshow)
        {
            _viewingEntities = entitiesToshow;
        }
        public void SetDisplayType(DisplayType type)
        {
            _typeSelected = type;
        }
        private void UpdateLogsForTime(double time)
        {
            if (_currentlySelectedCombat == null)
                return;
            var tempLogs = _currentlySelectedCombat.AllLogs.Where(l => (l.TimeStamp - _startTime).TotalSeconds < time);
            foreach(var log in tempLogs)
            {
                log.SecondsSinceCombatStart = (log.TimeStamp - _startTime).TotalSeconds;
            }
            LogsToDisplay = new ObservableCollection<ParsedLogEntry>(tempLogs.Where(l=> LogFilter(l.Source,l.Target,l.Effect)));
            OnPropertyChanged("LogsToDisplay");
        }
        private bool LogFilter(Entity source, Entity target, Effect effect)
        {
            switch (_typeSelected)
            {
                case DisplayType.Damage:
                    return _viewingEntities.Contains(source) && effect.EffectType == EffectType.Apply && effect.EffectName == "Damage";
                case DisplayType.DamageTaken:
                    return _viewingEntities.Contains(target) && effect.EffectType == EffectType.Apply && effect.EffectName == "Damage";
                case DisplayType.Healing:
                    return _viewingEntities.Contains(source) && effect.EffectType == EffectType.Apply && effect.EffectName == "Heal";
                case DisplayType.HealingReceived:
                    return _viewingEntities.Contains(target) && effect.EffectType == EffectType.Apply && effect.EffectName == "Heal";
                default:
                    return false;
            }
        }
    }
}
