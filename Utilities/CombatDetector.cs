using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public enum CombatState
    {
        EnteredCombat,
        InCombat,
        ExitedCombat,
        OutOfCombat, 
        ExitedByEntering,
    }
    public static class CombatDetector
    {
        private static DateTime _combatEndTime = DateTime.MinValue;
        private static List<string> _bossesKilledThisCombat = new List<string>();
        private static List<string> _combatResNames = new List<string> { "Revival", "Reanimation", "Heartrigger Patch", "Resuscitation Probe", "Emergency Medical Probe" ,"Onboard AED"};
        private static bool _bossCombat;
        private static bool _combatEnding;
        private static bool _isInCombat;
        private static bool _isCombatResOut;
        private static void Reset()
        {
            _combatEnding = false;
            _bossCombat = false;
            _bossesKilledThisCombat = new List<string>();
            _isInCombat = true;
        }
        public static CombatState CheckForCombatState(ParsedLogEntry line, long lineIndex = -1, long numberOfLines = -1)
        {
            if (!_isInCombat && line.Effect.EffectName == "EnterCombat")
            {
                if (_combatEnding)
                {
                    Reset();
                    return CombatState.ExitedByEntering; 
                }
                Reset();
                return CombatState.EnteredCombat;
            }
            var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(line.TimeStamp);
            if (currentEncounter.BossInfos == null)
                _bossCombat = false;
            if (currentEncounter.BossInfos != null && currentEncounter.BossInfos.Any(b => b.TargetNames.Contains(line.Target.Name) || b.TargetNames.Contains(line.Source.Name)))
                _bossCombat = true;
            if(_bossCombat && _combatResNames.Contains(line.Ability))
            {
                _isCombatResOut = true;
            }
            if (_bossCombat && !_isCombatResOut && line.Effect.EffectName == "Revived")
            {
                _combatEnding = true;
                _combatEndTime = line.TimeStamp;
            }
            if (_bossCombat && _isCombatResOut && line.Effect.EffectName == "Revived")
            {
                _isCombatResOut = false;
            }
            if (line.Effect.EffectName == "ExitCombat" && !_combatEnding && _isInCombat)
            {
                if (CombatLogStateBuilder.CurrentState.LogVersion == LogVersion.Legacy || !_bossCombat)
                {
                    _combatEnding = true;
                    _combatEndTime = line.TimeStamp;
                }
            }
            if (line.Effect.EffectName == "Death" && !line.Target.IsCharacter && currentEncounter.BossInfos != null && !_combatEnding && _isInCombat)
            {

                var bossKilled = currentEncounter.BossInfos.FirstOrDefault(bi => bi.TargetNames.Contains(line.Target.Name));
                if (bossKilled != null)
                {
                    _bossesKilledThisCombat.Add(bossKilled.TargetNames.First(tn => tn == line.Target.Name));
                    if (currentEncounter.BossInfos.Any(bi => bi.TargetNames.All(n => _bossesKilledThisCombat.Contains(n))))
                    {
                        _combatEnding = true;
                        _combatEndTime = line.TimeStamp;
                    }
                }

            }
            if (line.Effect.EffectName == "Death" && line.Target.IsCharacter && !_combatEnding && _isInCombat)
            {
                if (CombatLogStateBuilder.CurrentState.LogVersion == LogVersion.NextGen)
                {
                    var characterDeathStates = CombatLogStateBuilder.CurrentState.PlayerDeathChangeInfo;
                    var logTime = line.TimeStamp;
                    var allDead = characterDeathStates.Keys.All(c => CombatLogStateBuilder.CurrentState.WasPlayerDeadAtTime(c, logTime));
                    if (allDead)
                    {
                        _combatEnding = true;
                        _combatEndTime = line.TimeStamp;
                    }
                }
                else
                {
                    _combatEnding = true;
                    _combatEndTime = line.TimeStamp;
                }

            }
            var hasCombatEnded = CheckForCombatEnd(line.TimeStamp, lineIndex, numberOfLines);
            if (hasCombatEnded)
                return CombatState.ExitedCombat;
            else
            {
                if (_isInCombat)
                    return CombatState.InCombat;
                else
                    return CombatState.OutOfCombat;
            }
        }
        private static bool CheckForCombatEnd(DateTime currentLogTime, long lineIndex, long numberOfLines)
        {
            if(lineIndex == -1)
            {
                if ((currentLogTime - _combatEndTime).TotalSeconds > 2 && _combatEnding)
                {
                    _isInCombat = false;
                    _combatEnding = false;
                    return true;
                }
                return false;
            }
            else
            {
                if (((currentLogTime - _combatEndTime).TotalSeconds > 2 || (lineIndex == numberOfLines - 1)) && _combatEnding)
                {

                    _isInCombat = false;
                    _combatEnding = false;
                    return true;
                }
                return false;
            }
        }
    }
}
