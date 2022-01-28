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
        private static List<string> _bossesKilledThisCombat = new List<string>();
        private static List<string> _combatResNames = new List<string> { "Revival", "Reanimation", "Heartrigger Patch", "Resuscitation Probe", "Emergency Medical Probe" ,"Onboard AED"};
        private static bool _bossCombat;
        private static bool _isInCombat;
        private static bool _isCombatResOut;
        private static void Reset()
        {
            _bossCombat = false;
            _bossesKilledThisCombat = new List<string>();
            _isInCombat = false;
        }
        public static CombatState CheckForCombatState(ParsedLogEntry line)
        {
            if (line.Effect.EffectName == "EnterCombat")
            {
                if (_isInCombat)
                {
                    Reset();
                    _isInCombat = true;
                    return CombatState.ExitedByEntering;
                }
                Reset();
                _isInCombat = true;
                return CombatState.EnteredCombat;
            }
            var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(line.TimeStamp);
            if (currentEncounter.BossInfos == null)
                _bossCombat = false;
            if (currentEncounter.BossInfos != null && currentEncounter.BossInfos.Any(b => b.TargetNames.Contains(line.Target.Name) || b.TargetNames.Contains(line.Source.Name)))
                _bossCombat = true;
            if (_bossCombat && _combatResNames.Contains(line.Ability))
            {
                _isCombatResOut = true;
            }
            if (_bossCombat && !_isCombatResOut && line.Effect.EffectName == "Revived")
            {
                return EndCombat();
            }
            if (_bossCombat && _isCombatResOut && line.Effect.EffectName == "Revived")
            {
                _isCombatResOut = false;
            }
            if (line.Effect.EffectName == "ExitCombat" && _isInCombat)
            {
                if (CombatLogStateBuilder.CurrentState.LogVersion == LogVersion.Legacy || !_bossCombat)
                {
                    return EndCombat();
                }
            }
            if (line.Effect.EffectName == "Death" && !line.Target.IsCharacter && currentEncounter.BossInfos != null &&  _isInCombat)
            {
                var bossKilled = currentEncounter.BossInfos.FirstOrDefault(bi => bi.TargetNames.Contains(line.Target.Name));
                if (bossKilled != null)
                {
                    _bossesKilledThisCombat.Add(bossKilled.TargetNames.First(tn => tn == line.Target.Name));
                    if (currentEncounter.BossInfos.Any(bi => bi.TargetNames.All(n => _bossesKilledThisCombat.Contains(n))))
                    {
                        return EndCombat();
                    }
                }
            }
            if (line.Effect.EffectName == "Death" && line.Target.IsCharacter && _isInCombat)
            {
                if (CombatLogStateBuilder.CurrentState.LogVersion == LogVersion.NextGen)
                {
                    var characterDeathStates = CombatLogStateBuilder.CurrentState.PlayerDeathChangeInfo;
                    var logTime = line.TimeStamp;
                    var allDead = characterDeathStates.Keys.All(c => CombatLogStateBuilder.CurrentState.WasPlayerDeadAtTime(c, logTime));
                    if (allDead)
                    {
                        return EndCombat();
                    }
                }
                else
                {
                    return EndCombat();
                }

            }

            if (_isInCombat)
                return CombatState.InCombat;
            else
                return CombatState.OutOfCombat;

        }


        private static CombatState EndCombat()
        {
            _isInCombat = false;
            return CombatState.ExitedCombat;
        }
    }
}
