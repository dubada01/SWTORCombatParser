using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.LogParsing
{
    public static class LogModifier
    {
        private static List<string> _effectsToIgnore = new List<string> { "Form", "Veteran's Edge","Revolster:", "Revivial", "Force Might", "Force Valor","Mines Detonated","Exhausted","Drained","Satiated","Technical Difficulties","Mark of Power" };
        private static List<string> _raidBuffNames = new List<string> { "Bloodthirst", "Unlimited Power", "Supercharged Celerity" };
        private static List<string> _offensiveBuffs = new List<string>
        {
            //general
            "Advanced Kyrprax Triage Adrenal",
            "Advanced Kyrprax Attack Adrenal",
            "Advanced Kyrprax Efficacy Adrenal",
            "Advanced Kyrprax Critical Adrenal",
            "Critical Surge",
            //sorc heals
            "Polarity Shift",
            "Recklessness",
            //guardian tank
            "Critical Focus: Residual Power",
            "Force Clarity",
            //gunslinger
            "Illegal Mods",
            "Smuggler's Luck",
            "Entrenched Offense"

        };
        private static List<string> _defensiveBuffs = new List<string>
        {
            "Advanced Kyrprax Shield Adrenal",
            "Supression",
            "Unnatrual Vigor",
            "Resurgence: Protected",
            "Warding Strike: Warding Power",
            "Blade Barrier",
            "Defensive Swings"

        };
        private static List<string> _tankCooldowns = new List<string>
        {
            "Focused Defense",
            "Warding Call",
            "Saber Refelect",
            "Saber Ward",
            "Blade Turning"
        };
        public static void UpdateLogWithState(ParsedLogEntry parsedLog, LogState state)
        {
            UpdateEffectiveHealing(parsedLog, state);
        }

        public static void StartUpdateOfModifiersActiveForLogs(List<ParsedLogEntry> logs,LogState state)
        {
            Task.Run(() => {
                Parallel.ForEach(logs, new ParallelOptions { MaxDegreeOfParallelism = 3 }, log =>
                 {
                     var modifiers = state.GetCombatModifiersAtTimeInvolvingParticipants(log.TimeStamp, log.Source, log.Target);
                     var usableModifiers = modifiers.Where(m => !_effectsToIgnore.Any(e => m.Name.Contains(e))).ToList();
                     if(usableModifiers.Any())
                        UpdateLogBuffs(log, usableModifiers);
                 });
            });
        }
        private static void UpdateLogBuffs(ParsedLogEntry log, List<CombatModifier> usableModifiers)
        {
            var buffs = usableModifiers.Where(m => _offensiveBuffs.Any(b => m.Name == b) || _raidBuffNames.Any(rb => m.Name == rb));
            var buffsForSource = buffs.Where(b => b.Target == log.Source);
            log.Value.Buffs = buffsForSource.ToList();

            var defbuffs = usableModifiers.Where(m => _defensiveBuffs.Any(b => m.Name == b) || _tankCooldowns.Any(tc => tc == m.Name));
            var defBuffsForTarget = defbuffs.Where(db => db.Target == log.Target);
            log.Value.DefensiveBuffs = defBuffsForTarget.ToList();
        }
        public static void UpdateEffectiveHealing(ParsedLogEntry parsedLog, LogState state)
        {
            if (parsedLog.Effect.EffectName == "Heal" && parsedLog.Source.IsCharacter)
            {
                var swtorClass = state.PlayerClasses.GetOrAdd(parsedLog.Source, e=> null);
                if (swtorClass == null)
                {
                    parsedLog.Value.EffectiveDblValue = parsedLog.Threat * state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source);
                    if (parsedLog.Value.EffectiveDblValue > parsedLog.Value.DblValue)
                    {
                        //OnNewLog("**************Impossible Heal! " +
                        //  "\nTime: " + parsedLog.TimeStamp +
                        //  "\nName: " + parsedLog.Ability +
                        //  "\nCalculated: " + parsedLog.Value.EffectiveDblValue +
                        //  "\nThreat: " + parsedLog.Threat +
                        //  "\nRaw: " + parsedLog.Value.DblValue +
                        //  "\nThreat Multiplier: " + state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source));
                        parsedLog.Value.EffectiveDblValue = parsedLog.Value.DblValue;
                    }
                    return;
                }

                var specialThreatAbilties = state.PlayerClasses[parsedLog.Source].SpecialThreatAbilities;

                var specialThreatAbilityUsed = specialThreatAbilties.FirstOrDefault(a => parsedLog.Ability.Contains(a.Name));

                if (parsedLog.Ability.Contains("Advanced") && parsedLog.Ability.Contains("Medpac") && state.PlayerClasses[parsedLog.Source].Role != Role.Tank)
                    specialThreatAbilityUsed = new Ability() { StaticThreat = true };

                var effectiveAmmount = 0d;

                if (specialThreatAbilityUsed == null)
                {
                    effectiveAmmount = parsedLog.Threat * state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source);
                }
                else
                {
                    if (specialThreatAbilityUsed.StaticThreat)
                        effectiveAmmount = parsedLog.Threat * 2d;
                    if (specialThreatAbilityUsed.Threatless)
                        effectiveAmmount = parsedLog.Value.DblValue;
                }

                parsedLog.Value.EffectiveDblValue = (int)effectiveAmmount;
                if (parsedLog.Value.EffectiveDblValue > parsedLog.Value.DblValue)
                {
                    //OnNewLog("**************Impossible Heal! " +
                    //      "\nTime: " + parsedLog.TimeStamp +
                    //      "\nName: " + parsedLog.Ability +
                    //      "\nCalculated: " + parsedLog.Value.EffectiveDblValue +
                    //      "\nThreat: " + parsedLog.Threat +
                    //      "\nRaw: " + parsedLog.Value.DblValue +
                    //      "\nThreat Multiplier: " + state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source));
                    parsedLog.Value.EffectiveDblValue = parsedLog.Value.DblValue;
                }
                return;
            }
            if (parsedLog.Effect.EffectName == "Heal" && parsedLog.Source.IsCompanion)
            {
                parsedLog.Value.EffectiveDblValue = parsedLog.Threat * 5;
            }
        }
    }
}
