﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
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
    public static class CombatDetector
    {
        private static List<string> _bossesKilledThisCombat = new List<string>();
        private static List<string> _bossesSeenThisCombat = new List<string>();
        private static List<string> _combatResNames = new List<string> { "812826855735296", "808287075303424", "807217628446720", "814875555135488", "2940764107571200", "2940854301884416" };
        private static string _boonOfSpiritId = "3502674678906880";
        private static bool _bossCombat;
        private static BossInfo _currentBossInfo;
        public static bool InCombat;
        private static bool _justRevived;
        private static List<Entity> revivedPlayers = new List<Entity>();
        private static DateTime _inCombatStartTime;

        private static Timer _timeoutTimer = new Timer();
        private static DateTime _exitCombatDetectedTime;
        private static bool _checkLogsForTimtout;
        private static EncounterInfo _currentEncounter;

        public static event Action<CombatState> AlertExitCombatTimedOut = delegate { };

        public static void Reset()
        {
            _inCombatStartTime = DateTime.MinValue;
            _exitCombatDetectedTime = DateTime.MinValue;
            _bossCombat = false;
            _bossesKilledThisCombat = new List<string>();
            _bossesSeenThisCombat = new List<string>();
            InCombat = false;
            _checkLogsForTimtout = false;
            _justRevived = false;
            _timeoutTimer.Stop();
            revivedPlayers = new List<Entity>();
        }
        public static CombatState CheckForCombatState(ParsedLogEntry line, bool isRealTime = false)
        {
            if (_checkLogsForTimtout)
            {
                if (line.Effect.EffectId == _7_0LogParsing._damageEffectId && line.Source.IsCharacter)
                {
                    if (isRealTime)
                    {
                        Debug.WriteLine($"Restarting at: {(_inCombatStartTime - line.TimeStamp).TotalSeconds}");
                        RestartTimer();
                    }
                    else
                        _exitCombatDetectedTime = line.TimeStamp;
                }
                if ((line.TimeStamp - _exitCombatDetectedTime).TotalSeconds >= (_bossCombat ? 3 : 0.5) && !isRealTime)
                {
                    ExitCombatTimedOut(null, null);
                }
            }
            if (line.Effect.EffectId == _7_0LogParsing.EnterCombatId)
            {
                if (!_justRevived)
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
                if (_currentEncounter.BossInfos.Any(b => b.TargetIds.Contains(line.Target.LogId.ToString())))
                {
                    if (!_bossesSeenThisCombat.Contains(line.Target.LogId.ToString()))
                        _bossesSeenThisCombat.Add(line.Target.LogId.ToString());
                }
                if (_currentEncounter.BossInfos.Any(b => b.TargetIds.Contains(line.Source.LogId.ToString())))
                {
                    if (!_bossesSeenThisCombat.Contains(line.Source.LogId.ToString()))
                        _bossesSeenThisCombat.Add(line.Source.LogId.ToString());
                }
            }
            if (line.Effect.EffectId == _7_0LogParsing._damageEffectId && line.Target.LogId == 2857785339412480)
            {
                if (!_bossesSeenThisCombat.Contains(line.Target.LogId.ToString()))
                    _bossesSeenThisCombat.Add(line.Target.LogId.ToString());
            }
            if (_bossesSeenThisCombat.Count > 0)
            {
                if (_bossesSeenThisCombat.Any(b => b == "2857785339412480"))
                {
                    _currentBossInfo = new BossInfo() { EncounterName = "Parsing", TargetIds = new List<string> { _bossesSeenThisCombat.First() } };
                    _bossCombat = true;
                }
                var encounterInfo = _currentEncounter.BossInfos?.FirstOrDefault(b => _bossesSeenThisCombat.All(sb => b.TargetIds.Contains(sb)));
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
            if ((_bossCombat && revivedPlayers.All(c => c != line.Source) && line.Effect.EffectId == _7_0LogParsing.RevivedCombatId && !_currentEncounter.IsOpenWorld) || (!_bossCombat && line.Effect.EffectId == _7_0LogParsing.RevivedCombatId && line.Source.IsLocalPlayer))
            {
                revivedPlayers.Clear();
                return EndCombat();
            }
            if (_bossCombat && line.Effect.EffectId == _7_0LogParsing.RevivedCombatId)
            {
                revivedPlayers.RemoveAll(c => c == line.Source);
                if (line.Source.IsLocalPlayer)
                    _justRevived = true;
            }
            if (line.Effect.EffectId == _7_0LogParsing.ExitCombatId && InCombat)
            {
                ExitCombatDetected(line, isRealTime, _bossCombat ? 3:0.5);
            }
            if (line.Effect.EffectId == _7_0LogParsing.DeathCombatId && !line.Target.IsCharacter && _currentBossInfo != null && InCombat)
            {
                var bossKilled = _currentBossInfo.TargetsRequiredForKill.Contains(line.Target.LogId.ToString());
                if (bossKilled)
                {
                    _bossesKilledThisCombat.Add(line.Target.LogId.ToString());
                    if (_currentBossInfo.TargetsRequiredForKill.All(n => _bossesKilledThisCombat.Contains(n)) || (_currentBossInfo.IsOpenWorld && _currentBossInfo.TargetIds.Any(t=>t == line.Target.LogId.ToString())))
                    {
                        return EndCombat();
                    }
                }
            }
            if (line.Effect.EffectId == _7_0LogParsing.DeathCombatId && line.Target.IsCharacter && InCombat)
            {
                var characterClassUpdates = CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo;
                var charactersWhoChangedAfterCombatStart = characterClassUpdates.Where(kvp => kvp.Value.Keys.Any(k => k >= _inCombatStartTime && k <= line.TimeStamp)).Select(kvp => kvp.Key).ToList();
                var logTime = line.TimeStamp;
                var allDead = charactersWhoChangedAfterCombatStart.All(c => CombatLogStateBuilder.CurrentState.WasPlayerDeadAtTime(c, logTime));
                if (allDead && charactersWhoChangedAfterCombatStart.Count > 0)
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
        private static CombatState ExitCombatDetected(ParsedLogEntry log, bool isRealTime, double timeOutSec)
        {
            if (isRealTime)
            {
                _checkLogsForTimtout = true;
                _timeoutTimer.Interval = TimeSpan.FromSeconds(timeOutSec).TotalMilliseconds;
                _timeoutTimer.Elapsed += ExitCombatTimedOut;
                _timeoutTimer.AutoReset = false;
                _timeoutTimer.Start();
            }
            else
            {
                _exitCombatDetectedTime = log.TimeStamp;
                _checkLogsForTimtout = true;
            }
            return CombatState.ExitCombatDetected;
        }
        private static void RestartTimer()
        {
            if (_timeoutTimer.Enabled)
            {
                _timeoutTimer.Stop(); // Stop the timer if it's running
            }
            _timeoutTimer.Start(); // Start or restart the timer
        }
        private static void ExitCombatTimedOut(object sender, ElapsedEventArgs args)
        {
            _checkLogsForTimtout = false;
            _justRevived = false;
            _timeoutTimer.Stop();
            InCombat = false;
            Reset();
            AlertExitCombatTimedOut(CombatState.ExitCombatDelayTimedOut);
        }

        private static CombatState EndCombat()
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
