//using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class PostgresConnection
    {
        public static event Action LeaderboardUpdated = delegate { };
        private static string _dbConnectionString => ReadEncryptedString(JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@"connectionConfig.json"))["ConnectionString"].ToString());
        public static async Task<bool> TryAddLeaderboardEntry(LeaderboardEntry newEntry)
        {
            if (newEntry.Value == 0)
                return false;
            Logging.LogInfo("Trying to add valid entry to DB: "+JsonConvert.SerializeObject(newEntry));
            var currentMatchingEntries = await GetEntriesForBossAndCharacterWithClass(newEntry.Boss, newEntry.Character, newEntry.Class, newEntry.Encounter, newEntry.Type);
            Logging.LogInfo($"Found {currentMatchingEntries.Count} entries in DB already for that character and boss fight");
            if (currentMatchingEntries.Any())
            {
                var currentMax = currentMatchingEntries.MaxBy(e => e.Value).Value;
                var newHighest = currentMax < newEntry.Value;
                Logging.LogInfo($"The current max in the DB of {currentMax} vs {newEntry.Value}");
                if (!newHighest)
                    return false;
                Logging.LogInfo($"New value is higher, removing old value");
                var entriesToBeReplaced = currentMatchingEntries.Where(e => e.Value < newEntry.Value).ToList();
                Logging.LogInfo($"Found {entriesToBeReplaced.Count} entries that need to be removed");
                foreach (var entry in entriesToBeReplaced)
                {
                    await RemoveLeaderBoardEntry(entry);
                    Logging.LogInfo($"Removed with value of {entry.Value}");
                }
            }
            Logging.LogInfo($"Adding the new entry to the DB");
            AddLeaderboardEntry(newEntry);
            CleanDatabaseOfDuplicates(newEntry.Boss, newEntry.Encounter, newEntry.Character, newEntry.Class, newEntry.Type, newEntry.TimeStamp);
            LeaderboardUpdated();
            return true;
        }
        public static async void CleanDatabaseOfDuplicates(string bossName, string characterName, string className, string encounter, LeaderboardEntryType entryType, DateTime timeStamp)
        {
            Logging.LogInfo($"Clearing duplicate entries from other clients");
            var entries = await GetEntriesForBossAndCharacterWithClassFromTime(bossName, characterName, className, encounter, entryType, timeStamp.AddSeconds(-3));
            Logging.LogInfo($"Found {entries.Count} entries");
            if (entries.Count == 1 || entries.Count == 0)
                return;
            Logging.LogInfo($"{entries.Count-1} entries need to be cleansed");
            var orderedValues = entries.OrderByDescending(e=>e.Value).ToList();
            for(var i = 1; i<orderedValues.Count; i++)
            {
                await RemoveLeaderBoardEntry(orderedValues[i]);
                Logging.LogInfo($"Removed with value of {orderedValues[i].Value}");
            }
            
        }
        public static async Task<int> RemoveLeaderBoardEntry(LeaderboardEntry entry)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("DELETE FROM public.boss_leaderboards " +
  $"WHERE boss_name = '{entry.Boss.MakePGSQLSafe()}' and encounter_name = '{entry.Encounter.MakePGSQLSafe()}' and player_name = '{entry.Character.MakePGSQLSafe()}' and player_class = '{entry.Class.MakePGSQLSafe()}' and value_type = '{entry.Type}' and software_version='{Leaderboards._leaderboardVersion}'", connection))
                {
                    return await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        public static async void AddLeaderboardEntry(LeaderboardEntry newEntry)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("INSERT INTO public.boss_leaderboards" +
                " (boss_name,encounter_name,player_name,player_class,value,value_type,software_version,duration_sec,verified_kill,timestamp)" +
                $" VALUES ('{newEntry.Boss.MakePGSQLSafe()}','" +
                $"{newEntry.Encounter.MakePGSQLSafe()}','" +
                $"{newEntry.Character.MakePGSQLSafe()}','" +
                $"{newEntry.Class.MakePGSQLSafe()}'," +
                $"{newEntry.Value}," +
                $"'{newEntry.Type}'," +
                $"'{ Leaderboards._leaderboardVersion}'," +
                $"'{ newEntry.Duration}'," +
                $"'{ newEntry.VerifiedKill}'," +
                $"'{newEntry.TimeStamp.ToUniversalTime()}')", connection))
                {
                   await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        public static async Task<LeaderboardEntry> GetTopLeaderboard(string bossName, string encounter, LeaderboardEntryType type)
        {
            var entries = await GetEntriesForBossOfType(bossName, encounter, type);
            if (entries.Count == 0)
                return new LeaderboardEntry();
            return entries.MaxBy(l => l.Value);
        }
        public static LeaderboardEntry GetTopLeaderboardForClassNonAsync(string bossName, string encounter, string className, LeaderboardEntryType type)
        {
            var entries = GetEntriesForBossWithClassNonAsync(bossName, encounter, className, type);
            if (entries.Count == 0)
                return new LeaderboardEntry();
            return entries.MaxBy(l => l.Value);
        }
        public static async Task<LeaderboardEntry> GetTopLeaderboardForClass(string bossName,  string encounter, string className, LeaderboardEntryType type)
        {
            var entries = await GetEntriesForBossWithClass(bossName, encounter, className, type);
            if (entries.Count == 0)
                return new LeaderboardEntry();
            return entries.MaxBy(l => l.Value);
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossAndCharacterWithClass(string bossName, string characterName, string className,string encounter, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE boss_name ='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and player_name = '{characterName.MakePGSQLSafe()}' and player_class = '{className.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch(Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return entriesFound;
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossAndCharacterWithClassFromTime(string bossName, string characterName, string className, string encounter, LeaderboardEntryType entryType, DateTime from)
        {
            Trace.WriteLine("Removing duplicates from time: "+from.ToUniversalTime().ToString());
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE boss_name ='{bossName.MakePGSQLSafe()}' and " +
                $"encounter_name = '{encounter.MakePGSQLSafe()}' and " +
                $"player_name = '{characterName.MakePGSQLSafe()}' and " +
                $"player_class = '{className.MakePGSQLSafe()}' and " +
                $"value_type = '{entryType}' and " +
                $"software_version = '{ Leaderboards._leaderboardVersion}' and " +
                $"timestamp > '{from.ToUniversalTime()}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return entriesFound;
        }
        public static List<LeaderboardEntry> GetEntriesForBossWithClassNonAsync(string bossName, string encounter, string className, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE boss_name ='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and player_class = '{className.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.LogError(e.Message);
                    }
                }
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossWithClass(string bossName,string encounter, string className, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE boss_name ='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and player_class = '{className.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch(Exception e)
                    {
                        Logging.LogError(e.Message);
                    }
                }
            }
            return entriesFound.DistinctBy(e=>e.Value).ToList();
        }
        public static async Task<List<string>> GetEncountersWithEntries()
        {
            List<string> entriesFound = new List<string>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT distinct encounter_name FROM public.boss_leaderboards " +
                $"WHERE software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(reader.GetString(0));
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return entriesFound.ToList();
        }
        public static async Task<List<string>> GetBossesFromEncounterWithEntries(string encounter)
        {
            List<string> bossesFound = new List<string>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT distinct boss_name FROM public.boss_leaderboards " +
                $"WHERE encounter_name ='{encounter}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            bossesFound.Add(reader.GetString(0));
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return bossesFound.ToList();
        }
        public static List<LeaderboardEntry> GetEntriesForBossOfTypeNonAsync(string bossName, string encounter, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE boss_name='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader =  cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossOfType(string bossName, string encounter,  LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE boss_name='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch(Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForCharacterOfType(string playerName, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                $"WHERE player_name ='{playerName.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }
                    }
                    catch(Exception e)
                    {
                        Logging.LogError(e.Message);
                    }

                }
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        private static LeaderboardEntry GetLightweightLeaderboardEntry(NpgsqlDataReader reader)
        {
            return new LeaderboardEntry
            {
                Boss = reader.GetString(0),
                Character = reader.GetString(1),
                Class = reader.GetString(2),
                Value = reader.GetDouble(3),
                Type = (LeaderboardEntryType)Enum.Parse(typeof(LeaderboardEntryType), reader.GetString(4)),
                Encounter = reader.GetString(5),
                VerifiedKill = reader.GetBoolean(6),
                TimeStamp = ((DateTime)reader.GetTimeStamp(7)), 
                Duration = reader.GetInt32(8)
            };
        }
        private static LeaderboardEntry GetLeaderboardEntry(NpgsqlDataReader reader)
        {
            return new LeaderboardEntry
            {
                Boss = reader.GetString(0),
                Character = reader.GetString(2),
                Class = reader.GetString(3),
                Value = reader.GetDouble(4),
                Type = (LeaderboardEntryType)Enum.Parse(typeof(LeaderboardEntryType),reader.GetString(5)),
                Duration = reader.GetInt32(6),
                Encounter = reader.GetString(7),
                Version = reader.GetString(8),
                VerifiedKill = reader.GetBoolean(9),
                Logs = !reader.IsDBNull(10)? reader.GetString(10):""
            };
        }

        private static DateTime GetUTCTimeStamp(DateTime timeZone)
        {
            return timeZone.ToUniversalTime();
        }
        private static NpgsqlConnection ConnectToDB()
        {
            var conn = new NpgsqlConnection(_dbConnectionString);
            conn.Open();
            return conn;
        }
        private static string ReadEncryptedString(string encryptedString)
        {
            var secret = "obscureButNotSecure";
            var decryptedString = Crypto.DecryptStringAES(encryptedString, secret);
            return decryptedString;
        }
    }
}
