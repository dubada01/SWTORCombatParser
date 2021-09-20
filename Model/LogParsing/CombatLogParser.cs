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
        public static void SetCurrentState(LogState currentState)
        {
            _logState = currentState;
        }
        public static ParsedLogEntry ParseLine(string logEntry,long lineIndex,bool buildingState)
        {
            try
            {
                var logEntryInfos = Regex.Matches(logEntry, @"\[.*?\]", RegexOptions.Compiled);

                var secondPart = logEntry.Split(']').Last();
                var value = Regex.Match(secondPart, @"\(.*?\)", RegexOptions.Compiled);
                var threat = Regex.Matches(secondPart, @"\<.*?\>", RegexOptions.Compiled);

                if (logEntryInfos.Count < 5 || string.IsNullOrEmpty(value.Value))
                    return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };
                if (logEntry.Contains("v7."))
                {
                    ParseLogStartLine(logEntryInfos.Select(v => v.Value).ToArray(), value.Value);
                    return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };
                }
                var parsedLine = ExtractInfo(logEntryInfos.Select(v => v.Value).ToArray(), value.Value, threat.Count == 0 ? "" : threat.Select(v => v.Value).First());
                parsedLine.LogText = logEntry;
                parsedLine.LogLineNumber = lineIndex;
                if (!is7_0Logs && parsedLine.Source == parsedLine.Target && parsedLine.Source.IsCharacter)
                    parsedLine.Source.IsLocalPlayer = true;
                if(!buildingState)
                    UpdateEffectiveHealValues(parsedLine, _logState);

                return parsedLine;
            }
            catch (Exception e)
            {
                OnNewLog(e.Message);
                return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };
            }
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
                var parsedLine= ParseLine(logLines[i], i,true);
                if (parsedLine.Error == ErrorType.IncompleteLine)
                    continue;
                parsedLog[i] = parsedLine;
                parsedLog[i].LogName = combatLog.Name;
            }
            CombatTimestampRectifier.RectifyTimeStamps(parsedLog.Where(l => l != null).ToList());
            return parsedLog.Where(l => l != null).OrderBy(l => l.TimeStamp).ToList();
        }



        public static void UpdateEffectiveHealValues(ParsedLogEntry parsedLog, LogState state)
        {
            if(parsedLog.Effect.EffectName == "Heal" && parsedLog.Source.IsCharacter)
            {
                if (state.PlayerClasses[parsedLog.Source] == null)
                { 
                    parsedLog.Value.EffectiveDblValue = parsedLog.Threat * state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source);
                    if (parsedLog.Value.EffectiveDblValue > parsedLog.Value.DblValue)
                    {
                        OnNewLog("**************Impossible Heal! " +
                          "\nTime: " + parsedLog.TimeStamp +
                          "\nName: " + parsedLog.Ability +
                          "\nCalculated: " + parsedLog.Value.EffectiveDblValue +
                          "\nThreat: " + parsedLog.Threat +
                          "\nRaw: " + parsedLog.Value.DblValue +
                          "\nThreat Multiplier: " + state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source));
                        parsedLog.Value.EffectiveDblValue = parsedLog.Value.DblValue;
                    }
                    return;
                }

                var specialThreatAbilties = state.PlayerClasses[parsedLog.Source].SpecialThreatAbilities;

                var specialThreatAbilityUsed = specialThreatAbilties.FirstOrDefault(a => parsedLog.Ability.Contains(a.Name));

                if (parsedLog.Ability.Contains("Advanced")&&parsedLog.Ability.Contains("Medpac") && state.PlayerClasses[parsedLog.Source].Role != Role.Tank)
                    specialThreatAbilityUsed = new Ability() { StaticThreat = true };

                var effectiveAmmount = 0d;

                if (specialThreatAbilityUsed == null)
                {
                    effectiveAmmount = parsedLog.Threat * state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source);
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
                          "\nThreat Multiplier: " + state.GetCurrentHealsPerThreat(parsedLog.TimeStamp, parsedLog.Source));
                    parsedLog.Value.EffectiveDblValue = parsedLog.Value.DblValue;
                }
                return;
            }
            if(parsedLog.Effect.EffectName == "Heal" && parsedLog.Source.IsCompanion)
            {
                parsedLog.Value.EffectiveDblValue = parsedLog.Threat * (5);
            }
            //if (parsedLog.Effect.EffectName == "Heal" && parsedLog.Target.IsPlayer)
            //{
            //    parsedLog.Value.EffectiveDblValue = parsedLog.Threat * (2/0.9d);
            //}
        }
        private static bool is7_0Logs = false;
        private static void ParseLogStartLine(string[] entryInfos, string version)
        {
            is7_0Logs = true;
            var player = _currentEntities.FirstOrDefault(e => CleanString(entryInfos[2]).Split(':')[0].Split('#')[0].Replace("@", "") == e.Name);
            if (player == null)
                ParseEntity(entryInfos[2], true);
            else
                player.IsLocalPlayer = true;
        }
        private static ParsedLogEntry ExtractInfo(string[] entryInfo, string value, string threat)
        {
            if (entryInfo.Length == 0)
                return null;

            var newEntry = new ParsedLogEntry();

            var extractionOffset = entryInfo.Count() == 6 ? 0 : 1;
            if(extractionOffset == 0)
                newEntry.Position= ParsePositionData(entryInfo[0]);
            var time = DateTime.Parse(CleanString(entryInfo[1-extractionOffset]));
            var date = new DateTime(_logDate.Year, _logDate.Month, _logDate.Day);

            var newDate = date.Add(new TimeSpan(0, time.Hour, time.Minute, time.Second, time.Millisecond));
            newEntry.TimeStamp = newDate;
            newEntry.Source = ParseEntity(CleanString(entryInfo[2 - extractionOffset]));
            newEntry.Target = ParseEntity(CleanString(entryInfo[3 - extractionOffset]));
            newEntry.Ability = ParseAbility(CleanString(entryInfo[4 - extractionOffset]));
            newEntry.Effect = ParseEffect(CleanString(entryInfo[5 - extractionOffset]));

            newEntry.Value = ParseValues(value, newEntry.Effect);
            newEntry.Threat =string.IsNullOrEmpty(threat) ? 0 : int.Parse(threat.Replace("<","").Replace(">",""));
            //if (extractionOffset == 1 && newEntry.Target == newEntry.Source)
            //    newEntry.Target.IsPlayer = true;
                
            return newEntry;
        }
        private static PositionData ParsePositionData(string positionString)
        {
            var elements = CleanString(positionString).Replace("{","").Replace("}","").Split(',');
            return new PositionData { X = double.Parse(elements[1]), Y = double.Parse(elements[2]), Facing = double.Parse(elements[0]) };
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
        private static Entity ParseEntity(string value, bool isPlayer =false)
        {
            if (value.Contains("@") && !value.Contains(":"))
            {
                var characterName = value.Split('#')[0].Replace("@", "");
                var existingCharacterEntity = _currentEntities.FirstOrDefault(e => e.Name == characterName);
                if (existingCharacterEntity != null)
                    return existingCharacterEntity;
                var characterEntity = new Entity() { IsCharacter = true, Name =  characterName, IsLocalPlayer = isPlayer};
                _currentEntities.Add(characterEntity);
                return characterEntity;
            }
            if (value.Contains("@") && value.Contains(":"))
            {
                var valueToUse = value; 
                var compaionName = valueToUse.Split(':')[1];
                if (value.Contains("/"))
                { 
                    valueToUse = value.Split('/')[1]; 
                    compaionName = valueToUse.Split(':')[0];
                }
                
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
