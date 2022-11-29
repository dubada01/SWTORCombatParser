using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures;

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
        public List<DisplayableLogEntry> LogsToDisplay { get; set; }
        public bool DisplayOffensiveBuffs { get; set; }
        public bool DisplayDefensiveBuffs => !DisplayOffensiveBuffs;

        public void SelectCombat(Combat combatSeleted)
        {
            _startTime = combatSeleted.StartTime;
            _currentlySelectedCombat = combatSeleted;
            foreach (var log in _currentlySelectedCombat.AllLogs)
            {
                log.SecondsSinceCombatStart = (log.TimeStamp - _startTime).TotalSeconds;
            }
            UpdateLogs();
        }
        public void SetViewableEntities(List<Entity> entitiesToshow)
        {
            _viewingEntities = entitiesToshow;
        }
        public void SetDisplayType(DisplayType type)
        {
            _typeSelected = type;
        }
        public void UpdateLogs()
        {
            if (_currentlySelectedCombat == null)
                return;
            LogsToDisplay = new List<DisplayableLogEntry>(_currentlySelectedCombat.AllLogs.Where(l=> LogFilter(l.Source,l.Target,l.Effect)).Select(
                l=>new DisplayableLogEntry(l.SecondsSinceCombatStart.ToString(),l.Source.Name,l.Target.Name,l.Ability,l.Effect.EffectName,l.Value.DisplayValue,l.Value.WasCrit,l.Value.ValueType.ToString(),l.Value.ModifierType,l.Value.ModifierDisplayValue)));
            OnPropertyChanged("LogsToDisplay");
        }
        private bool LogFilter(Entity source, Entity target, Effect effect)
        {
            switch (_typeSelected)
            {
                case DisplayType.All:
                    {
                        DisplayOffensiveBuffs = true;
                        return _viewingEntities.Contains(source) || _viewingEntities.Any(e => e.Name == "All" || _viewingEntities.Contains(target) );
                    }
                case DisplayType.Damage:
                    {
                        DisplayOffensiveBuffs = true;
                        return (_viewingEntities.Contains(source) || _viewingEntities.Any(e => e.Name == "All")) && effect.EffectType == EffectType.Apply && effect.EffectName == "Damage";
                    }
                case DisplayType.DamageTaken:
                    {
                        DisplayOffensiveBuffs = false;
                        return (_viewingEntities.Contains(target) || _viewingEntities.Any(e => e.Name == "All")) && effect.EffectType == EffectType.Apply && effect.EffectName == "Damage";
                    }
                case DisplayType.Healing:
                    {
                        DisplayOffensiveBuffs = true;
                        return (_viewingEntities.Contains(source) || _viewingEntities.Any(e => e.Name == "All")) && effect.EffectType == EffectType.Apply && effect.EffectName == "Heal";
                    }
                case DisplayType.HealingReceived:
                    {
                        DisplayOffensiveBuffs = false;
                        return (_viewingEntities.Contains(target) || _viewingEntities.Any(e => e.Name == "All")) && effect.EffectType == EffectType.Apply && effect.EffectName == "Heal";
                    }
                default:
                    return false;
            }
        }
    }
}
