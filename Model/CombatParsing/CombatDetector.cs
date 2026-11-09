using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using SWTORCombatParser.Utilities;
using Timer = System.Timers.Timer;

namespace SWTORCombatParser.Model.CombatParsing
{
    public enum CombatState
    {
        EnteredCombat,
        InCombat,
        ExitedCombat,
        ExitCombatDetected,
        ExitCombatDelayTimedOut,
        OutOfCombat,
        ExitedByEntering,
    }
    public class CombatDetector
    {
        private List<long> _bossesKilledThisCombat = new List<long>();
        private List<long> _bossesSeenThisCombat = new List<long>();
        private List<ulong> _combatResNames = new List<ulong> { 812826855735296, 808287075303424, 807217628446720, 814875555135488, 2940764107571200, 2940854301884416};
        private ulong _boonOfSpiritId = 3502674678906880;
        private bool _bossCombat;
        private BossInfo _currentBossInfo;
        private BossInfo _parsingBossInfo;
        public bool InCombat;
        private bool _justRevived;
        private List<Entity> revivedPlayers = new List<Entity>();
        private DateTime _inCombatStartTime;

        private Timer _timeoutTimer = new Timer();
        private DateTime _exitCombatDetectedTime;
        private bool _checkLogsForTimtout;
        private EncounterInfo _currentEncounter;

        public event Action<CombatState, bool> AlertExitCombatTimedOut = delegate { };

        public CombatDetector()
        {
            _timeoutTimer.AutoReset = false;
            _timeoutTimer.Elapsed += (_, _) => ExitCombatTimedOut(true);
        }

        public void Reset()
        {
            _inCombatStartTime = DateTime.MinValue;
            _exitCombatDetectedTime = DateTime.MinValue;
            _bossCombat = false;
            _bossesKilledThisCombat = new List<long>();
            _bossesSeenThisCombat = new List<long>();
            _parsingBossInfo = null;
            InCombat = false;
            _checkLogsForTimtout = false;
            _justRevived = false;
            _timeoutTimer.Stop();
            revivedPlayers = new List<Entity>();
        }
        public CombatState CheckForCombatState(ParsedLogEntry line, bool isRealTime = false)
        {
            // A replay polling batch can span many seconds of log time. Resolve
            // those boundaries in record order, without an asynchronous timer
            // changing detector state midway through that batch.
            var useEventTime = !isRealTime || LogReplayContext.IsActive;
            if (_checkLogsForTimtout)
            {
                if (line.Effect.EffectId == _7_0LogParsing._damageEffectId && line.Source.IsCharacter)
                {
                    if (!useEventTime)
                    {
                        RestartTimer();
                    }
                    else
                        _exitCombatDetectedTime = line.TimeStamp;
                }
                if ((line.TimeStamp - _exitCombatDetectedTime).TotalSeconds >= (_bossCombat ? 3 : 0.5) && useEventTime)
                {
                    ExitCombatTimedOut(isRealTime);
                }
            }
            if (line.Effect.EffectId == _7_0LogParsing.EnterCombatId)
            {
                if (!_justRevived || _currentEncounter.IsOpenWorld)
                {
                    if (InCombat)
                    {
                        if (_bossCombat)
                        {
                            _checkLogsForTimtout = false;
                            _timeoutTimer.Stop();
                            return CombatState.InCombat;
                        }
                        Reset();
                        InCombat = true;
                        return CombatState.ExitedByEntering;
                    }
                    _inCombatStartTime = line.TimeStamp;
                    _currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(line.TimeStamp);
                    Reset();
                    InCombat = true;
                    return CombatState.EnteredCombat;
                }
                _justRevived = false;
            }

            if (_currentEncounter == null)
            {
                _currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(line.TimeStamp);
            }
            if (_currentEncounter.BossInfos == null)
            {
                _bossCombat = false;
                _currentBossInfo = null;
            }

            if (_currentEncounter.BossInfos != null && line.Effect.EffectId == _7_0LogParsing._damageEffectId)
            {
                var targetIsBoss = false;
                var sourceIsBoss = false;
                foreach (var boss in _currentEncounter.BossInfos)
                {
                    if (!targetIsBoss)
                        targetIsBoss = boss.TargetIds.Contains(line.Target.LogId);
                    if (!sourceIsBoss)
                        sourceIsBoss = boss.TargetIds.Contains(line.Source.LogId);
                    if (targetIsBoss && sourceIsBoss)
                        break;
                }
                if (targetIsBoss && !_bossesSeenThisCombat.Contains(line.Target.LogId))
                    _bossesSeenThisCombat.Add(line.Target.LogId);
                if (sourceIsBoss && !_bossesSeenThisCombat.Contains(line.Source.LogId))
                    _bossesSeenThisCombat.Add(line.Source.LogId);
            }
            if (line.Effect.EffectId == _7_0LogParsing._damageEffectId && line.Target.LogId == 2857785339412480)
            {
                if (!_bossesSeenThisCombat.Contains(line.Target.LogId))
                    _bossesSeenThisCombat.Add(line.Target.LogId);
            }
            if (_bossesSeenThisCombat.Count > 0)
            {
                if (_bossesSeenThisCombat.Contains(2857785339412480))
                {
                    _parsingBossInfo ??= new BossInfo() { EncounterName = "Parsing", TargetIds = new List<long> { _bossesSeenThisCombat[0] } };
                    _currentBossInfo = _parsingBossInfo;
                    _bossCombat = true;
                }
                var encounterInfo = FindBossContainingSeenTargets();
                if (encounterInfo != null)
                {
                    _currentBossInfo = encounterInfo;
                    _bossCombat = true;
                }

            }
            if (_bossCombat && _combatResNames.Contains(line.AbilityId) && line.Effect.EffectId == _7_0LogParsing.AbilityActivateId)
            {
                revivedPlayers.Add(CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(line.Source, line.TimeStamp).Entity);
            }
            if (_bossCombat && _combatResNames.Contains(line.AbilityId) && line.Effect.EffectId == _7_0LogParsing.AbilityCancelId)
            {
                revivedPlayers.Remove(CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(line.Source, line.TimeStamp).Entity);
            }

            if (_bossCombat && line.AbilityId == _boonOfSpiritId)
            {
                revivedPlayers.Add(line.Source);
            }
            if (line.Effect.EffectId == _7_0LogParsing.RevivedCombatId &&
                ((_bossCombat && !WasRevivedInCombat(line.Source) && !_currentEncounter.IsOpenWorld) ||
                 (!_bossCombat && line.Source.IsLocalPlayer)))
            {
                revivedPlayers.Clear();
                return EndCombat();
            }
            if (_bossCombat && line.Effect.EffectId == _7_0LogParsing.RevivedCombatId)
            {
                for (var index = revivedPlayers.Count - 1; index >= 0; index--)
                    if (ReferenceEquals(revivedPlayers[index], line.Source))
                        revivedPlayers.RemoveAt(index);
                if (line.Source.IsLocalPlayer)
                    _justRevived = true;
            }
            if (line.Effect.EffectId == _7_0LogParsing.ExitCombatId && InCombat)
            {
                ExitCombatDetected(line, isRealTime, _bossCombat ? 3:0.5);
            }
            if (line.Effect.EffectId == _7_0LogParsing.DeathCombatId && !line.Target.IsCharacter && _currentBossInfo != null && InCombat)
            {
                var bossKilled = _currentBossInfo.TargetsRequiredForKill.Contains(line.Target.LogId);
                if (bossKilled)
                {
                    _bossesKilledThisCombat.Add(line.Target.LogId);
                    if (_currentBossInfo.TargetsRequiredForKill.All(n => _bossesKilledThisCombat.Contains(n)) || (_currentBossInfo.IsOpenWorld && _currentBossInfo.TargetIds.Contains(line.Target.LogId)))
                    {
                        return EndCombat();
                    }
                }
            }
            if (line.Effect.EffectId == _7_0LogParsing.DeathCombatId && line.Target.IsCharacter && InCombat)
            {
                if (AreAllParticipantsDead(line.TimeStamp))
                {
                    return EndCombat();
                }
            }

            if (InCombat && line.Effect.EffectType == EffectType.AreaEntered)
                return EndCombat();
            if (InCombat)
                return CombatState.InCombat;
            else
                return CombatState.OutOfCombat;

        }

        private BossInfo FindBossContainingSeenTargets()
        {
            if (_currentEncounter.BossInfos == null)
                return null;

            // This runs for every record in a boss fight. Iterate directly instead
            // of allocating nested LINQ predicates/enumerators. Keep inspecting the
            // current definitions so edits within the same encounter remain visible.
            foreach (var boss in _currentEncounter.BossInfos)
            {
                var containsAll = true;
                foreach (var target in _bossesSeenThisCombat)
                {
                    if (boss.TargetIds.Contains(target))
                        continue;
                    containsAll = false;
                    break;
                }
                if (containsAll)
                    return boss;
            }
            return null;
        }

        private bool WasRevivedInCombat(Entity player)
        {
            foreach (var revivedPlayer in revivedPlayers)
                if (ReferenceEquals(revivedPlayer, player))
                    return true;
            return false;
        }

        private bool AreAllParticipantsDead(DateTime logTime)
        {
            var characterClassUpdates = CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo;
            var charactersWhoChangedAfterCombatStart = characterClassUpdates
                .Where(kvp => kvp.Value.Keys.Any(k => k >= _inCombatStartTime && k <= logTime))
                .Select(kvp => kvp.Key).ToList();
            return charactersWhoChangedAfterCombatStart.Count > 0 && charactersWhoChangedAfterCombatStart
                .All(c => CombatLogStateBuilder.CurrentState.WasPlayerDeadAtTime(c, logTime));
        }

        private CombatState ExitCombatDetected(ParsedLogEntry log, bool isRealTime, double timeOutSec)
        {
            if (isRealTime && !LogReplayContext.IsActive)
            {
                _checkLogsForTimtout = true;
                _timeoutTimer.Interval = TimeSpan.FromSeconds(timeOutSec).TotalMilliseconds;
                _timeoutTimer.Start();
            }
            else
            {
                _exitCombatDetectedTime = log.TimeStamp;
                _checkLogsForTimtout = true;
            }
            return CombatState.ExitCombatDetected;
        }

        // Only called by the pinned replay reader on an empty frame, under its
        // combat lock. The writer watermark prevents buffered, due records from
        // being mistaken for an idle gap.
        internal void CheckReplayTimeout(long consumedBytes)
        {
            var deliveredThrough = LogReplayContext.GetDeliveredTime(consumedBytes);
            if (_checkLogsForTimtout && deliveredThrough.HasValue &&
                (deliveredThrough.Value - _exitCombatDetectedTime).TotalSeconds >= (_bossCombat ? 3 : 0.5))
                ExitCombatTimedOut(true);
        }
        private void RestartTimer()
        {
            if (_timeoutTimer.Enabled)
            {
                _timeoutTimer.Stop(); // Stop the timer if it's running
            }
            _timeoutTimer.Start(); // Start or restart the timer
        }
        private void ExitCombatTimedOut(bool realtime)
        {
            _checkLogsForTimtout = false;
            _justRevived = false;
            _timeoutTimer.Stop();
            InCombat = false;
            Reset();
            AlertExitCombatTimedOut.InvokeSafely(CombatState.ExitCombatDelayTimedOut,realtime);
        }

        private CombatState EndCombat()
        {
            _checkLogsForTimtout = false;
            _justRevived = false;
            _timeoutTimer.Stop();
            InCombat = false;
            Reset();
            return CombatState.ExitedCombat;
        }
    }
}
