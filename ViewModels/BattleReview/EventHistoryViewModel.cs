using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.BattleReview
{
    public class EventHistoryViewModel : ReactiveObject
    {
        private Combat? _currentlySelectedCombat;
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
                if (selectedIndex != value && HasFocus && value >= 0)
                {
                    LogPositionChanged(_displayedLogs[value].SecondsSinceCombatStart, GetInfosNearLog(_displayedLogs[value].SecondsSinceCombatStart));
                }
                this.RaiseAndSetIfChanged(ref selectedIndex, value);

            }
        }
        public bool DisplayOffensiveBuffs { get; set; }
        public bool DisplayDefensiveBuffs => !DisplayOffensiveBuffs;

        public async Task<DateTime> SelectCombat(Combat combatSeleted, bool inverted = false)
        {
            _startTime = combatSeleted.StartTime;
            _currentlySelectedCombat = combatSeleted;
            foreach (var log in _currentlySelectedCombat.AllLogs.OrderBy(kvp=>kvp.Key))
            {
                log.Value.SecondsSinceCombatStart = (log.Value.TimeStamp - _startTime).TotalSeconds;
            }
            return await UpdateLogs(inverted);
        }

        public void SetViewableEntities(List<Entity> entitiesToshow)
        {
            _viewingEntities = entitiesToshow;
        }
        public void SetDisplayType(DisplayType type)
        {
            _typeSelected = type;
        }
        public async Task SetFilter(string logFilter)
        {
            if(!string.IsNullOrEmpty(logFilter))
                _logFilter = logFilter.ToLower();
            await UpdateLogs();
        }

        public async Task<DateTime> UpdateLogs(bool isDethReview = false)
        {
            return await Task.Run(async () =>
            {
                DeathReview = isDethReview;
                if (_currentlySelectedCombat == null)
                    return DateTime.MinValue;
                DateTime firstDeath = DateTime.MinValue;
                await StartApplyFilter();


                if (isDethReview)
                {
                    var firstDeathLog = _displayedLogs.OrderBy(t => t.TimeStamp).FirstOrDefault(l =>
                        l.Effect.EffectId == _7_0LogParsing.DeathCombatId && l.Target.IsCharacter);
                    firstDeath = firstDeathLog == null
                        ? DateTime.MinValue
                        : firstDeathLog.TimeStamp.AddSeconds(-15);
                    _displayedLogs = _displayedLogs.Where(l => l.TimeStamp > firstDeath).ToList();
                }

                var maxValue = _displayedLogs.Any() ? _displayedLogs.Max(v => v.Value.EffectiveDblValue) : 0;
                var logs = Dispatcher.UIThread.Invoke(() =>
                {
                    return new List<DisplayableLogEntry>(_displayedLogs.OrderBy(l => l.TimeStamp).Select(
                        l =>
                            new DisplayableLogEntry(l.SecondsSinceCombatStart.ToString(CultureInfo.InvariantCulture),
                                l.Source.Name,
                                l.Source.LogId,
                                l.Target.Name,
                                l.Target.LogId,
                                l.Ability,
                                l.AbilityId,
                                l.Effect.EffectName,
                                l.Effect.EffectId,
                                l.Value.DisplayValue,
                                l.Value.WasCrit,
                                l.Value.ValueType != DamageType.none
                                    ? l.Value.ValueType.ToString()
                                    : l.Effect.EffectType.ToString(),
                                l.Value.ModifierType,
                                l.Value.ModifierDisplayValue, maxValue, l.Value.EffectiveDblValue,
                                l.Threat,
                                l.LogName,
                                l.LogLineNumber)));
                });
                await Task.Run(async () =>
                {
                    var tasks = logs.Select(l => l.AddIcons());
                    await Task.WhenAll(tasks);
                });

                LogsToDisplay = new ObservableCollection<DisplayableLogEntry>(logs);
                _distinctEntities = _currentlySelectedCombat.AllLogs.Values.Select(l => l.Source).Distinct().ToList();

                return firstDeath;
            });
        }

        private async Task StartApplyFilter()
        {
            await Task.Run(() =>
            {
                try
                {
                    Regex re = new Regex(!string.IsNullOrEmpty(_logFilter) ? _logFilter : "", RegexOptions.IgnoreCase);
                    _displayedLogs = _currentlySelectedCombat?.AllLogs.Values.Where(l=>LogFilter(l, re)).ToList() ?? new List<ParsedLogEntry>();
                }
                catch (Exception e)
                {
                    Logging.LogError(e.Message);
                    _displayedLogs = _currentlySelectedCombat?.AllLogs.Values.ToList() ?? new List<ParsedLogEntry>();
                }
            });
        }

        public bool DeathReview { get; set; }

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

            bool sourceSelected = _viewingEntities.Select(e=>e.LogId).Contains(log.Source.LogId);
            bool targetSelected = _viewingEntities.Select(e=>e.LogId).Contains(log.Target.LogId);

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
                DisplayType.DeathRecap      => IsLogDeathRecap(log),
                _ => false,
            };
        }

        private bool IsLogDeathRecap(ParsedLogEntry log)
        {
            if (log.Effect.EffectId == 836045448938502)
                return false;
            if (log.Effect.EffectId == _7_0LogParsing._healEffectId)
                return false;
            if (log.Effect.EffectType == EffectType.Remove)
                return false;
            if (_viewingEntities.Contains(log.Source) && log.Effect.EffectId == _7_0LogParsing._damageEffectId)
                return false;
            if (log.Source.IsCharacter && log.Effect.EffectId == _7_0LogParsing._damageEffectId)
                return false;
            if (log.Source.IsCharacter && !log.Target.IsCharacter && log.Effect.EffectType == EffectType.Apply)
                return false;
            if (log.Source.IsCharacter && log.Target.IsCharacter && log.Effect.EffectId == _7_0LogParsing.AbilityActivateId)
                return false;
            return true;
        }

        internal List<EntityInfo> Seek(double obj)
        {

            if (LogsToDisplay.Count == 0)
                return new List<EntityInfo>();
            var logToSeekTo = LogsToDisplay.MinBy(v => Math.Abs(TimeSpan.ParseExact(v.SecondsSinceCombatStart,@"mm\:ss\.fff",null).TotalSeconds - obj));
            SelectedIndex = LogsToDisplay.IndexOf(logToSeekTo);

            List<EntityInfo> returnList = GetInfosNearLog(TimeSpan.ParseExact(logToSeekTo.SecondsSinceCombatStart,@"mm\:ss\.fff",null).TotalSeconds);
            return returnList;
        }

        private List<EntityInfo> GetInfosNearLog(double seekTime)
        {
            List<EntityInfo> returnList = new List<EntityInfo>();
            foreach (var entity in _distinctEntities)
            {
                var closestLog = _currentlySelectedCombat.AllLogs.OrderBy(kvp=>kvp.Key).Where(e => e.Value.Source.LogId == entity.LogId || e.Value.Target.LogId == entity.LogId).MinBy(l => Math.Abs(l.Value.SecondsSinceCombatStart - seekTime));
                if (closestLog.Value == null)
                    return returnList;
                if (closestLog.Value.Source.LogId == entity.LogId)
                {
                    returnList.Add(closestLog.Value.SourceInfo);
                }
                else
                {
                    returnList.Add(closestLog.Value.TargetInfo);
                }
            }

            return returnList;
        }
    }
}
