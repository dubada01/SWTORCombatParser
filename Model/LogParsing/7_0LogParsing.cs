using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SWTORCombatParser.Model.LogParsing
{
    public class CustomStringInterning
    {
        private ConcurrentDictionary<string, string> internPool = new ConcurrentDictionary<string, string>();

        public string Intern(string value)
        {
            if (internPool.TryGetValue(value, out var internedValue))
            {
                return internedValue;
            }
            else
            {
                internPool[value] = value;
                return value;
            }
        }
    }
    public static class _7_0LogParsing
    {
        private static ConcurrentDictionary<long, Entity> _currentEntities = new ConcurrentDictionary<long, Entity>();

        private static DateTime _dateTime;

        public static string _damageEffectId = "836045448945501";
        public static string _healEffectId = "836045448945500";
        public static string _fallDamageEffectId = "836045448945484";
        public static string _reflectedId = "836045448953649";
        public static string EnterCombatId = "836045448945489";
        public static string ExitCombatId = "836045448945490";
        public static string DeathCombatId = "836045448945493";
        public static string RevivedCombatId = "836045448945494";
        public static string InterruptCombatId = "836045448945482";
        public static string TargetSetId = "836045448953668";
        public static string TargetClearedId = "836045448953669";
        public static string AbilityActivateId = "836045448945479";
        public static string AbilityCancelId = "836045448945481";
        public static string ApplyEffectId = "836045448945477";
        public static string RemoveEffectId = "836045448945478";
        public static string InConversationEffectId = "806968520343876";
        public static string ModifyThreatId = "836045448945483";
        public static string TauntId = "836045448945488";

        private static Regex valueRegex;
        public static Regex threatRegex;
        private static Encoding _fileEncoding;
        private static CustomStringInterning _interner;

        public static void SetupRegex()
        {
            _interner = new CustomStringInterning();
            valueRegex = new Regex(@"\(.*?\)", RegexOptions.Compiled);
            threatRegex = new Regex(@"\<.*?\>", RegexOptions.Compiled);
            _fileEncoding = Encoding.GetEncoding(1252);
        }
        public static void SetStartDate()
        {
            _dateTime = TimeUtility.CorrectedTime;
        }
        public static ParsedLogEntry ParseLog(string logEntry, DateTime previousLogTime, long lineIndex, List<string> parsedLineInfo, bool realTime)
        {
            var logEntryInfos = parsedLineInfo;

            var secondPart = logEntry.Split(']').Last();
            var value = valueRegex.Match(secondPart);
            var threat = threatRegex.Matches(secondPart);

            //if(!logEntry.Contains('\n'))
            //    return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };
            if (logEntryInfos.Count < 5)
                return new ParsedLogEntry() { LogBytes = _fileEncoding.GetByteCount(logEntry), Error = ErrorType.IncompleteLine };
            //try
            // {
            var parsedLine = ExtractInfo(logEntryInfos.ToArray(), value.Value, threat.Count == 0 ? "" : threat.Select(v => v.Value).First(), previousLogTime);
            parsedLine.LogBytes = _fileEncoding.GetByteCount(logEntry);
            parsedLine.LogLineNumber = lineIndex;
            if (realTime)
                CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(parsedLine, true);
            return parsedLine;
            // }
            //catch(Exception e)
            // {
            // Logging.LogError("Received incomplete log: " + e.Message + "\r\n"+logEntry);
            // return new ParsedLogEntry() { LogText = logEntry, Error = ErrorType.IncompleteLine };
            //  }

        }
        private static ParsedLogEntry ExtractInfo(string[] entryInfo, string value, string threat, DateTime previousLogTime)
        {
            var newEntry = new ParsedLogEntry();

            var time = DateTime.Parse(entryInfo[0]);

            if (time.Hour < previousLogTime.Hour && time != DateTime.MinValue)
                _dateTime = _dateTime.AddDays(1);

            var date = new DateTime(_dateTime.Year, _dateTime.Month, _dateTime.Day);
            var newDate = date.Add(new TimeSpan(0, time.Hour, time.Minute, time.Second, time.Millisecond));
            newEntry.TimeStamp = newDate;

            newEntry.SourceInfo = ParseEntity(entryInfo[1]);
            newEntry.TargetInfo = entryInfo[2] == "=" ? newEntry.SourceInfo : ParseEntity(entryInfo[2]);
            newEntry.Ability = _interner.Intern(ParseAbility(entryInfo[3]));
            newEntry.AbilityId = _interner.Intern(ParseAbilityId(entryInfo[3]));
            newEntry.Effect = ParseEffect(entryInfo[4]);

            if (newEntry.Effect.EffectId == DeathCombatId)
            {
                newEntry.TargetInfo.IsAlive = false;
            }
            if (newEntry.Effect.EffectType == EffectType.AreaEntered)
            {
                newEntry.LogLocation = newEntry.Effect.EffectName;
                newEntry.LogLocationId = newEntry.Effect.EffectId;
                if (!string.IsNullOrEmpty(newEntry.Effect.SecondEffectId))
                    newEntry.LogDifficultyId = newEntry.Effect.SecondEffectId;
            }
            if (newEntry.Effect.EffectType == EffectType.DisciplineChanged)
            {
                newEntry.SourceInfo.Class = GetClassFromDicipline(newEntry.Effect.EffectName);
            }
            newEntry.Value = ParseValues(value, newEntry.Effect);

            if(newEntry.Effect.EffectType != EffectType.AreaEntered)
                newEntry.Threat = string.IsNullOrEmpty(threat) ? 0 : double.Parse(threat.Replace("<", "").Replace(">", ""), CultureInfo.InvariantCulture); 
            
            if (newEntry.Effect.EffectType == EffectType.ModifyThreat)
            {
                newEntry.Value.DisplayValue = newEntry.Threat.ToString();
                newEntry.Value.StrValue = newEntry.Threat.ToString();
            }

            return newEntry;
        }

        private static SWTORClass GetClassFromDicipline(string effectName)
        {
            var parts = effectName.Split('/');
            var spec = parts[1].Split('{')[1].Replace("}", "").Trim();
            return ClassIdentifier.IdentifyClassById(spec);
        }

        private static Value ParseValues(string valueString, Effect currentEffect)
        {
            var cleanValueString = _interner.Intern(valueString.Replace("(", "").Replace(")", ""));
            if (currentEffect.EffectType == EffectType.Apply && (currentEffect.EffectId == _damageEffectId || currentEffect.EffectId == _healEffectId))
                return ParseValueNumber(valueString, currentEffect.EffectName);
            if (currentEffect.EffectType == EffectType.Restore || currentEffect.EffectType == EffectType.Spend)
                return ParseResourceEventValue(valueString);
            if (currentEffect.EffectType == EffectType.Event)
                return new Value() { StrValue = cleanValueString, DisplayValue = cleanValueString };
            if (currentEffect.EffectType == EffectType.Apply && currentEffect.EffectId != _damageEffectId && currentEffect.EffectId != _healEffectId)
                return ParseCharges(valueString);
            if (currentEffect.EffectType == EffectType.ModifyCharges)
            {
                return new Value { StrValue = cleanValueString, DisplayValue = cleanValueString, DblValue = double.Parse(cleanValueString.Split(' ')[0], CultureInfo.InvariantCulture) };
            }
            return new Value();
        }
        private static Value ParseResourceEventValue(string resourceString)
        {
            var cleanValue = resourceString.Replace("(", "").Replace(")", "");
            return new Value() { DblValue = double.Parse(cleanValue, CultureInfo.InvariantCulture), DisplayValue = cleanValue };
        }
        private static Value ParseCharges(string value)
        {
            var chargesValue = new Value();
            if (string.IsNullOrEmpty(value) || value == "()")
                return chargesValue;
            var valueParts = value.Replace("(", string.Empty).Replace(")", string.Empty).Trim().Split(' ');
            chargesValue.StrValue = _interner.Intern(valueParts[0] + " " + valueParts[1]);
            chargesValue.DisplayValue = chargesValue.StrValue;
            chargesValue.DblValue = double.Parse(valueParts[0], CultureInfo.InvariantCulture);
            return chargesValue;
        }
        private static Value ParseValueNumber(string damageValueString, string effectName)
        {

            var newValue = new Value();
            if (damageValueString == "(0 -)" || damageValueString == "")
                return newValue;
            var valueParts = damageValueString.Replace("(", string.Empty).Replace(")", string.Empty).Trim().Split(' ').Where(v => !string.IsNullOrEmpty(v)).ToList();

            if (valueParts.Count == 0)
                return newValue;

            if (valueParts.Count == 1) //fully effective heal
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.ValueType = effectName == _healEffectId ? DamageType.heal : DamageType.none;
                newValue.EffectiveDblValue = newValue.DblValue > 0 ? newValue.DblValue : 0;
            }
            if (valueParts.Count == 2) // partially effective heal
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.ValueType = DamageType.heal;
                var effectiveHeal = double.Parse(valueParts[1].Replace("~", ""), CultureInfo.InvariantCulture);
                newValue.EffectiveDblValue = effectiveHeal > 0 ? effectiveHeal : 0;
            }
            if (valueParts.Count == 3) // fully effective damage or parry
            {
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.EffectiveDblValue = newValue.DblValue;
                newValue.MitigatedDblValue = newValue.DblValue;
                //newValue.ValueType = GetValueType(valueParts[1].Replace("-", ""));
                newValue.ValueTypeId = valueParts[2].Replace("{", "").Replace("}", "").Trim();
                newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);

            }
            
            if (valueParts.Count == 4) // partially effective damage
            {
                if (valueParts[3] == "-") // handle weird space pvp stuff
                {
                    newValue.EffectiveDblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                    newValue.ValueTypeId = valueParts[2].Replace("{", "").Replace("}", "").Trim();
                    newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);
                }
                else
                {
                    if (valueParts[3].Contains(_reflectedId)) // damage reflected
                    {
                        newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                        newValue.EffectiveDblValue = newValue.DblValue;
                        return newValue;
                    }
                    newValue.WasCrit = valueParts[0].Contains("*");
                    newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                    newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""), CultureInfo.InvariantCulture);
                    newValue.MitigatedDblValue = newValue.EffectiveDblValue;
                    newValue.ValueTypeId = valueParts[3].Replace("{", "").Replace("}", "").Trim();
                    newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);
                }
            }
            if (valueParts.Count == 5) // partially effective reflected damage
            {
                if (valueParts[4].Contains(_reflectedId)) // damage reflected
                {
                    newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                    newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""), CultureInfo.InvariantCulture);
                    newValue.MitigatedDblValue = newValue.EffectiveDblValue;
                    return newValue;
                }
                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.MitigatedDblValue = newValue.DblValue;
                newValue.ValueTypeId = valueParts[3].Replace("{", "").Replace("}", "").Trim();
                newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);

            }
            if (valueParts.Count == 6)// absorbed damage tank-weird
            {
                var modifier = new Value
                {
                    ValueType = GetValueTypeById(valueParts[5].Replace("{", "").Replace("}", ""))
                };
                if (double.TryParse(valueParts[3].Replace("(", ""), out double value))
                    modifier.DblValue = value;
                modifier.EffectiveDblValue = modifier.DblValue;
                newValue.Modifier = modifier;
                newValue.ModifierType = newValue.Modifier.ValueType.ToString();
                newValue.ModifierDisplayValue = modifier.EffectiveDblValue.ToString("#,##0");

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.MitigatedDblValue = double.Parse(valueParts[0].Replace("~", "").Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                //newValue.ValueType = GetValueType(valueParts[1]);
                newValue.ValueTypeId = valueParts[2].Replace("{", "").Replace("}", "").Trim();
                newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);
                if (modifier.ValueType == DamageType.absorbed)
                    newValue.EffectiveDblValue = newValue.DblValue;
                else
                    newValue.EffectiveDblValue = newValue.MitigatedDblValue;
            }
            if (valueParts.Count == 7) // absorbed damage non-tank
            {
                var modifier = new Value
                {
                    ValueType = GetValueTypeById(valueParts[6].Replace("{", "").Replace("}", "")),
                    DblValue = double.Parse(valueParts[4].Replace("(", ""), CultureInfo.InvariantCulture)
                };
                modifier.EffectiveDblValue = modifier.DblValue;
                newValue.Modifier = modifier;
                newValue.ModifierType = newValue.Modifier.ValueType.ToString();
                newValue.ModifierDisplayValue = modifier.EffectiveDblValue.ToString("#,##0");

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.MitigatedDblValue = double.Parse(valueParts[1].Replace("~", ""), CultureInfo.InvariantCulture);
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                //newValue.ValueType = GetValueType(valueParts[2]);
                newValue.ValueTypeId = valueParts[3].Replace("{", "").Replace("}", "").Trim();
                newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);
                if (modifier.ValueType == DamageType.absorbed)
                    newValue.EffectiveDblValue = newValue.DblValue;
                else
                    newValue.EffectiveDblValue = newValue.MitigatedDblValue;
            }
            if (valueParts.Count == 8) // tank shielding sheilds more than damage
            {

                var modifier = new Value
                {
                    ValueType = GetValueTypeById(valueParts[4].Replace("{", "").Replace("}", "")),
                    DblValue = double.Parse(valueParts[5].Replace("(", ""), CultureInfo.InvariantCulture)
                };

                modifier.EffectiveDblValue = modifier.DblValue;
                newValue.Modifier = modifier;
                newValue.ModifierType = newValue.Modifier.ValueType.ToString();
                newValue.ModifierDisplayValue = modifier.EffectiveDblValue.ToString("#,##0");

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture) + modifier.EffectiveDblValue;
                newValue.EffectiveDblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.MitigatedDblValue = newValue.EffectiveDblValue;
                //newValue.ValueType = GetValueType(valueParts[1]);
                newValue.ValueTypeId = valueParts[2].Replace("{", "").Replace("}", "").Trim();
                newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);

            }
            if (valueParts.Count == 9) // tank shielding shields less than or equal to damage
            {

                var modifier = new Value
                {
                    ValueType = GetValueTypeById(valueParts[5].Replace("{", "").Replace("}", "")),
                    DblValue = double.Parse(valueParts[6].Replace("(", ""), CultureInfo.InvariantCulture)
                };

                modifier.EffectiveDblValue = Math.Min(double.Parse(valueParts[0].Replace("*", "")), modifier.DblValue);
                newValue.Modifier = modifier;
                newValue.ModifierType = newValue.Modifier.ValueType.ToString();
                newValue.ModifierDisplayValue = modifier.EffectiveDblValue.ToString("#,##0");

                newValue.WasCrit = valueParts[0].Contains("*");
                newValue.DblValue = double.Parse(valueParts[0].Replace("*", ""), CultureInfo.InvariantCulture);
                newValue.EffectiveDblValue = double.Parse(valueParts[1].Replace("~", ""), CultureInfo.InvariantCulture);
                newValue.MitigatedDblValue = newValue.EffectiveDblValue;
                //newValue.ValueType = GetValueType(valueParts[2]);
                newValue.ValueTypeId = valueParts[3].Replace("{", "").Replace("}", "").Trim();
                newValue.ValueType = GetValueTypeById(newValue.ValueTypeId);
            }
            newValue.ValueTypeId = "";
            newValue.DisplayValue = _interner.Intern(newValue.EffectiveDblValue.ToString("#,##0"));
            return newValue;
        }
        private static EntityInfo ParseEntity(string value)
        {
            var entityToReturn = new EntityInfo
            {
                IsAlive = true
            };
            var entityParts = value.Split("|");
            if (entityParts.Length == 1)
                return entityToReturn;
            var name = entityParts[0];
            var position = entityParts[1];
            var hpInfo = entityParts[2];

            AddEntity(entityToReturn, name);
            AddPosition(entityToReturn, position);
            AddHpInfo(entityToReturn, hpInfo);

            return entityToReturn;
        }
        private static void AddPosition(EntityInfo entityInfo, string positionInfo)
        {
            var innerPart = positionInfo.Substring(1, positionInfo.Length - 2);
            var positionParts = innerPart.Split(',');
            entityInfo.Position = new PositionData()
            {
                X = double.Parse(positionParts[0], CultureInfo.InvariantCulture),
                Y = double.Parse(positionParts[1], CultureInfo.InvariantCulture),
                Z = double.Parse(positionParts[2], CultureInfo.InvariantCulture),
                Facing = double.Parse(positionParts[3], CultureInfo.InvariantCulture)
            };
        }
        private static void AddHpInfo(EntityInfo entityInfo, string hpInfo)
        {
            if (string.IsNullOrEmpty(hpInfo))
                return;
            var innerPart = hpInfo.Substring(1, hpInfo.Length - 2);
            var hpParts = innerPart.Split('/');
            entityInfo.CurrentHP = double.Parse(hpParts[0], CultureInfo.InvariantCulture);
            entityInfo.MaxHP = double.Parse(hpParts[1], CultureInfo.InvariantCulture);
        }
        private static void AddEntity(EntityInfo entityToReturn, string name)
        {
            // Constants
            const char atSymbol = '@';
            const char colonSymbol = ':';
            const char leftBraceSymbol = '{';
            const char rightBraceSymbol = '}';
            if (name.Contains(atSymbol))
            {
                if (!name.Contains(colonSymbol))
                {
                    var parts = name.Split('#');
                    var characterName = parts[0].Replace(atSymbol.ToString(), "");
                    if (long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerId))
                    {
                        entityToReturn.Entity = _currentEntities.GetOrAdd(playerId, new Entity
                        {
                            IsCharacter = true,
                            Name = characterName,
                            Id = playerId,
                            LogId = playerId
                        });
                    }
                    return;
                }

                if (name.Contains("/"))
                {
                    var valueToUse = name.Split('/')[1];
                    var companionName = valueToUse.Split(colonSymbol)[0].Trim();

                    var companionNameComponents = companionName.Split(leftBraceSymbol);
                    var compName = companionNameComponents[0].Trim();
                    if (long.TryParse(companionNameComponents[1].Replace(rightBraceSymbol.ToString(), ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var compId))
                    {
                        entityToReturn.Entity = _currentEntities.GetOrAdd(compId, new Entity
                        {
                            IsCharacter = true,
                            IsCompanion = true,
                            Name = compName,
                            Id = compId,
                            LogId = compId
                        });
                    }
                    return;
                }
            }

            if (!name.Contains(colonSymbol))
            {
                var unknownValParts = name.Split(leftBraceSymbol);
                if (long.TryParse(unknownValParts[1].Replace(rightBraceSymbol.ToString(), ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unknownEntityId))
                {
                    entityToReturn.Entity = _currentEntities.GetOrAdd(unknownEntityId, new Entity
                    {
                        IsCharacter = false,
                        Name = "Unknown",
                        Id = unknownEntityId,
                        LogId = unknownEntityId
                    });
                }
                return;
            }

            if (name[0] == ':' && name[1] == ':')
            {
                if (long.TryParse(name.Replace(colonSymbol.ToString(), ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var starFighterId))
                {
                    entityToReturn.Entity = _currentEntities.GetOrAdd(starFighterId, new Entity
                    {
                        IsCharacter = false,
                        Name = starFighterId.ToString(),
                        Id = starFighterId,
                        LogId = starFighterId
                    });
                }
                return;
            }
            var id = long.Parse(name.Split(':')[1]);
            var splitVal = name.Split('{');
            var logId = long.Parse(splitVal[1].Split('}')[0]);
            var entityName = splitVal[0].Trim();

            var newEntity = new Entity() { IsCharacter = false, Name = entityName, Id = id, LogId = logId };
            var entityToUse = _currentEntities.GetOrAdd(id, newEntity);
            entityToReturn.Entity = entityToUse;
        }

        private static string ParseAbility(string value)
        {
            if (value == "")
                return "";
            var splitVal = value.Split('{');
            return splitVal[0].Trim();
        }
        private static string ParseAbilityId(string value)
        {
            if (value == "")
                return "";
            var splitVal = value.Split('{');
            return splitVal[1].Replace("}", "").Trim();
        }
        private static Effect ParseEffect(string value)
        {
            var split = value.Split(':');
            var type = "";
            var name = "";
            if (split.Length == 2)
            {
                type = split[0];
                name = split[1];
            }

            if (split.Length == 3)
            {
                type = split[0];
                name = split[1] + split[2];
            }
            var newEffect = new Effect
            {
                //EffectType = GetEffectType(type.Split('{')[0].Trim())
                EffectType = GetEffectTypeById(type.Split('{')[1].Replace("}", "").Trim())
            };

            var splitName = name.Split('{');

            switch (newEffect.EffectType)
            {
                case EffectType.DisciplineChanged:
                    newEffect.EffectName = _interner.Intern(name);
                    break;
                case EffectType.AreaEntered:
                    {
                        var difficulty = splitName.Length > 1 ? splitName[1].Split('}')[1].Trim() : "";
                        if (splitName.Length > 2)
                            newEffect.SecondEffectId = splitName[2].Replace("}", "");
                        var areaInfo = splitName[0].Trim() + " " + difficulty;
                        newEffect.EffectName = areaInfo;
                        newEffect.EffectId = splitName[1].Split('}')[0];
                        break;
                    }
                default:
                    newEffect.EffectName = _interner.Intern(splitName[0].Trim());
                    newEffect.EffectId = _interner.Intern(splitName[1].Replace("}", "").Trim());
                    break;
            }
            if (newEffect.EffectType == EffectType.Event)
            {
                if (newEffect.EffectId == TargetSetId || newEffect.EffectId == TargetClearedId)
                {
                    newEffect.EffectType = EffectType.TargetChanged;
                }
                if(newEffect.EffectId == ModifyThreatId)
                {
                    newEffect.EffectType = EffectType.ModifyThreat;
                }
                if(newEffect.EffectId == TauntId)
                {
                    newEffect.EffectType = EffectType.ModifyThreat;
                }
            }
            return newEffect;
        }
        private static DamageType GetValueTypeById(string val)
        {
            switch (val)
            {
                case "836045448940874":
                    return DamageType.energy;
                case "836045448940873":
                    return DamageType.kinetic;
                case "836045448940876":
                    return DamageType.intern;
                case "836045448940875":
                    return DamageType.elemental;
                case "836045448945509":
                    return DamageType.shield;
                case "836045448945511":
                    return DamageType.absorbed;
                case "836045448945502":
                    return DamageType.miss;
                case "836045448945503":
                    return DamageType.parry;
                case "836045448945508":
                    return DamageType.deflect;
                case "836045448945505":
                    return DamageType.dodge;
                case "836045448945506":
                    return DamageType.immune;
                case "836045448945507":
                    return DamageType.resist;
                default:
                    return DamageType.unknown;
            }
        }
        private static EffectType GetEffectTypeById(string v)
        {
            switch (v)
            {
                case "836045448945477":
                    return EffectType.Apply;
                case "836045448945478":
                    return EffectType.Remove;
                case "836045448945472":
                    return EffectType.Event;
                case "836045448945473":
                    return EffectType.Spend;
                case "836045448945476":
                    return EffectType.Restore;
                case "836045448953664":
                    return EffectType.AreaEntered;
                case "836045448953665":
                    return EffectType.DisciplineChanged;
                case "836045448953666":
                    return EffectType.ModifyCharges;
                default:
                    return EffectType.Unknown;
            }
        }
    }
}
