using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public class EventHistoryViewModel:INotifyPropertyChanged
    {
        private Combat _currentlySelectedCombat;
        private DateTime _startTime;
        private List<Entity> _viewingEntities = new List<Entity>();
        private DisplayType _typeSelected;
        private string _logFilter;
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
        public void SetFilter(string logFilter)
        {
            _logFilter = logFilter.ToLower();
            UpdateLogs();
        }
        public void UpdateLogs()
        {
            if (_currentlySelectedCombat == null)
                return;
            LogsToDisplay = new List<DisplayableLogEntry>(_currentlySelectedCombat.AllLogs.Where(LogFilter).Select(
                l=>new DisplayableLogEntry(l.SecondsSinceCombatStart.ToString(CultureInfo.InvariantCulture),l.Source.Name,l.Target.Name,l.Ability,l.Effect.EffectName,l.Value.DisplayValue,l.Value.WasCrit,l.Value.ValueType != DamageType.none ? l.Value.ValueType.ToString():l.Effect.EffectType.ToString(),l.Value.ModifierType,l.Value.ModifierDisplayValue)));
            OnPropertyChanged("LogsToDisplay");
        }
        private bool LogFilter(ParsedLogEntry log)
        {
            if(string.IsNullOrEmpty(_logFilter) || 
                (log.Effect.EffectName?.ToLower().Contains(_logFilter) ?? false) || 
                (log.Effect.EffectId?.ToLower().Contains(_logFilter) ?? false) ||
                (log.Ability?.ToLower().Contains(_logFilter) ?? false) ||
                (log.AbilityId?.ToLower().Contains(_logFilter) ?? false) || 
                (log.Source.Name?.ToLower().Contains(_logFilter) ?? false) || 
                log.Source.LogId.ToString().ToLower().Contains(_logFilter) ||
                (log.Target.Name?.ToLower().Contains(_logFilter) ?? false) || 
                log.Target.LogId.ToString().ToLower().Contains(_logFilter) ||
                log.Value.ValueType.ToString().ToLower().Contains(_logFilter) ||
                log.Value.DblValue.ToString().Contains(_logFilter) ||
                log.Value.EffectiveDblValue.ToString().Contains(_logFilter)
                )
            {
                switch (_typeSelected)
                {
                    case DisplayType.All:
                        {
                            DisplayOffensiveBuffs = true;
                            return _viewingEntities.Contains(log.Source) || _viewingEntities.Any(e => e.Name == "All" || _viewingEntities.Contains(log.Target));
                        }
                    case DisplayType.Damage:
                        {
                            DisplayOffensiveBuffs = true;
                            return (_viewingEntities.Contains(log.Source) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == "Damage";
                        }
                    case DisplayType.DamageTaken:
                        {
                            DisplayOffensiveBuffs = false;
                            return (_viewingEntities.Contains(log.Target) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == "Damage";
                        }
                    case DisplayType.Healing:
                        {
                            DisplayOffensiveBuffs = true;
                            return (_viewingEntities.Contains(log.Source) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == "Heal";
                        }
                    case DisplayType.HealingReceived:
                        {
                            DisplayOffensiveBuffs = false;
                            return (_viewingEntities.Contains(log.Target) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == "Heal";
                        }
                    default:
                        return false;
                }
            }
            return false;
        }
    }
}
