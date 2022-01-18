using MoreLinq;
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
        private static string _dbConnectionString => ReadEncryptedString(JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@"connectionConfig.json"))["ConnectionString"].ToString());
        public static bool TryAddLeaderboardEntry(LeaderboardEntry newEntry)
        {
            var currentValidEntry = GetEntriesForBossAndCharacterWithClass(newEntry.Boss, newEntry.Encounter, newEntry.Character, newEntry.Class, newEntry.Type);
            if(currentValidEntry.Count == 0 || newEntry.Value > currentValidEntry.First().Value)
            {
                if (currentValidEntry.Count > 0)
                {
                    RemoveLeaderBoardEntry(newEntry);
                }
                AddLeaderboardEntry(newEntry);
                return true;
            }
            return false;

        }
        public static void RemoveLeaderBoardEntry(LeaderboardEntry entry)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("DELETE FROM public.boss_leaderboards " +
  $"WHERE boss_name ='{entry.Boss.MakePGSQLSafe()}' and player_name = '{entry.Character.MakePGSQLSafe()}' and player_class = '{entry.Class.MakePGSQLSafe()}' and value_type = '{entry.Type}'", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static void AddLeaderboardEntry(LeaderboardEntry newEntry)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("INSERT INTO public.boss_leaderboards" +
                " (boss_name,encounter_name,player_name,player_class,value,value_type,software_version,duration_sec,verified_kill)" +
                $" VALUES ('{newEntry.Boss.MakePGSQLSafe()}','{newEntry.Encounter.MakePGSQLSafe()}','{newEntry.Character.MakePGSQLSafe()}','{newEntry.Class.MakePGSQLSafe()}',{newEntry.Value},'{newEntry.Type}','{ Leaderboards._leaderboardVersion}','{ newEntry.Duration}','{ newEntry.VerifiedKill}')", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static LeaderboardEntry GetTopLeaderboard(string bossName, string encounter, LeaderboardEntryType type)
        {
            var entries = GetEntriesForBossOfType(bossName, encounter, type);
            if (entries.Count == 0)
                return new LeaderboardEntry();
            return entries.MaxBy(l => l.Value).First();
        }
        public static LeaderboardEntry GetTopLeaderboardForClass(string bossName,  string encounter, string className, LeaderboardEntryType type)
        {
            var entries = GetEntriesForBossWithClass(bossName, encounter, className, type);
            if (entries.Count == 0)
                return new LeaderboardEntry();
            return entries.MaxBy(l => l.Value).First();
        }
        public static List<LeaderboardEntry> GetEntriesForBossAndCharacterWithClass(string bossName, string characterName, string className,string encounter, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.boss_leaderboards " +
                $"WHERE boss_name ='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and player_name = '{characterName.MakePGSQLSafe()}' and player_class = '{className.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entriesFound.Add(GetLeaderboardEntry(reader));
                    }
                }
            }
            return entriesFound;
        }
        public static List<LeaderboardEntry> GetEntriesForBossWithClass(string bossName,string encounter, string className, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.boss_leaderboards " +
                $"WHERE boss_name ='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and player_class = '{className.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entriesFound.Add(GetLeaderboardEntry(reader));
                    }
                }
            }
            return entriesFound;
        }
        public static List<LeaderboardEntry> GetEntriesForBossOfType(string bossName, string encounter,  LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.boss_leaderboards " +
                $"WHERE boss_name='{bossName.MakePGSQLSafe()}' and encounter_name = '{encounter.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entriesFound.Add(GetLeaderboardEntry(reader));
                    }
                }
            }
            return entriesFound;
        }
        public static List<LeaderboardEntry> GetEntriesForCharacterOfType(string playerName, LeaderboardEntryType entryType)
        {
            List<LeaderboardEntry> entriesFound = new List<LeaderboardEntry>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.boss_leaderboards " +
                $"WHERE player_name ='{playerName.MakePGSQLSafe()}' and value_type = '{entryType}' and software_version='{ Leaderboards._leaderboardVersion}'", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entriesFound.Add(GetLeaderboardEntry(reader));
                    }
                }
            }
            return entriesFound;
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
                VerifiedKill = reader.GetBoolean(9)
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
