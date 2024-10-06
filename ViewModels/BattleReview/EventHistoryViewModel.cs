using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public class EventHistoryViewModel : ReactiveObject
    {
        private Combat _currentlySelectedCombat;
        private DateTime _startTime;
        private List<Entity> _viewingEntities = new List<Entity>();
        private DisplayType _typeSelected;
        private string _logFilter;
        public IObservable<double> LogsUpdatedObservable;
        private int selectedIndex;
        private List<Entity> _distinctEntities = new List<Entity>();
        private List<ParsedLogEntry> _displayedLogs = new List<ParsedLogEntry>();
        private ObservableCollection<DisplayableLogEntry> _logsToDisplay = new ObservableCollection<DisplayableLogEntry>();

        public event Action<double, List<EntityInfo>> LogPositionChanged = delegate { };

        public ObservableCollection<DisplayableLogEntry> LogsToDisplay
        {
            get => _logsToDisplay;
            set => this.RaiseAndSetIfChanged(ref _logsToDisplay, value);
        }

        public bool HasFocus { get; set; }
        public int SelectedIndex
        {
            get => selectedIndex; set
            {
                if (selectedIndex != value && HasFocus)
                {
                    LogPositionChanged(_displayedLogs[value].SecondsSinceCombatStart, GetInfosNearLog(_displayedLogs[value].SecondsSinceCombatStart));
                }
                this.RaiseAndSetIfChanged(ref selectedIndex, value);

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
            Dispatcher.UIThread.Invoke(() =>
            {
                return UpdateLogs(inverted);
            });
            return DateTime.MinValue;
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
            if(!string.IsNullOrEmpty(logFilter))
                _logFilter = logFilter.ToLower();
            UpdateLogs();
        }
        public DateTime UpdateLogs(bool isDethReview = false)
        {
            if (_currentlySelectedCombat == null)
                return DateTime.MinValue;
            DateTime firstDeath = DateTime.MinValue;
            Regex re = new Regex(!string.IsNullOrEmpty(_logFilter) ? _logFilter : "", RegexOptions.IgnoreCase);
            _displayedLogs = _currentlySelectedCombat.AllLogs.Where(l=>LogFilter(l, re)).ToList();
            Dispatcher.UIThread.Invoke(() =>
            {
                if (isDethReview)
                {
                    var firstDeathLog = _displayedLogs.OrderBy(t => t.TimeStamp).FirstOrDefault(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && l.Target.IsCharacter);
                    firstDeath = firstDeathLog == null ? DateTime.MinValue : firstDeathLog.TimeStamp.AddSeconds(-15);
                    _displayedLogs = _displayedLogs.Where(l => l.TimeStamp > firstDeath).ToList();
                }
                var maxValue = _displayedLogs.Any() ? _displayedLogs.Max(v => v.Value.EffectiveDblValue) : 0;
                var logs = new List<DisplayableLogEntry>(_displayedLogs.OrderBy(l => l.TimeStamp).Select(
                    l =>
                    new DisplayableLogEntry(l.SecondsSinceCombatStart.ToString(CultureInfo.InvariantCulture),
                    l.Source.Name,
                    string.Intern(l.Source.LogId.ToString()),
                    l.Target.Name,
                    string.Intern(l.Target.LogId.ToString()),
                    l.Ability,
                    l.AbilityId,
                    l.Effect.EffectName,
                    l.Effect.EffectId,
                    l.Value.DisplayValue,
                    l.Value.WasCrit,
                    l.Value.ValueType != DamageType.none ? l.Value.ValueType.ToString() : l.Effect.EffectType.ToString(),
                    l.Value.ModifierType,
                    l.Value.ModifierDisplayValue, maxValue, l.Value.EffectiveDblValue,
                    l.Threat)));
                Task.Run(() => { logs.ForEach(async l => await l.AddIcons()); });

                LogsToDisplay = new ObservableCollection<DisplayableLogEntry>(logs);
                _distinctEntities = _currentlySelectedCombat.AllLogs.Select(l => l.Source).Distinct().ToList();
            });
            return firstDeath;
        }

        private enum MatchField {
            Source,
            Target,
            Either,
        };

        private bool LogFilter(ParsedLogEntry log, Regex re)
        {
            MatchField matchField = MatchField.Either;
            List<EffectType> matchEffectTypes = [];
            switch (_typeSelected)
            {
                case DisplayType.All:
                    DisplayOffensiveBuffs = true;
                    matchField = MatchField.Either;
                    break;
                case DisplayType.Damage:
                    DisplayOffensiveBuffs = true;
                    matchField = MatchField.Source;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.DamageTaken:
                    DisplayOffensiveBuffs = false;
                    matchField = MatchField.Target;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.Healing:
                    DisplayOffensiveBuffs = true;
                    matchField = MatchField.Source;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.HealingReceived:
                    DisplayOffensiveBuffs = false;
                    matchField = MatchField.Target;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.Abilities:
                    matchField = MatchField.Target;  // Should probably be Either
                    matchEffectTypes.Add(EffectType.Event);
                    break;
                case DisplayType.DeathRecap:
                    DisplayOffensiveBuffs = false;
                    matchField = MatchField.Either;
                    break;

            }

            bool sourceSelected = _viewingEntities.Contains(log.Source);
            bool targetSelected = _viewingEntities.Contains(log.Target);

            if (!(
                _viewingEntities.Any(e => e.Name == "All")
                || (matchField == MatchField.Source && sourceSelected)
                || (matchField == MatchField.Target && targetSelected)
                || (matchField == MatchField.Either && (sourceSelected || targetSelected))
            )) {
                return false;
            }
            if (!string.IsNullOrEmpty(_logFilter))
            {
                if (!log.Strings().Any(s => s != null && re.Match(s.ToLower()).Success)) {
                    return false;
                }
            }
            if (matchEffectTypes.Count > 0 && !matchEffectTypes.Contains(log.Effect.EffectType)) {
                return false;
            }

            return _typeSelected switch
            {
                DisplayType.All             => true,
                DisplayType.Damage          => log.Effect.EffectId == _7_0LogParsing._damageEffectId,
                DisplayType.DamageTaken     => log.Effect.EffectId == _7_0LogParsing._damageEffectId,
                DisplayType.Healing         => log.Effect.EffectId == _7_0LogParsing._healEffectId,
                DisplayType.HealingReceived => log.Effect.EffectId == _7_0LogParsing._healEffectId,
                DisplayType.Abilities       => true,
                DisplayType.DeathRecap      => (
                                        log.Effect.EffectId != _7_0LogParsing._healEffectId
                                        && log.Effect.EffectType != EffectType.Remove
                                        && !(_viewingEntities.Contains(log.Source)
                                        && log.Effect.EffectId == _7_0LogParsing._damageEffectId)
                                        && log.Effect.EffectId != _7_0LogParsing.AbilityActivateId
                                    ),
                _ => false,
            };
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
