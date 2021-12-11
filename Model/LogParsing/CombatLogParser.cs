using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static ConcurrentDictionary<string,Entity> _currentEntities = new ConcurrentDictionary<string,Entity>();
        private static Random _idGenerator = new Random();
        public static event Action<string> OnNewLog = delegate { };

        public static void SetCurrentState(LogState currentState)
        {
            _logState = currentState;
        }
        public static ParsedLogEntry ParseLine(string logEntry,long lineIndex, bool realTime = true)
        {
            try
            {
                if (_logDate == DateTime.MinValue)
                _logDate = DateTime.Now;
                //var logEntryInfos = Regex.Matches(logEntry, @"\[.*?\]", RegexOptions.Compiled);
                var listEntries = GetInfoComponents(logEntry);

                if (logEntry.Contains('|'))
                    return _7_0LogParsing.ParseLog(logEntry, lineIndex, _logDate, listEntries);

                var secondPart = logEntry.Split(']').Last();
                var value = Regex.Match(secondPart, @"\(.*?\)", RegexOptions.Compiled);
                var threat = Regex.Matches(secondPart, @"\<.*?\>", RegexOptions.Compiled);

                if (listEntries.Count < 5 || string.IsNullOrEmpty(value.Value))
                    return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };

                var parsedLine = ExtractInfo(listEntries.ToArray(), value.Value, threat.Count == 0 ? "" : threat.Select(v => v.Value).First());

                parsedLine.LogText = logEntry;
                parsedLine.LogLineNumber = lineIndex;
                if (parsedLine.Source == parsedLine.Target && parsedLine.Source.IsCharacter)
                    parsedLine.Source.IsLocalPlayer = true;
                if (realTime)
                {
                    UpdateStateAndLogs(new List<ParsedLogEntry> { parsedLine },true);
                }
                return parsedLine;
            }
            catch (Exception e)
            {
                OnNewLog(e.Message);
                return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };
            }
        }
        private static List<string> GetInfoComponents(string log)
        {
            var returnValues = new List<string>();
            int startIndex = 0;
            int numberOfCloses = 0;
            for(var i = 0;i<log.Length;i++)
            {
                if (log[i] == '[')
                {
                    startIndex = i+1;
                    continue;
                }
                if (log[i] == ']')
                { 
                    returnValues.Add(log.Substring(startIndex,i-startIndex));
                    numberOfCloses++;
                    if (numberOfCloses == 5)
                        return returnValues;
                    else
                        continue;
                }
            }
            return returnValues;
        }
        public static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog)
        {
            CombatLogStateBuilder.ClearState();
            _logDate = combatLog.Time;
            var logLines = combatLog.Data.Split('\n');
            var numberOfLines = logLines.Length;
            ParsedLogEntry[] parsedLog = new ParsedLogEntry[numberOfLines];
            //for (var i = 0; i < numberOfLines; i++)
            Parallel.For(0, numberOfLines, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                if (logLines[i] == "")
                    return;
                var parsedLine = ParseLine(logLines[i], i, false);

                if (parsedLine.Error == ErrorType.IncompleteLine)
                    return;
                parsedLog[i] = parsedLine;
                parsedLog[i].LogName = combatLog.Name;
            }
            );
            UpdateLogLocationsFor7_0(parsedLog);
            var orderdedLog = parsedLog.Where(l => l != null).OrderBy(l => l.TimeStamp);
            UpdateStateAndLogs(orderdedLog.ToList(), false);

            CombatTimestampRectifier.RectifyTimeStamps(orderdedLog.ToList());
            return orderdedLog.ToList();
        }

        private static void UpdateLogLocationsFor7_0(ParsedLogEntry[] parsedLog)
        {
            var areaChangedLogs = parsedLog.Where(l => l != null && l.Effect.EffectType == EffectType.AreaEntered).ToList();
            for (var i = 0; i < areaChangedLogs.Count; i++)
            {
                List<ParsedLogEntry> logsInScope = new List<ParsedLogEntry>();
                if (i < areaChangedLogs.Count - 1)
                {
                    logsInScope = parsedLog.Where(l => l != null && l.TimeStamp > areaChangedLogs[i].TimeStamp && l.TimeStamp <= areaChangedLogs[i + 1].TimeStamp).ToList();
                }
                else
                {
                    logsInScope = parsedLog.Where(l => l != null && l.TimeStamp > areaChangedLogs[i].TimeStamp).ToList();
                }
                logsInScope.ForEach(l => l.LogLocation = areaChangedLogs[i].Effect.EffectName);
                logsInScope.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "EnterCombat").ToList().ForEach(l => l.Value.StrValue = areaChangedLogs[i].Effect.EffectName);
            }
        }

        private static void UpdateStateAndLogs(List<ParsedLogEntry> orderdedLog, bool realTime)
        {
            foreach (var line in orderdedLog)
            {
                SetCurrentState(CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(line, realTime));
            }
            Parallel.ForEach(orderdedLog, line =>
            {
                LogModifier.UpdateLogWithState(line, _logState);
            });
            //LogModifier.StartUpdateOfModifiersActiveForLogs(orderdedLog, _logState);
        }

        private static ParsedLogEntry ExtractInfo(string[] entryInfo, string value, string threat)
        {
            if (entryInfo.Length == 0)
                return null;

            var newEntry = new ParsedLogEntry();
            var extractionOffset = entryInfo.Count() == 6 ? 0 : 1;

            var time = DateTime.Parse(CleanString(entryInfo[1-extractionOffset]));
            var date = new DateTime(_logDate.Year, _logDate.Month, _logDate.Day);

            var newDate = date.Add(new TimeSpan(0, time.Hour, time.Minute, time.Second, time.Millisecond));
            newEntry.TimeStamp = newDate;
            newEntry.SourceInfo = new EntityInfo { Entity = ParseEntity(CleanString(entryInfo[2 - extractionOffset])) };
            newEntry.TargetInfo = new EntityInfo { Entity = ParseEntity(CleanString(entryInfo[3 - extractionOffset])) };
            newEntry.Ability = ParseAbility(CleanString(entryInfo[4 - extractionOffset]));
            newEntry.Effect = ParseEffect(CleanString(entryInfo[5 - extractionOffset]));
            newEntry.Value = ParseValues(value, newEntry.Effect);
            newEntry.Threat =string.IsNullOrEmpty(threat) ? 0 : int.Parse(threat.Replace("<","").Replace(">",""));
            if (newEntry.Effect.EffectName == "EnterCombat")
            {
                newEntry.LogLocation = newEntry.Value.StrValue;
            }
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
                newValue.ValueType = DamageType.heal;
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
                if (_currentEntities.ContainsKey(characterName))
                {
                    return _currentEntities[characterName];
                }

                var characterEntity = new Entity() { IsCharacter = true, Name =  characterName, IsLocalPlayer = isPlayer, Id = _idGenerator.Next(0,10000)};
                _currentEntities[characterName]= characterEntity;
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
                var companionNameValue = companionNameComponents[0].Trim();
                if (_currentEntities.ContainsKey(companionNameValue))
                {
                    return _currentEntities[companionNameValue];
                }
                var companion = new Entity() { IsCharacter = false,IsCompanion = true, Name = companionNameComponents[0].Trim(), Id = _idGenerator.Next(0, 10000) };
                _currentEntities[companionNameValue]=(companion);
                return companion;
            }
            var splitVal = value.Split('{');
            var entityName = splitVal[0].Trim();
            if (_currentEntities.ContainsKey(entityName))
            {
                return _currentEntities[entityName];
            }

            var newEntity = new Entity();
            newEntity.Name = entityName;
            newEntity.Id = _idGenerator.Next(0, 10000);
            _currentEntities[entityName]=(newEntity);
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
