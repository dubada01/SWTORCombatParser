using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SWTORCombatParser
{

    public static class CombatLogParser
    {
        private static DateTime _logDate;

        private static LogState _logState = new LogState();
        public static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog)
        {
            _logDate = combatLog.Time;
            var logLines = combatLog.Data.Split('\n');
            var numberOfLines = logLines.Length;
            ParsedLogEntry[] parsedLog = new ParsedLogEntry[numberOfLines];
            for (var i = 0; i < numberOfLines; i++)
            {
                if (logLines[i] == "")
                    break;
                parsedLog[i] = ParseLine(logLines[i]); 
                parsedLog[i].LogName = combatLog.Name;
            }
            return parsedLog.Where(l=>l!=null).OrderBy(l=>l.TimeStamp).ToList();
        }
        public static LogState BuildLogState(CombatLogFile file)
        {
            var lines = ParseAllLines(file);
            _logState = CombatLogStateBuilder.GetStateDuringLog(ref lines);
            return _logState;
        }

        public static ParsedLogEntry ParseLine(string logEntry)
        {
            var logEntryInfos = Regex.Matches(logEntry, @"\[.*?\]", RegexOptions.Compiled);

            var secondPart = logEntry.Split(']').Last();
            var value = Regex.Matches(secondPart, @"\(.*?\)", RegexOptions.Compiled);
            var threat = Regex.Matches(secondPart, @"\<.*?\>", RegexOptions.Compiled);

            if (logEntryInfos.Count != 5 || value.Count == 0 || string.IsNullOrEmpty(value[0].Value))
                return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };

            var parsedLine = ExtractInfo(logEntryInfos.Select(v => v.Value).ToArray(), value.Select(v => v.Value).First(), threat.Count == 0 ? "" : threat.Select(v => v.Value).First());
            if (parsedLine.Target.Name == _logState.PlayerName)
                parsedLine.Target.IsPlayer = true;
            if (parsedLine.Source.Name == _logState.PlayerName)
                parsedLine.Source.IsPlayer = true;
            parsedLine.LogText = logEntry;
            UpdateEffectiveHealValues(parsedLine);
            return parsedLine;
        }
        private static void UpdateEffectiveHealValues(ParsedLogEntry parsedLog)
        {
            if(parsedLog.Effect.EffectName == "Heal" && parsedLog.Source.IsPlayer)
            {
                if (_logState.PlayerClass == null)
                { 
                    parsedLog.Value.EffectiveDblValue = parsedLog.Threat * _logState.GetCurrentHealsPerThreat(parsedLog.TimeStamp);
                    if (parsedLog.Value.EffectiveDblValue > parsedLog.Value.DblValue)
                        throw new Exception();
                    return;
                }

                var specialThreatAbilties = _logState.PlayerClass.SpecialThreatAbilities;

                var specialThreatAbilityUsed = specialThreatAbilties.FirstOrDefault(a => a.Name == parsedLog.Ability);

                var effectiveAmmount = 0d;

                if (specialThreatAbilityUsed == null)
                {
                    effectiveAmmount = parsedLog.Threat * _logState.GetCurrentHealsPerThreat(parsedLog.TimeStamp);
                }
                else
                { 
                    if(specialThreatAbilityUsed.StaticThreat)
                        effectiveAmmount = parsedLog.Threat * 2d;
                    if (specialThreatAbilityUsed.Threatless)
                        effectiveAmmount = parsedLog.Value.DblValue;
                }

                parsedLog.Value.EffectiveDblValue = (int)effectiveAmmount;
                if (parsedLog.Value.EffectiveDblValue > parsedLog.Value.DblValue)
                    throw new Exception();
            }
        }


        private static ParsedLogEntry ExtractInfo(string[] entryInfo, string value, string threat)
        {
            if (entryInfo.Length == 0)
                return null;

            var newEntry = new ParsedLogEntry();

            var date = new DateTime(_logDate.Year, _logDate.Month, _logDate.Day);
            var time = DateTime.Parse(CleanString(entryInfo[0]));
            var newDate = date.Add(new TimeSpan(0, time.Hour, time.Minute, time.Second, time.Millisecond));
            newEntry.TimeStamp = newDate;
            newEntry.Source = ParseEntity(CleanString(entryInfo[1]));
            newEntry.Target = ParseEntity(CleanString(entryInfo[2]));
            newEntry.Ability = ParseAbility(CleanString(entryInfo[3]));
            newEntry.Effect = ParseEffect(CleanString(entryInfo[4]));

            newEntry.Value = ParseValues(value, newEntry.Effect);
            newEntry.Threat =string.IsNullOrEmpty(threat) ? 0 : int.Parse(threat.Replace("<","").Replace(">",""));
            return newEntry;
        }
        private static Value ParseValues(string valueString, Effect currentEffect)
        {
            if(currentEffect.EffectType == EffectType.Apply && (currentEffect.EffectName == "Damage" || currentEffect.EffectName == "Heal"))
                return ParseDamageValue(valueString);
            if (currentEffect.EffectType == EffectType.Restore || currentEffect.EffectType == EffectType.Spend)
                return ParseResourceEventValue(valueString);
            if (currentEffect.EffectType == EffectType.Event)
                return new Value() {StrValue = valueString.Replace("(", "").Replace(")","") };
            return new Value();
        }
        private static Value ParseResourceEventValue(string resourceString)
        {
            var cleanValue = resourceString.Replace("(", "").Replace(")", "");
            return new Value() { DblValue = double.Parse(cleanValue) };
        }
        private static Value ParseDamageValue(string damageValueString)
        {
            var newValue = new Value();
            var valueParts = damageValueString.Replace("(", string.Empty).Replace(")", string.Empty).Split(' ');

            if (valueParts.Length == 0)
                return newValue;
            if(valueParts.Length == 1)
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
            }
            if (valueParts.Length == 3)
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.DamageType = GetValueType(valueParts[1].Replace("-", ""));
            }
            if (valueParts.Length == 6)
            {

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.DamageType = GetValueType(valueParts[1]);

                var modifier = new Value();
                modifier.DamageType = GetValueType(valueParts[4].Replace("-", ""));
                modifier.DblValue = double.Parse(valueParts[3].Replace("(", ""));
                newValue.Modifier = modifier;
            }
            return newValue;
        }
        private static Entity ParseEntity(string value)
        {
            if (value.Contains("@") && !value.Contains(":"))
            {

                    var _characterEntity = new Entity() { IsCharacter = true, Name = value.Replace("@", "") };
                return _characterEntity;
            }
            if (value.Contains("@") && value.Contains(":"))
            {
                var companion = new Entity() { IsCharacter = false,IsCompanion = true, Name = value.Split(':')[1] };
                return companion;
            }
            var newEntity = new Entity();
            var splitVal = value.Split('{');
            newEntity.Name = splitVal[0].Trim();
            return newEntity;
        }
        private static string ParseAbility(string value)
        {
            if (value == "")
                return "";
            var splitVal = value.Split('{');
            return splitVal[0].Trim();
        }
        private static Effect ParseEffect(string value)
        {
            var split = value.Split(':');
            var type = split[0];
            var name = split[1];
            var newEffect = new Effect();

            var splitName = name.Split('{');
            newEffect.EffectName = splitName[0].Trim();

            newEffect.EffectType = GetEffectType(type.Split('{')[0].Trim());

            return newEffect;
        }
        private static DamageType GetValueType(string val)
        {
            switch (val)
            {        
                case "energy":
                    return DamageType.energy;
                case "kinetic":
                    return DamageType.kinetic;
                case "internal":
                    return DamageType.intern;
                case "elemental":
                    return DamageType.elemental;
                case "shield":
                    return DamageType.shield;
                case "absorbed":
                    return DamageType.absorbed;
                case "miss":
                    return DamageType.miss;
                case "parry":
                    return DamageType.parry;
                case "deflect":
                    return DamageType.deflect;
                case "dodge":
                    return DamageType.dodge;
                case "immune":
                    return DamageType.immune;
                case "resist":
                    return DamageType.resist;
                case "cover":
                    return DamageType.cover;
                default:
                    return DamageType.unknown;
            }
        }
        private static EffectType GetEffectType(string v)
        {
            switch (v)
            {
                case "ApplyEffect":
                    return EffectType.Apply;
                case "RemoveEffect":
                    return EffectType.Remove;
                case "Event":
                    return EffectType.Event;
                case "Spend":
                    return EffectType.Spend;
                case "Restore":
                    return EffectType.Restore;
                default:
                    throw new Exception("No valid type");
            }
        }

        private static string CleanString(string input)
        {
            return input.Replace("[", "").Replace("]", "");
        }
    }
}
