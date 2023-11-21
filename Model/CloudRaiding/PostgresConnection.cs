//using MoreLinq;
using Newtonsoft.Json;
using Npgsql;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class PostgresConnection
    {
        public static int GetCurrentLeaderboardVersion()
        {
            List<LeaderboardVersion> versions = new List<LeaderboardVersion>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return 0;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT leaderboard_version, timestamp FROM public.leaderboard_versions", connection))
                    {

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            versions.Add(new LeaderboardVersion
                            {
                                leadeboard_version = reader.GetInt32(0),
                                timestamp = (DateTime)reader.GetDate(1)
                            });
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return versions.Any() ? versions.Where(t=>t.timestamp < DateTime.Now).MaxBy(v=>v.timestamp).leadeboard_version : 0;
        }
        public static async Task<bool> TryAddLeaderboardEntry(LeaderboardEntry newEntry)
        {
            if (newEntry.Value == 0 || Settings.ReadSettingOfType<bool>("offline_mode"))
                return false;
            var stats = await GetTimestampMed_StdDev(newEntry.Boss, newEntry.Encounter, newEntry.Type);
            if ((stats.Item1 - newEntry.Duration) / stats.Item2 > 4)
                return false;
            Logging.LogInfo("Trying to add valid entry to DB: " + JsonConvert.SerializeObject(newEntry));
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
            await AddLeaderboardEntry(newEntry);
            await CleanDatabaseOfDuplicates(newEntry.Boss, newEntry.Encounter, newEntry.Character, newEntry.Class, newEntry.Type, newEntry.TimeStamp);
            return true;
        }
        private static async Task CleanDatabaseOfDuplicates(string bossName, string characterName, string className, string encounter, LeaderboardEntryType entryType, DateTime timeStamp)
        {
            Logging.LogInfo($"Clearing duplicate entries from other clients");
            var entries = await GetEntriesForBossAndCharacterWithClassFromTime(bossName, characterName, className, encounter, entryType, timeStamp.AddSeconds(-3));
            Logging.LogInfo($"Found {entries.Count} entries");
            if (entries.Count == 1 || entries.Count == 0)
                return;
            Logging.LogInfo($"{entries.Count - 1} entries need to be cleansed");
            var orderedValues = entries.OrderByDescending(e => e.Value).ToList();
            for (var i = 1; i < orderedValues.Count; i++)
            {
                await RemoveLeaderBoardEntry(orderedValues[i]);
                Logging.LogInfo($"Removed with value of {orderedValues[i].Value}");
            }

        }
        public static async Task<int> RemoveLeaderBoardEntry(LeaderboardEntry entry)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return 0;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("DELETE FROM public.boss_leaderboards " +
      $"WHERE boss_name = @p1 and encounter_name =  @p2 and player_name =  @p3 and player_class =  @p4 and value_type =  @p5 and software_version= @p6", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",entry.Boss),
                                   new ("p2",entry.Encounter),
                                   new ("p3",entry.Character),
                                   new ("p4",entry.Class),
                                   new ("p5",entry.Type.ToString()),
                                   new ("p6",Leaderboards._leaderboardVersion),
                               }
                    })
                    {
                        return await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
                return 0;
            }
        }
        public static async Task AddLeaderboardEntry(LeaderboardEntry newEntry)
        {
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("INSERT INTO public.boss_leaderboards" +
                    " (boss_name,encounter_name,player_name,player_class,value,value_type,software_version,duration_sec,verified_kill,timestamp)" +
                    $" VALUES (@p1," +
                    $"@p2," +
                    $"@p3," +
                    $"@p4," +
                    $"@p5," +
                    $"@p6," +
                    $"@p7," +
                    $"@p8," +
                    $"@p9," +
                    $"@p10)", connection)
                    {
                        Parameters =
                        {
                            new ("p1",newEntry.Boss),
                            new ("p2",newEntry.Encounter),
                            new ("p3",newEntry.Character),
                            new ("p4",newEntry.Class),
                            new ("p5",newEntry.Value),
                            new ("p6",newEntry.Type.ToString()),
                            new ("p7",Leaderboards._leaderboardVersion),
                            new ("p8",newEntry.Duration),
                            new ("p9",newEntry.VerifiedKill),
                            new ("p10",GetUTCTimeStamp(newEntry.TimeStamp)),
                        }
                    })
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(JsonConvert.SerializeObject(e));
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
        public static async Task<LeaderboardEntry> GetTopLeaderboardForClass(string bossName, string encounter, string className, LeaderboardEntryType type)
        {
            var entries = await GetEntriesForBossWithClass(bossName, encounter, className, type);
            if (entries.Count == 0)
                return new LeaderboardEntry();
            return entries.MaxBy(l => l.Value);
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossAndCharacterWithClass(string bossName, string characterName, string className, string encounter, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                    $"WHERE boss_name = @p1 and encounter_name = @p2 and player_name = @p3 and player_class = @p4 and value_type = @p5 and software_version= @p6", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",bossName),
                                   new ("p2",encounter),
                                   new ("p3",characterName),
                                   new ("p4",className),
                                   new ("p5",entryType.ToString()),
                                   new ("p6",Leaderboards._leaderboardVersion),
                               }
                    })
                    {

                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }


                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return entriesFound;
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossAndCharacterWithClassFromTime(string bossName, string characterName, string className, string encounter, LeaderboardEntryType entryType, DateTime from)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                    $"WHERE boss_name = @p1 and " +
                    $"encounter_name = @p2 and " +
                    $"player_name = @p3 and " +
                    $"player_class = @p4 and " +
                    $"value_type = @p5 and " +
                    $"software_version = @p6 and " +
                    $"timestamp > to_timestamp(@p6,\'YYYY-MM-DD hh24:mi:ss\')", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",bossName),
                                   new ("p2",encounter),
                                   new ("p3",characterName),
                                   new ("p4",className),
                                   new ("p5",entryType.ToString()),
                                   new ("p6",Leaderboards._leaderboardVersion),
                                   new ("p6",GetUTCTimeStamp(from).ToString("yyyy-MM-dd HH:00:00")),
                               }
                    })
                    {

                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }


                    }

                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return entriesFound;
        }
        public static List<LeaderboardEntry> GetEntriesForBossWithClassNonAsync(string bossName, string encounter, string className, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                    $"WHERE boss_name = @p1 and encounter_name = @p2 and player_class = @p3 and value_type = @p4 and software_version = @p5", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",bossName),
                                   new ("p2",encounter),
                                   new ("p3",className),
                                   new ("p4",entryType.ToString()),
                                   new ("p5",Leaderboards._leaderboardVersion),
                               }
                    })
                    {

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossWithClass(string bossName, string encounter, string className, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                                                       $"WHERE boss_name = @p1 and encounter_name = @p2 and player_class = @p3 and value_type = @p4 and software_version = @p5", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",bossName),
                                   new ("p2",encounter),
                                   new ("p3",className),
                                   new ("p4",entryType.ToString()),
                                   new ("p5",Leaderboards._leaderboardVersion),
                               }
                    })
                    {

                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(GetLightweightLeaderboardEntry(reader));
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        public static async Task<List<string>> GetEncountersWithEntries()
        {
            List<string> entriesFound = new List<string>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT distinct encounter_name FROM public.boss_leaderboards " +
                    $"WHERE software_version='{Leaderboards._leaderboardVersion}'", connection))
                    {

                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            entriesFound.Add(reader.GetString(0));
                        }
                    }


                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return entriesFound.ToList();
		}
		public static async Task<Version> GetMostRecentVersion()
		{
			List<Version> entriesFound = new List<Version>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return new Version();
			try
			{
				using (NpgsqlConnection connection = ConnectToDB())
				{
					using (var cmd = new NpgsqlCommand("SELECT version_name FROM public.software_versions", connection))
					{

						var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
						while (reader.Read())
						{
							entriesFound.Add(new Version(reader.GetString(0)));
						}
					}
				}
			}
			catch (Exception e)
			{
				Logging.LogError(e.Message);
			}
			return entriesFound.Max();
		}
		public static async Task<List<string>> GetBossesFromEncounterWithEntries(string encounter)
        {
            List<string> bossesFound = new List<string>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return bossesFound;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT distinct boss_name FROM public.boss_leaderboards " +
                    $"WHERE encounter_name = @p1 and software_version = @p2", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",encounter),
                                   new ("p2",Leaderboards._leaderboardVersion),
                               }
                    })
                    {

                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            bossesFound.Add(reader.GetString(0));
                        }


                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return bossesFound.ToList();
        }
        public static async Task<List<LeaderboardEntry>> GetEntriesForBossOfType(string bossName, string encounter, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return entriesFound;
            try
            {
                var stats = await GetTimestampMed_StdDev(bossName, encounter, entryType);
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    using (var cmd = new NpgsqlCommand("SELECT boss_name, player_name, player_class, value, value_type, encounter_name, verified_kill, timestamp, duration_sec FROM public.boss_leaderboards " +
                    $"WHERE boss_name = @p1 and encounter_name = @p2 and value_type = @p3 and software_version = @p4", connection)
                    {
                        Parameters =
                               {
                                   new ("p1",bossName),
                                   new ("p2",encounter),
                                   new ("p3",entryType.ToString()),
                                   new ("p4",Leaderboards._leaderboardVersion),
                               }
                    })
                    {

                        var reader = await cmd.ExecuteReaderAsync();
                        while (reader.Read())
                        {
                            var entry = GetLightweightLeaderboardEntry(reader);
                            if ((stats.Item1 - entry.Duration) / stats.Item2 > 4)
                                continue;
                            entriesFound.Add(entry);
                        }


                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return entriesFound.DistinctBy(e => e.Value).ToList();
        }
        public static async Task<(double,double)> GetTimestampMed_StdDev(string bossName, string encounter, LeaderboardEntryType entryType)
        {
            (double, double) stats = new(0, 0);
            if (Settings.ReadSettingOfType<bool>("offline_mode"))
                return stats;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {
                    // Step 1: Fetch the median and standard deviation
                    var sqlFetchStats = "SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY duration_sec) AS median, " +
                                        "stddev(duration_sec) AS standard_deviation FROM public.boss_leaderboards WHERE boss_name = @p1 and encounter_name = @p2 and value_type = @p3 and software_version = @p4";

                    using (var cmd = new NpgsqlCommand(sqlFetchStats, connection)
                    {
                        Parameters =
                               {
                                   new ("p1",bossName),
                                   new ("p2",encounter),
                                   new ("p3",entryType.ToString()),
                                   new ("p4",Leaderboards._leaderboardVersion),
                               }
                    })
                    {

                        var reader = await cmd.ExecuteReaderAsync();

                        if (reader.Read() && reader["median"] != DBNull.Value && reader["standard_deviation"] != DBNull.Value)
                        {
                            stats.Item1 = Convert.ToInt32(reader["median"]);
                            stats.Item2 = Convert.ToDouble(reader["standard_deviation"]);
                        }


                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError(e.Message);
            }
            return stats;
        }
        private static LeaderboardEntry GetLightweightLeaderboardEntry(NpgsqlDataReader reader)
        {
            var encounterName = reader.GetString(5);
            if (encounterName.Contains('"'))
                encounterName = encounterName.Replace('"', '\'');
            return new LeaderboardEntry
            {
                Boss = reader.GetString(0),
                Character = reader.GetString(1),
                Class = reader.GetString(2),
                Value = reader.GetDouble(3),
                Type = (LeaderboardEntryType)Enum.Parse(typeof(LeaderboardEntryType), reader.GetString(4)),
                Encounter = encounterName,
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
                Type = (LeaderboardEntryType)Enum.Parse(typeof(LeaderboardEntryType), reader.GetString(5)),
                Duration = reader.GetInt32(6),
                Encounter = reader.GetString(7),
                Version = reader.GetString(8),
                VerifiedKill = reader.GetBoolean(9),
                Logs = !reader.IsDBNull(10) ? reader.GetString(10) : ""
            };
        }

        private static DateTime GetUTCTimeStamp(DateTime timeZone)
        {
            return timeZone.ToUniversalTime();
        }
        private static NpgsqlConnection ConnectToDB()
        {
            try
            {
                var conn = new NpgsqlConnection(DatabaseIPGetter.GetCurrentConnectionString());
                conn.Open();
                return conn;
            }
            catch (Exception e)
            {
                throw new Exception("Connection attempt failed: " + JsonConvert.SerializeObject(e));
            }
        }
    }
}
