using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public class EventHistoryViewModel : INotifyPropertyChanged
    {
        private Combat _currentlySelectedCombat;
        private DateTime _startTime;
        private List<Entity> _viewingEntities = new List<Entity>();
        private DisplayType _typeSelected;
        private string _logFilter;
        public event PropertyChangedEventHandler PropertyChanged;
        public IObservable<double> LogsUpdatedObservable;
        private int selectedIndex;
        private List<Entity> _distinctEntities = new List<Entity>();
        private List<ParsedLogEntry> _displayedLogs = new List<ParsedLogEntry>();

        public event Action<double, List<EntityInfo>> LogPositionChanged = delegate { };
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public List<DisplayableLogEntry> LogsToDisplay { get; set; } = new List<DisplayableLogEntry>();
        public bool HasFocus { get; set; }
        public int SelectedIndex
        {
            get => selectedIndex; set
            {
                if (selectedIndex != value && HasFocus)
                {
                    LogPositionChanged(_displayedLogs[value].SecondsSinceCombatStart, GetInfosNearLog(_displayedLogs[value].SecondsSinceCombatStart));
                }
                selectedIndex = value;

                OnPropertyChanged();
            }
        }
        public bool DisplayOffensiveBuffs { get; set; }
        public bool DisplayDefensiveBuffs => !DisplayOffensiveBuffs;

        public DateTime SelectCombat(Combat combatSeleted, bool inverted = false)
        {
            _startTime = combatSeleted.StartTime;
            _currentlySelectedCombat = combatSeleted;
            foreach (var log in _currentlySelectedCombat.AllLogs)
            {
                log.SecondsSinceCombatStart = (log.TimeStamp - _startTime).TotalSeconds;
            }
            return UpdateLogs(inverted);
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
        public DateTime UpdateLogs(bool isDethReview = false)
        {
            if (_currentlySelectedCombat == null)
                return DateTime.MinValue;
            DateTime firstDeath = DateTime.MinValue;
            _displayedLogs = _currentlySelectedCombat.AllLogs.Where(LogFilter).ToList();
            if (isDethReview)
            {
                var firstDeathLog = _displayedLogs.OrderBy(t => t.TimeStamp).FirstOrDefault(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && l.Target.IsCharacter);
                firstDeath = firstDeathLog == null ? DateTime.MinValue : firstDeathLog.TimeStamp.AddSeconds(-15);
                _displayedLogs = _displayedLogs.Where(l => l.TimeStamp > firstDeath).ToList();
            }
            var maxValue = _displayedLogs.Any() ? _displayedLogs.Max(v => v.Value.EffectiveDblValue) : 0;
            var logs = new List<DisplayableLogEntry>(_displayedLogs.Select(
                l => new DisplayableLogEntry(l.SecondsSinceCombatStart.ToString(CultureInfo.InvariantCulture),
                l.Source.Name,
                l.Source.LogId.ToString(),
                l.Target.Name,
                l.Target.LogId.ToString(),
                l.Ability,
                l.AbilityId,
                l.Effect.EffectName,
                l.Effect.EffectId,
                l.Value.DisplayValue,
                l.Value.WasCrit,
                l.Value.ValueType != DamageType.none ? l.Value.ValueType.ToString() : l.Effect.EffectType.ToString(),
                l.Value.ModifierType,
                l.Value.ModifierDisplayValue, maxValue, l.Value.EffectiveDblValue)));
            LogsToDisplay = logs;
            _distinctEntities = _currentlySelectedCombat.AllLogs.Select(l => l.Source).Distinct().ToList();
            OnPropertyChanged("LogsToDisplay");
            return firstDeath;
        }
        private bool LogFilter(ParsedLogEntry log)
        {
            if (string.IsNullOrEmpty(_logFilter) ||
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
                            return (_viewingEntities.Contains(log.Source) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectId == _7_0LogParsing._damageEffectId;
                        }
                    case DisplayType.DamageTaken:
                        {
                            DisplayOffensiveBuffs = false;
                            return (_viewingEntities.Contains(log.Target) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectId == _7_0LogParsing._damageEffectId;
                        }
                    case DisplayType.Healing:
                        {
                            DisplayOffensiveBuffs = true;
                            return (_viewingEntities.Contains(log.Source) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectId == _7_0LogParsing._healEffectId;
                        }
                    case DisplayType.HealingReceived:
                        {
                            DisplayOffensiveBuffs = false;
                            return (_viewingEntities.Contains(log.Target) || _viewingEntities.Any(e => e.Name == "All")) && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectId == _7_0LogParsing._healEffectId;
                        }
                    case DisplayType.DeathRecap:
                        {
                            DisplayOffensiveBuffs = false;
                            return (_viewingEntities.Contains(log.Source) || _viewingEntities.Contains(log.Target)) &&
    log.Effect.EffectId != _7_0LogParsing._healEffectId &&
    log.Effect.EffectType != EffectType.Remove &&
    !(_viewingEntities.Contains(log.Source) && log.Effect.EffectId == _7_0LogParsing._damageEffectId) &&
    log.Effect.EffectId != _7_0LogParsing.AbilityActivateId;
                        }
                    default:
                        return false;
                }
            }
            return false;
        }

        internal List<EntityInfo> Seek(double obj)
        {

            if (LogsToDisplay.Count == 0)
                return new List<EntityInfo>();
            var logToSeekTo = LogsToDisplay.MinBy(v => Math.Abs(double.Parse(v.SecondsSinceCombatStart, CultureInfo.InvariantCulture) - obj));
            SelectedIndex = LogsToDisplay.IndexOf(logToSeekTo);

            List<EntityInfo> returnList = GetInfosNearLog(double.Parse(logToSeekTo.SecondsSinceCombatStart, CultureInfo.InvariantCulture));
            return returnList;
        }

        private List<EntityInfo> GetInfosNearLog(double seekTime)
        {
            List<EntityInfo> returnList = new List<EntityInfo>();
            foreach (var entity in _distinctEntities)
            {
                var closestLog = _currentlySelectedCombat.AllLogs.Where(e => e.Source.LogId == entity.LogId || e.Target.LogId == entity.LogId).MinBy(l => Math.Abs(l.SecondsSinceCombatStart - seekTime));
                if (closestLog == null)
                    return returnList;
                if (closestLog.Source.LogId == entity.LogId)
                {
                    returnList.Add(closestLog.SourceInfo);
                }
                else
                {
                    returnList.Add(closestLog.TargetInfo);
                }
            }

            return returnList;
        }
    }
}
