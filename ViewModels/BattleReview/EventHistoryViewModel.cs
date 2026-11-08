using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
        private Combat? _displayedCombat;
        private int _updateVersion;
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
                if (selectedIndex != value && HasFocus && value >= 0 && value < _displayedLogs.Count)
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
            if (type != DisplayType.Abilities)
            {
                DisplayOffensiveBuffs = type is DisplayType.All or DisplayType.Damage or DisplayType.Healing;
                this.RaisePropertyChanged(nameof(DisplayOffensiveBuffs));
                this.RaisePropertyChanged(nameof(DisplayDefensiveBuffs));
            }
        }
        public Task SetFilter(string logFilter)
        {
            SetFilterText(logFilter);
            return UpdateLogs();
        }

        public void SetFilterText(string logFilter)
        {
            _logFilter = logFilter ?? string.Empty;
            Interlocked.Increment(ref _updateVersion);
        }

        public async Task<DateTime> UpdateLogs(bool isDethReview = false)
        {
            var version = Interlocked.Increment(ref _updateVersion);
            var combat = _currentlySelectedCombat;
            if (combat == null)
                return DateTime.MinValue;

            var filter = _logFilter;
            var type = _typeSelected;
            var entities = _viewingEntities.ToList();
            var entityIds = entities.Select(e => e.LogId).ToHashSet();
            var showAll = entities.Any(e => e.Name == "All");
            var (displayedLogs, firstDeath) = await Task.Run(() =>
            {
                List<ParsedLogEntry> filtered;
                try
                {
                    var regex = string.IsNullOrEmpty(filter) ? null :
                        new Regex(filter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                    filtered = combat.AllLogs.Values.Where(l => LogFilter(l, regex, type, entities, entityIds, showAll)).ToList();
                }
                catch (ArgumentException)
                {
                    filtered = combat.AllLogs.Values.Where(l => LogFilter(l, null, type, entities, entityIds, showAll)).ToList();
                }
                catch (RegexMatchTimeoutException)
                {
                    filtered = combat.AllLogs.Values.Where(l => LogFilter(l, null, type, entities, entityIds, showAll)).ToList();
                }

                var death = DateTime.MinValue;
                if (isDethReview)
                {
                    var firstDeathLog = filtered.OrderBy(t => t.TimeStamp).FirstOrDefault(l =>
                        l.Effect.EffectId == _7_0LogParsing.DeathCombatId && l.Target.IsCharacter);
                    death = firstDeathLog == null ? DateTime.MinValue : firstDeathLog.TimeStamp.AddSeconds(-15);
                    filtered = filtered.Where(l => l.TimeStamp > death).ToList();
                }
                return (filtered.OrderBy(l => l.TimeStamp).ToList(), death);
            });

            if (version != Volatile.Read(ref _updateVersion))
                return firstDeath;

            var maxValue = displayedLogs.Count > 0 ? displayedLogs.Max(v => v.Value.EffectiveDblValue) : 0;
            var logs = await Dispatcher.UIThread.InvokeAsync(() => displayedLogs.Select(l =>
                new DisplayableLogEntry(l.SecondsSinceCombatStart.ToString(CultureInfo.InvariantCulture),
                    l.Source.Name, l.Source.LogId, l.Target.Name, l.Target.LogId, l.Ability, l.AbilityId,
                    l.Effect.EffectName, l.Effect.EffectId, l.Value.DisplayValue, l.Value.WasCrit,
                    l.Value.ValueType != DamageType.none ? l.Value.ValueType.ToString() : l.Effect.EffectType.ToString(),
                    l.Value.ModifierType, l.Value.ModifierDisplayValue, maxValue, l.Value.EffectiveDblValue,
                    l.Threat, l.LogName, l.LogLineNumber)).ToList());
            await Task.WhenAll(logs.Select(l => l.AddIcons()));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != Volatile.Read(ref _updateVersion))
                    return;
                DeathReview = isDethReview;
                _displayedCombat = combat;
                _displayedLogs = displayedLogs;
                _distinctEntities = combat.AllLogs.Values.Select(l => l.Source).Distinct().ToList();
                LogsToDisplay = new ObservableCollection<DisplayableLogEntry>(logs);
            });
            return firstDeath;
        }

        public bool DeathReview { get; set; }

        private enum MatchField {
            Source,
            Target,
            Either,
        };

        private static bool LogFilter(ParsedLogEntry log, Regex re, DisplayType type, List<Entity> viewingEntities, HashSet<long> entityIds, bool showAll)
        {
            MatchField matchField = MatchField.Either;
            List<EffectType> matchEffectTypes = [];
            switch (type)
            {
                case DisplayType.All:
                    matchField = MatchField.Either;
                    break;
                case DisplayType.Damage:
                    matchField = MatchField.Source;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.DamageTaken:
                    matchField = MatchField.Target;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.Healing:
                    matchField = MatchField.Source;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.HealingReceived:
                    matchField = MatchField.Target;
                    matchEffectTypes.Add(EffectType.Apply);
                    break;
                case DisplayType.Abilities:
                    matchField = MatchField.Target;  // Should probably be Either
                    matchEffectTypes.Add(EffectType.Event);
                    break;
                case DisplayType.DeathRecap:
                    matchField = MatchField.Either;
                    break;

            }

            bool sourceSelected = entityIds.Contains(log.Source.LogId);
            bool targetSelected = entityIds.Contains(log.Target.LogId);

            if (!(
                showAll
                || (matchField == MatchField.Source && sourceSelected)
                || (matchField == MatchField.Target && targetSelected)
                || (matchField == MatchField.Either && (sourceSelected || targetSelected))
            )) {
                return false;
            }
            if (re != null)
            {
                if (!log.Strings().Any(s => s != null && re.IsMatch(s))) {
                    return false;
                }
            }
            if (matchEffectTypes.Count > 0 && !matchEffectTypes.Contains(log.Effect.EffectType)) {
                return false;
            }

            return type switch
            {
                DisplayType.All             => true,
                DisplayType.Damage          => log.Effect.EffectId == _7_0LogParsing._damageEffectId,
                DisplayType.DamageTaken     => log.Effect.EffectId == _7_0LogParsing._damageEffectId,
                DisplayType.Healing         => log.Effect.EffectId == _7_0LogParsing._healEffectId,
                DisplayType.HealingReceived => log.Effect.EffectId == _7_0LogParsing._healEffectId,
                DisplayType.Abilities       => true,
                DisplayType.DeathRecap      => IsLogDeathRecap(log, viewingEntities),
                _ => false,
            };
        }

        private static bool IsLogDeathRecap(ParsedLogEntry log, List<Entity> viewingEntities)
        {
            if (log.Effect.EffectId == 836045448938502)
                return false;
            if (log.Effect.EffectId == _7_0LogParsing._healEffectId)
                return false;
            if (log.Effect.EffectType == EffectType.Remove)
                return false;
            if (viewingEntities.Contains(log.Source) && log.Effect.EffectId == _7_0LogParsing._damageEffectId)
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
                var closestLog = _displayedCombat.AllLogs.OrderBy(kvp=>kvp.Key).Where(e => e.Value.Source.LogId == entity.LogId || e.Value.Target.LogId == entity.LogId).MinBy(l => Math.Abs(l.Value.SecondsSinceCombatStart - seekTime));
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
