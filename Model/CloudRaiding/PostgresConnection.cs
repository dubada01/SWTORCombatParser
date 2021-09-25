//using Npgsql;
//using SWTORCombatParser.Utilities;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace SWTORCombatParser.Model.CloudRaiding
//{
//    public class PostgresConnection
//    {
//        private string _dbConnectionString = "Host=swtorparse-free.cagglk8w6mwm.us-west-2.rds.amazonaws.com;Port=5432;Username=master_user;Password=d5525end;Database=swtor-parse";
//        public void AddLog(Guid groupId, ParsedLogEntry logToAdd)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand("INSERT INTO public.raid_logs" +
//                " (raid_group_id,\"timestamp\",target_name,target_isplayer,target_ischaracter,target_iscompanion,source_name,source_isplayer,source_ischaracter,source_iscompanion,ability,effect_name,effect_type,value_raw,value_effective,value_string,was_crit,modifier_raw,modifier_type,threat,log_file_name)" +
//                $" VALUES ('{groupId}','{GetUTCTimeStamp(logToAdd.TimeStamp).ToString("yyyy-MM-dd HH:mm:ss.ms")}'," +
//                $"'{logToAdd.Target.Name.MakePGSQLSafe()}',{logToAdd.Target.IsLocalPlayer},{logToAdd.Target.IsCharacter},{logToAdd.Target.IsCompanion}," +
//                $"'{logToAdd.Source.Name.MakePGSQLSafe()}',{logToAdd.Source.IsLocalPlayer},{logToAdd.Source.IsCharacter},{logToAdd.Source.IsCompanion}," +
//                $"'{logToAdd.Ability.MakePGSQLSafe()}'," +
//                $"'{logToAdd.Effect.EffectName.MakePGSQLSafe()}','{logToAdd.Effect.EffectType}'," +
//                $"{logToAdd.Value.DblValue},{logToAdd.Value.EffectiveDblValue},'{logToAdd.Value.StrValue}',{logToAdd.Value.WasCrit}," +
//                $"{GetDummyModifier(logToAdd.Value).DblValue},'{GetDummyModifier(logToAdd.Value).ValueType}'," +
//                $"{logToAdd.Threat}," +
//                $"'{logToAdd.LogName.MakePGSQLSafe()}')", connection))
//                {
//                    cmd.ExecuteNonQuery();
//                }
//            }
//        }
//        public List<ParsedLogEntry> GetLogsFromLast10Mins(Guid groupId)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand($"SELECT * FROM public.raid_logs where \"timestamp\">\'{GetUTCTimeStamp(DateTime.Now.AddMinutes(-10)):yyyy-MM-dd HH:mm:ss.ms}\' and raid_group_id='{groupId}'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        List<ParsedLogEntry> logs = new List<ParsedLogEntry>();
//                        while (reader.Read())
//                        {
//                            logs.Add(ParseRow(reader));
//                        }
//                        return logs.OrderBy(l => l.TimeStamp).ToList();
//                    }
//                }
//            }
//        }
//        public List<ParsedLogEntry> GetLogsAfterTime(DateTime loggingStarted, Guid groupId)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand($"SELECT * FROM public.raid_logs where \"timestamp\">\'{GetUTCTimeStamp(loggingStarted):yyyy-MM-dd HH:mm:ss.ms}\' and raid_group_id='{groupId}'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        List<ParsedLogEntry> logs = new List<ParsedLogEntry>();
//                        while (reader.Read())
//                        {
//                            logs.Add(ParseRow(reader));
//                        }
//                        return logs.OrderBy(l => l.TimeStamp).ToList();
//                    }
//                }
//            }
//        }
//        public List<ParsedLogEntry> GetLogsBetweenTimes(DateTime from, DateTime until, Guid groupId)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand($"SELECT * FROM public.raid_logs where \"timestamp\">=\'{GetUTCTimeStamp(from):yyyy-MM-dd HH:mm:ss.ms}\' and \"timestamp\"<\'{GetUTCTimeStamp(until):yyyy-MM-dd HH:mm:ss.ms}\' and raid_group_id='{groupId}'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        List<ParsedLogEntry> logs = new List<ParsedLogEntry>();
//                        while (reader.Read())
//                        {
//                            logs.Add(ParseRow(reader));
//                        }
//                        return logs.OrderBy(l => l.TimeStamp).ToList();
//                    }
//                }
//            }
//        }
//        public List<(string,string)> CheckForKeepAlivesInGroupFromTime(Guid groupId, DateTime timeJoined)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand($"SELECT message, log_name FROM public.raid_group_member_keepalive where \"group_id\"=\'{groupId}\' and \"timestamp\">\'{GetUTCTimeStamp(timeJoined):yyyy-MM-dd HH:mm:ss.ms}\'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        List<(string,string)> members = new List<(string,string)>();
//                        while (reader.Read())
//                        {
//                            members.Add((reader.GetString(0),reader.GetString(1)));
//                        }
//                        return members;
//                    }
//                }
//            }
//        }
//        public string GetMostRecentInfoFromLogName(Guid groupId, string logName)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand($"SELECT message FROM public.raid_group_member_keepalive where \"group_id\"=\'{groupId}\' and \"log_name\"=\'{logName}\'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        while (reader.Read())
//                        {
//                            return reader.GetString(0);
//                        }
//                        return "";
//                    }
//                }
//            }
//        }
//        public void UploadMemberKeepAlive(Guid groupId, string info, string logName)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                var alreadyKeepingAlive = false;
//                using (var cmd = new NpgsqlCommand($"SELECT timestamp FROM public.raid_group_member_keepalive where \"group_id\"=\'{groupId}\' and \"log_name\"=\'{logName}\'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        alreadyKeepingAlive = reader.HasRows;
//                    }
//                }
//                var command = "";
//                if (alreadyKeepingAlive)
//                    command = $"UPDATE public.raid_group_member_keepalive SET message='{info.MakePGSQLSafe()}', timestamp='{GetUTCTimeStamp(DateTime.Now):yyyy-MM-dd HH:mm:ss.ms}' where \"group_id\"=\'{groupId}\' and \"log_name\"=\'{logName}\'";
//                else
//                    command = $"INSERT INTO public.raid_group_member_keepalive (group_id,message,log_name,timestamp) VALUES('{groupId}','{info.MakePGSQLSafe()}','{logName}','{GetUTCTimeStamp(DateTime.Now):yyyy-MM-dd HH:mm:ss.ms}')";
//                using (var cmd = new NpgsqlCommand(command, connection))
//                {
//                    cmd.ExecuteNonQuery();
//                }
//            }
//        }

//        public (bool,Guid) ValidateGroupInfo(string name, string password)
//        {
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand($"SELECT id FROM public.raid_groups where name=\'{name}\' and password=\'{password}\'", connection))
//                {
//                    using (var reader = cmd.ExecuteReaderAsync().Result)
//                    {
//                        var anyData = reader.Read();
//                        if (anyData)
//                            return (true, reader.GetGuid(0));
//                        return (false, Guid.Empty);
//                    }
//                }
//            }
//        }
//        public (bool,Guid) AddNewRaidGroup(string name, string password)
//        {
//            if (ValidateGroupInfo(name,password).Item1)
//                return (false,Guid.Empty);

//            var groupId = Guid.NewGuid();
//            using (NpgsqlConnection connection = ConnectToDB())
//            {
//                using (var cmd = new NpgsqlCommand("INSERT INTO public.raid_groups (name,password,id) VALUES (@n,@p,@id)", connection))
//                {
//                    cmd.Parameters.AddWithValue("n", name);
//                    cmd.Parameters.AddWithValue("p", password);
//                    cmd.Parameters.AddWithValue("id", groupId);
//                    cmd.ExecuteNonQueryAsync().Wait();
//                }
//            }
//            return (true, groupId);
//        }
//        private Value GetDummyModifier(Value current)
//        {
//            if (current.Modifier != null)
//                return current.Modifier;
//            return new Value();
//        }
//        private List<Entity> _seenEntities = new List<Entity>();
//        private ParsedLogEntry ParseRow(NpgsqlDataReader reader)
//        {
//            Entity target = GetTarget(reader);
//            Entity source = GetSource(reader);

//            var newLog = new ParsedLogEntry
//            {
//                RaidLogId = reader.GetInt64(16),
//                TimeStamp = TimeZoneInfo.ConvertTimeFromUtc(reader.GetDateTime(1), TimeZoneInfo.Local),
//                Target = target,
//                Source = source,
//                Ability = reader.GetString(8),
//                Effect = new Effect
//                {
//                    EffectName = reader.GetString(17),
//                    EffectType = Enum.Parse<EffectType>(reader.GetString(18)),
//                },
//                Value = new Value
//                {
//                    DblValue = reader.GetDouble(9),
//                    EffectiveDblValue = reader.GetDouble(10),
//                    StrValue = reader.GetString(11),
//                    WasCrit = reader.GetBoolean(12),
//                    Modifier = new Value
//                    {
//                        DblValue = reader.GetDouble(13),
//                        ValueType = Enum.Parse<DamageType>(reader.GetString(14)),
//                    }
//                },
//                Threat = reader.GetInt32(15),
//                LogName = reader.GetString(19)
//            };
//            return newLog;
//        }

//        private Entity GetTarget(NpgsqlDataReader reader)
//        {
//            Entity target;
//            var targetName = reader.GetString(2);
//            var isPlayer = reader.GetBoolean(3);
//            var isCharacter = reader.GetBoolean(4);
//            if (_seenEntities.Any(e => e.Name == targetName && e.IsCharacter == isCharacter && e.IsLocalPlayer == isPlayer))
//                target = _seenEntities.First(e => e.Name == targetName);
//            else
//            {
//                target = new Entity
//                {
//                    Name = reader.GetString(2),
//                    IsLocalPlayer = reader.GetBoolean(3),
//                    IsCharacter = reader.GetBoolean(4),
//                    IsCompanion = reader.GetBoolean(20)
                    
//                };
//                _seenEntities.Add(target);
//            }

//            return target;
//        }

//        private Entity GetSource(NpgsqlDataReader reader)
//        {
//            Entity source;
//            var sourceName = reader.GetString(5);
//            if (_seenEntities.Any(e => e.Name == sourceName))
//                source = _seenEntities.First(e => e.Name == sourceName);
//            else
//            {
//                source = new Entity
//                {
//                    Name = reader.GetString(5),
//                    IsLocalPlayer = reader.GetBoolean(6),
//                    IsCharacter = reader.GetBoolean(7),
//                    IsCompanion = reader.GetBoolean(21)
//                };
//                _seenEntities.Add(source);
//            }

//            return source;
//        }

//        private DateTime GetUTCTimeStamp(DateTime timeZone)
//        {
//            return timeZone.ToUniversalTime();
//        }
//        private NpgsqlConnection ConnectToDB()
//        {
//            var conn = new NpgsqlConnection(_dbConnectionString);
//            conn.Open();
//            return conn;
//        }
//    }
//}
