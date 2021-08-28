using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Raiding;
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
        private static List<Entity> _currentEntities = new List<Entity>();
        public static event Action<string> OnNewLog = delegate { };
        public static event Action RaidingStarted = delegate { };
        public static event Action RaidingStopped = delegate { };
        public static RaidGroupInfo CurrentRaidGroup;
        public static void SetCurrentRaidGroup(RaidGroupInfo raidGroup)
        {
            CurrentRaidGroup = raidGroup;
            RaidingStarted();
        }
        public static void ClearRaidGroup()
        {
            RaidingStopped();
            CurrentRaidGroup = null;
        }

        public static LogState InitalizeStateFromLog(CombatLogFile file)
        {
            var lines = ParseAllLines(file);
            _logState = CombatLogStateBuilder.UpdateCurrentLogState(ref lines,file.Name);
            return _logState;
        }
        public static LogState GetCurrentLogState()
        {
            return _logState;
        }
        public static ParsedLogEntry ParseLine(string logEntry,long lineIndex)
        {
            var logEntryInfos = Regex.Matches(logEntry, @"\[.*?\]", RegexOptions.Compiled);

            var secondPart = logEntry.Split(']').Last();
            var value = Regex.Match(secondPart, @"\(.*?\)", RegexOptions.Compiled);
            var threat = Regex.Matches(secondPart, @"\<.*?\>", RegexOptions.Compiled);

            if (logEntryInfos.Count != 5 ||  string.IsNullOrEmpty(value.Value))
                return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };

            var parsedLine = ExtractInfo(logEntryInfos.Select(v => v.Value).ToArray(), value.Value, threat.Count == 0 ? "" : threat.Select(v => v.Value).First());
            parsedLine.LogText = logEntry;
            parsedLine.LogLineNumber = lineIndex;
            if (parsedLine.Source.Name == parsedLine.Target.Name && parsedLine.Source.IsCharacter)
                parsedLine.Source.IsPlayer = true;
            if (CurrentRaidGroup == null)
            {
                UpdateEffectiveHealValues(parsedLine, _logState);
            }
            return parsedLine;
        }
        public static List<ParsedLogEntry> ParseLast10Mins(CombatLogFile file)
        {
            var allLines = ParseAllLines(file);
            return allLines.Where(l => l.TimeStamp > allLines.Last().TimeStamp.AddMinutes(-10)).ToList();
        }
        public static List<ParsedLogEntry> GetAllCombatStartEvents(CombatLogFile log)
        {
            var allLines = ParseAllLines(log);
            return allLines.Where(l => l.Effect.EffectName=="EnterCombat").ToList();
        }
        private static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog)
        {
            _logDate = combatLog.Time;
            var logLines = combatLog.Data.Split('\n');
            var numberOfLines = logLines.Length;
            ParsedLogEntry[] parsedLog = new ParsedLogEntry[numberOfLines];
            for (var i = 0; i < numberOfLines; i++)
            {
                if (logLines[i] == "")
                    break;
                parsedLog[i] = ParseLine(logLines[i],i);
                parsedLog[i].LogName = combatLog.Name;
            }
            CombatTimestampRectifier.RectifyTimeStamps(parsedLog.Where(l => l != null).ToList());
            return parsedLog.Where(l => l != null).OrderBy(l => l.TimeStamp).ToList();
        }



        public static void UpdateEffectiveHealValues(ParsedLogEntry parsedLog, LogState state)
        {
            if(parsedLog.Effect.EffectName == "Heal" && (parsedLog.Source.IsPlayer || parsedLog.Source.IsCompanion))
            {
                if (state.PlayerClass == null)
                { 
                    parsedLog.Value.EffectiveDblValue = parsedLog.Threat * state.GetCurrentHealsPerThreat(parsedLog.TimeStamp);
                    if (parsedLog.Value.EffectiveDblValue > parsedLog.Value.DblValue)
                    {
                        OnNewLog("**************Impossible Heal! " +
                          "\nTime: " + parsedLog.TimeStamp +
                          "\nName: " + parsedLog.Ability +
                          "\nCalculated: " + parsedLog.Value.EffectiveDblValue +
                          "\nThreat: " + parsedLog.Threat +
                          "\nRaw: " + parsedLog.Value.DblValue +
                          "\nThreat Multiplier: " + state.GetCurrentHealsPerThreat(parsedLog.TimeStamp));
                        parsedLog.Value.EffectiveDblValue = parsedLog.Value.DblValue;
                    }
                    return;
                }

                var specialThreatAbilties = state.PlayerClass.SpecialThreatAbilities;

                var specialThreatAbilityUsed = specialThreatAbilties.FirstOrDefault(a => parsedLog.Ability.Contains(a.Name));

                if (parsedLog.Ability.Contains("Advanced")&&parsedLog.Ability.Contains("Medpac") && state.PlayerClass.Role != Role.Tank)
                    specialThreatAbilityUsed = new Ability() { StaticThreat = true };

                var effectiveAmmount = 0d;

                if (specialThreatAbilityUsed == null)
                {
                    effectiveAmmount = parsedLog.Threat * state.GetCurrentHealsPerThreat(parsedLog.TimeStamp);
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
                {
                    OnNewLog("**************Impossible Heal! " +
                          "\nTime: " + parsedLog.TimeStamp +
                          "\nName: " + parsedLog.Ability +
                          "\nCalculated: " + parsedLog.Value.EffectiveDblValue +
                          "\nThreat: " + parsedLog.Threat +
                          "\nRaw: " + parsedLog.Value.DblValue +
                          "\nThreat Multiplier: " + state.GetCurrentHealsPerThreat(parsedLog.TimeStamp));
                    parsedLog.Value.EffectiveDblValue = parsedLog.Value.DblValue;
                }
                return;
            }
            if (parsedLog.Effect.EffectName == "Heal" && parsedLog.Target.IsPlayer)
            {
                parsedLog.Value.EffectiveDblValue = parsedLog.Threat * (2/0.9d);
            }
        }


        private static ParsedLogEntry ExtractInfo(string[] entryInfo, string value, string threat)
        {
            if (entryInfo.Length == 0)
                return null;

            var newEntry = new ParsedLogEntry();
            var time = DateTime.Parse(CleanString(entryInfo[0]));
            var date = new DateTime(_logDate.Year, _logDate.Month, _logDate.Day);

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
            if (valueParts.Length == 1)
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
            }
            if (valueParts.Length == 3)
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = newValue.DblValue;
                newValue.ValueType = GetValueType(valueParts[1].Replace("-", ""));
            }
            if(valueParts.Length == 4)
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = newValue.DblValue;
                newValue.ValueType = GetValueType(valueParts[1].Replace("-", ""));
            }
            if (valueParts.Length == 6)
            {
                var modifier = new Value();
                modifier.ValueType = GetValueType(valueParts[4].Replace("-", ""));
                modifier.DblValue = double.Parse(valueParts[3].Replace("(", ""));
                modifier.EffectiveDblValue = modifier.DblValue;
                newValue.Modifier = modifier;

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.EffectiveDblValue = double.Parse(valueParts[0].Replace("*", ""))-modifier.EffectiveDblValue;
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.ValueType = GetValueType(valueParts[1]);
            }
            if (valueParts.Length == 8)
            {

                var modifier = new Value();
                modifier.ValueType = GetValueType(valueParts[3].Replace("-", ""));

                modifier.DblValue = double.Parse(valueParts[5].Replace("(", ""));
                modifier.EffectiveDblValue = Math.Min(double.Parse(valueParts[0].Replace("*", "")),  modifier.DblValue);
                newValue.Modifier = modifier;

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = double.Parse(valueParts[0].Replace("*", ""))-modifier.EffectiveDblValue;
                newValue.ValueType = GetValueType(valueParts[1]);

            }

            return newValue;
        }
        private static Entity ParseEntity(string value)
        {
            if (value.Contains("@") && !value.Contains(":"))
            {
                var characterName = value.Replace("@", "");
                var existingCharacterEntity = _currentEntities.FirstOrDefault(e => e.Name == characterName);
                if (existingCharacterEntity != null)
                    return existingCharacterEntity;
                var characterEntity = new Entity() { IsCharacter = true, Name =  characterName};
                _currentEntities.Add(characterEntity);
                return characterEntity;
            }
            if (value.Contains("@") && value.Contains(":"))
            {
                var compaionName = value.Split(':')[1];
                var companionNameComponents = compaionName.Split('{');
                var existingCompanionEntity = _currentEntities.FirstOrDefault(e => e.Name == companionNameComponents[0].Trim());
                if (existingCompanionEntity != null)
                    return existingCompanionEntity;
                var companion = new Entity() { IsCharacter = false,IsCompanion = true, Name = companionNameComponents[0].Trim() };
                _currentEntities.Add(companion);
                return companion;
            }
            var splitVal = value.Split('{');
            var entityName = splitVal[0].Trim();
            var existingEntity = _currentEntities.FirstOrDefault(e => e.Name == entityName);
            if (existingEntity != null)
                return existingEntity;

            var newEntity = new Entity();
            newEntity.Name = entityName;
            _currentEntities.Add(newEntity);
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
