using Npgsql;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class PostgresConnection
    {
        private string _dbConnectionString = "Host=swtor-parsing-db-instance-1.cagglk8w6mwm.us-west-2.rds.amazonaws.com;Port=3306;Username=master_user;Password=d5525end;Database=swtor-parse";
        public PostgresConnection()
        {
        }
        public void AddLog(Guid groupId, ParsedLogEntry logToAdd)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("INSERT INTO public.raid_logs" +
                " (raid_group_id,\"timestamp\",target_name,target_isplayer,target_ischaracter,source_name,source_isplayer,source_ischaracter,ability,effect_name,effect_type,value_raw,value_effective,value_string,was_crit,modifier_raw,modifier_type,threat,log_file_name)" +
                $" VALUES ('{groupId}','{GetUTCTimeStamp(logToAdd.TimeStamp).ToString("yyyy-MM-dd hh:mm:ss.ms")}'," +
                $"'{logToAdd.Target.Name.MakePGSQLSafe()}',{logToAdd.Target.IsPlayer},{logToAdd.Target.IsCharacter}," +
                $"'{logToAdd.Source.Name.MakePGSQLSafe()}',{logToAdd.Source.IsPlayer},{logToAdd.Source.IsCharacter}," +
                $"'{logToAdd.Ability.MakePGSQLSafe()}'," +
                $"'{logToAdd.Effect.EffectName.MakePGSQLSafe()}','{logToAdd.Effect.EffectType}'," +
                $"{logToAdd.Value.DblValue},{logToAdd.Value.EffectiveDblValue},'{logToAdd.Value.StrValue}',{logToAdd.Value.WasCrit}," +
                $"{GetDummyModifier(logToAdd.Value).DblValue},'{GetDummyModifier(logToAdd.Value).ValueType}'," +
                $"{logToAdd.Threat}," +
                $"'{logToAdd.LogName.MakePGSQLSafe()}')", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public List<ParsedLogEntry> GetLogsAfterTime(DateTime loggingStarted, Guid groupId)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand($"SELECT * FROM public.raid_logs where \"timestamp\">=\'{GetUTCTimeStamp(loggingStarted).ToString("yyyy-MM-dd hh:mm:ss.ms")}\' and raid_group_id='{groupId}'", connection))
                {
                    using (var reader = cmd.ExecuteReaderAsync().Result)
                    {
                        List<ParsedLogEntry> logs = new List<ParsedLogEntry>();
                        while (reader.Read())
                        {
                            logs.Add(ParseRow(reader));
                        }
                        return logs.OrderBy(l => l.TimeStamp).ToList();
                    }
                }
            }
        }
        public List<ParsedLogEntry> GetLogsBetweenTimes(DateTime from, DateTime until, Guid groupId)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand($"SELECT * FROM public.raid_logs where \"timestamp\">=\'{GetUTCTimeStamp(from).ToString("yyyy-MM-dd hh:mm:ss.ms")}\' and \"timestamp\"<\'{GetUTCTimeStamp(until).ToString("yyyy-MM-dd hh:mm:ss.ms")}\' and raid_group_id='{groupId}'", connection))
                {
                    using (var reader = cmd.ExecuteReaderAsync().Result)
                    {
                        List<ParsedLogEntry> logs = new List<ParsedLogEntry>();
                        while (reader.Read())
                        {
                            logs.Add(ParseRow(reader));
                        }
                        return logs.OrderBy(l => l.TimeStamp).ToList();
                    }
                }
            }
        }
        public (bool,Guid) ValidateGroupInfo(string name, string password)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand($"SELECT id FROM public.raid_groups where name=\'{name}\' and password=\'{password}\'", connection))
                {
                    using (var reader = cmd.ExecuteReaderAsync().Result)
                    {
                        var anyData = reader.Read();
                        if (anyData)
                            return (true, reader.GetGuid(0));
                        return (false, Guid.Empty);
                    }
                }
            }
        }
        public (bool,Guid) AddNewRaidGroup(string name, string password)
        {
            if (ValidateGroupInfo(name,password).Item1)
                return (false,Guid.Empty);

            var groupId = Guid.NewGuid();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("INSERT INTO public.raid_groups (name,password,id) VALUES (@n,@p,@id)", connection))
                {
                    cmd.Parameters.AddWithValue("n", name);
                    cmd.Parameters.AddWithValue("p", password);
                    cmd.Parameters.AddWithValue("id", groupId);
                    cmd.ExecuteNonQueryAsync().Wait();
                }
            }
            return (true, groupId);
        }
        private Value GetDummyModifier(Value current)
        {
            if (current.Modifier != null)
                return current.Modifier;
            return new Value();
        }
        private ParsedLogEntry ParseRow(NpgsqlDataReader reader)
        {
            var newLog = new ParsedLogEntry
            {
                RaidLogId = reader.GetInt64(16),
                TimeStamp = TimeZoneInfo.ConvertTimeFromUtc(reader.GetDateTime(1), TimeZoneInfo.Local),
                Target = new Entity
                {
                    Name = reader.GetString(2),
                    IsPlayer = reader.GetBoolean(3),
                    IsCharacter = reader.GetBoolean(4)
                },
                Source = new Entity
                {
                    Name = reader.GetString(5),
                    IsPlayer = reader.GetBoolean(6),
                    IsCharacter = reader.GetBoolean(7)
                },
                Ability = reader.GetString(8),
                Effect = new Effect
                {
                    EffectName = reader.GetString(17),
                    EffectType = Enum.Parse<EffectType>(reader.GetString(18)),
                },
                Value = new Value
                {
                    DblValue = reader.GetDouble(9),
                    EffectiveDblValue = reader.GetDouble(10),
                    StrValue = reader.GetString(11),
                    WasCrit = reader.GetBoolean(12),
                    Modifier = new Value
                    {
                        DblValue = reader.GetDouble(13),
                        ValueType = Enum.Parse<DamageType>(reader.GetString(14)),
                    }
                },
                Threat = reader.GetInt32(15),
                LogName = reader.GetString(19)
            };
            return newLog;
        }
        private DateTime GetUTCTimeStamp(DateTime timeZone)
        {
            return timeZone.ToUniversalTime();
        }
        private NpgsqlConnection ConnectToDB()
        {
            var conn = new NpgsqlConnection(_dbConnectionString);
            conn.Open();
            return conn;
        }
    }
}
