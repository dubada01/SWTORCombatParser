using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SWTORCombatParser.Model.LogParsing
{
    public static class _7_0LogParsing
    {
        private static  List<Entity> _currentEntities = new List<Entity>();
        private static DateTime _dateTime;
        public static ParsedLogEntry ParseLog(string logEntry, long lineIndex, bool buildingState, DateTime logDate, List<Entity> currentEntities)
        {
            _dateTime = logDate;
            _currentEntities = currentEntities;

            var logEntryInfos = Regex.Matches(logEntry, @"\[.*?\]", RegexOptions.Compiled);

            var secondPart = logEntry.Split(']').Last();
            var value = Regex.Match(secondPart, @"\(.*?\)", RegexOptions.Compiled);
            var threat = Regex.Matches(secondPart, @"\<.*?\>", RegexOptions.Compiled);

            if (logEntryInfos.Count < 5)
                return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };

            var parsedLine = ExtractInfo(logEntryInfos.Select(v => v.Value).ToArray(), value.Value, threat.Count == 0 ? "" : threat.Select(v => v.Value).First());
            parsedLine.LogText = logEntry;
            parsedLine.LogLineNumber = lineIndex;

            return parsedLine;
        }
        private static void ParseLogStartLine(string[] entryInfos)
        {
            var player = _currentEntities.FirstOrDefault(e => CleanString(entryInfos[2]).Split(':')[0].Split('#')[0].Replace("@", "") == e.Name);
            if (player == null)
                ParseEntity(CleanString(entryInfos[1]), true);
            else
                player.IsLocalPlayer = true;
        }
        private static ParsedLogEntry ExtractInfo(string[] entryInfo, string value, string threat)
        {
            if (entryInfo.Length == 0)
                return null;

            var newEntry = new ParsedLogEntry();


                
            var time = DateTime.Parse(CleanString(entryInfo[0]));
            var date = new DateTime(_dateTime.Year, _dateTime.Month, _dateTime.Day);
            var newDate = date.Add(new TimeSpan(0, time.Hour, time.Minute, time.Second, time.Millisecond));
            newEntry.TimeStamp = newDate;

            newEntry.SourceInfo = ParseEntity(CleanString(entryInfo[1]));
            if (CleanString(entryInfo[2]) == "=")
                newEntry.TargetInfo = newEntry.SourceInfo;
            else
                newEntry.TargetInfo = ParseEntity(CleanString(entryInfo[2]));
            newEntry.Ability = ParseAbility(CleanString(entryInfo[3]));
            newEntry.Effect = ParseEffect(CleanString(entryInfo[4]));
            if (newEntry.Effect.EffectType == EffectType.AreaEntered)
                newEntry.SourceInfo.Entity.IsLocalPlayer = true;
            newEntry.Value = ParseValues(value, newEntry.Effect);
            if(!threat.Contains('.'))
                newEntry.Threat = string.IsNullOrEmpty(threat) ? 0 : int.Parse(threat.Replace("<", "").Replace(">", ""));

            return newEntry;
        }
        private static Value ParseValues(string valueString, Effect currentEffect)
        {
            if (currentEffect.EffectType == EffectType.Apply && (currentEffect.EffectName == "Damage" || currentEffect.EffectName == "Heal"))
                return ParseDamageValue(valueString);
            if (currentEffect.EffectType == EffectType.Restore || currentEffect.EffectType == EffectType.Spend)
                return ParseResourceEventValue(valueString);
            if (currentEffect.EffectType == EffectType.Event)
                return new Value() { StrValue = valueString.Replace("(", "").Replace(")", "") };
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
            if (damageValueString == "(0 -)" || damageValueString == "")
                return newValue;
            var valueParts = damageValueString.Replace("(", string.Empty).Replace(")", string.Empty).Split(' ');

            if (valueParts.Length == 0)
                return newValue;

            if (valueParts.Length == 1) //fully effective heal
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = newValue.DblValue;
            }
            if (valueParts.Length == 2) // partially effective heal
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""));
            }
            if (valueParts.Length == 3) // fully effective damage or parry
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = newValue.DblValue;
                newValue.ValueType = GetValueType(valueParts[1].Replace("-", ""));
            }
            if (valueParts.Length == 4) // partially effective damage
            {
                if (valueParts[2].Contains("reflected")) // damage reflected
                {
                    newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                    newValue.EffectiveDblValue = newValue.DblValue;
                    return newValue;
                }
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""));
                newValue.ValueType = GetValueType(valueParts[2].Replace("-", ""));
            }
            if (valueParts.Length == 7) // absorbed damage non-tank
            {
                var modifier = new Value();
                modifier.ValueType = GetValueType(valueParts[5].Replace("-", ""));
                modifier.DblValue = double.Parse(valueParts[4].Replace("(", ""));
                modifier.EffectiveDblValue = modifier.DblValue;
                newValue.Modifier = modifier;

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""));
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.ValueType = GetValueType(valueParts[1]);
            }
            if (valueParts.Length == 8) // tank shielding sheilds more than damage
            {

                var modifier = new Value();
                modifier.ValueType = GetValueType(valueParts[3].Replace("-", ""));

                modifier.DblValue = double.Parse(valueParts[5].Replace("(", ""));
                modifier.EffectiveDblValue = modifier.DblValue;
                newValue.Modifier = modifier;

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", "")) + modifier.EffectiveDblValue;
                newValue.EffectiveDblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.ValueType = GetValueType(valueParts[1]);

            }
            if (valueParts.Length == 9) // tank shielding shields less than or equal to damage
            {

                var modifier = new Value();
                modifier.ValueType = GetValueType(valueParts[4].Replace("-", ""));

                modifier.DblValue = double.Parse(valueParts[6].Replace("(", ""));
                modifier.EffectiveDblValue = Math.Min(double.Parse(valueParts[0].Replace("*", "")), modifier.DblValue);
                newValue.Modifier = modifier;

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""));
                newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""));
                newValue.ValueType = GetValueType(valueParts[1]);

            }
            return newValue;
        }
        private static EntityInfo ParseEntity(string value, bool isPlayer = false)
        {
            var entityToReturn = new EntityInfo();
            var entityParts = value.Split("|");
            if (entityParts.Length == 1)
                return entityToReturn;
            var name = entityParts[0];
            var position = entityParts[1];
            var hpInfo = entityParts[2];

            AddEntity(isPlayer, entityToReturn, name);
            AddPosition(entityToReturn, position);
            AddHPInfo(entityToReturn, hpInfo);

            return entityToReturn;
        }
        private static void AddPosition(EntityInfo entityInfo,string positionInfo)
        {
            var positionParts = positionInfo.Replace("(","").Replace(")", "").Split(",");
            entityInfo.Position = new PositionData()
            {
                X = double.Parse(positionParts[0]),
                Z = double.Parse(positionParts[1]),
                Y = double.Parse(positionParts[2]),
                Facing = double.Parse(positionParts[3])
            };
        }
        private static void AddHPInfo(EntityInfo entityInfo, string hpInfo)
        {
            var hpParts = hpInfo.Replace("(", "").Replace(")", "").Split("/");
            entityInfo.CurrentHP = double.Parse(hpParts[0]);
            entityInfo.MaxHP = double.Parse(hpParts[1]);
        }
        private static void AddEntity(bool isPlayer, EntityInfo entityToReturn, string name)
        {
            if (name.Contains("@") && !name.Contains(":"))
            {
                var characterName = name.Split('#')[0].Replace("@", "");
                var existingCharacterEntity = _currentEntities.FirstOrDefault(e => e.Name == characterName);
                if (existingCharacterEntity != null)
                {
                    entityToReturn.Entity = existingCharacterEntity;
                }
                else
                {
                    var characterEntity = new Entity() { IsCharacter = true, Name = characterName, IsLocalPlayer = isPlayer };
                    _currentEntities.Add(characterEntity);
                    entityToReturn.Entity = characterEntity;
                }
            }
            if (name.Contains("@") && name.Contains(":"))
            {
                var valueToUse = name;
                var compaionName = valueToUse.Split(':')[1];
                if (name.Contains("/"))
                {
                    valueToUse = name.Split('/')[1];
                    compaionName = valueToUse.Split(':')[0];
                }

                var companionNameComponents = compaionName.Split('{');
                var existingCompanionEntity = _currentEntities.FirstOrDefault(e => e.Name == companionNameComponents[0].Trim());
                if (existingCompanionEntity != null)
                    entityToReturn.Entity = existingCompanionEntity;
                else
                {
                    var companion = new Entity() { IsCharacter = false, IsCompanion = true, Name = companionNameComponents[0].Trim() };
                    _currentEntities.Add(companion);
                    entityToReturn.Entity = companion;
                }
            }
            if (entityToReturn.Entity.Name == null)
            {
                var splitVal = name.Split('{');
                var entityName = splitVal[0].Trim();
                var existingEntity = _currentEntities.FirstOrDefault(e => e.Name == entityName);
                if (existingEntity != null)
                    entityToReturn.Entity = existingEntity;
                else
                {
                    var newEntity = new Entity();
                    newEntity.Name = entityName;
                    _currentEntities.Add(newEntity);
                    entityToReturn.Entity = newEntity;
                }
            }
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
                case "AreaEntered":
                    return EffectType.AreaEntered;
                case "DisciplineChanged":
                    return EffectType.DisciplineChanged;
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
