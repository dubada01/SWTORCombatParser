using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class TimerDatabaseAccess
    {
        public static List<string> GetAllTimerIds()
        {
            List<string> entriesFound = new List<string>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT timer_id FROM public.timers", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entriesFound.Add(reader.GetString(0));
                    }
                }
            }
            return entriesFound;
        }
        public static void AddTimer(Timer newTimer)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("INSERT INTO public.timers" +
                " (timer_id,source,source_is_local,target,target_is_local,hp_percentage,name,trigger_type,expiration_trigger_id,ability,effect,is_periodic,is_alert,duration_sec,color,specific_boss,specific_encounter)" +
                $" VALUES ('{newTimer.ShareId.MakePGSQLSafe()}','{newTimer.Source.MakePGSQLSafe()}','{newTimer.SourceIsLocal}','{newTimer.Target.MakePGSQLSafe()}',{newTimer.TargetIsLocal},'{newTimer.HPPercentage}','{newTimer.Name.MakePGSQLSafe()}'," +
                $"'{newTimer.TriggerType}','{newTimer.ExperiationTimerId.MakePGSQLSafe()}','{newTimer.Ability.MakePGSQLSafe()}','{newTimer.Effect.MakePGSQLSafe()}'" +
                $",{newTimer.IsPeriodic},'{newTimer.IsAlert}','{newTimer.DurationSec}','{newTimer.TimerColor}','{newTimer.SpecificBoss.MakePGSQLSafe()}','{newTimer.SpecificEncounter.MakePGSQLSafe()}')", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static Timer GetTimerFromId(string timerId)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.timers " +
                $"WHERE timer_id='{timerId.MakePGSQLSafe()}'", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        return GetTimer(reader);
                    }
                }
            }
            return null;
        }
        public static List<Timer> GetTimersForEncounterBoss(string bossName, string encounter)
        {
            List<Timer> entriesFound = new List<Timer>();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.timers " +
                $"WHERE specific_boss='{bossName.MakePGSQLSafe()}' and specific_encounter = '{encounter.MakePGSQLSafe()}'", connection))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entriesFound.Add(GetTimer(reader));
                    }
                }
            }
            return entriesFound;
        }
        private static Timer GetTimer(NpgsqlDataReader reader)
        {
            return new Timer
            {
                ShareId = reader.GetString(1),
                Source = reader.GetString(2),
                SourceIsLocal = reader.GetBoolean(3),
                Target = reader.GetString(4),
                TargetIsLocal = reader.GetBoolean(5),
                HPPercentage = reader.GetDouble(6),
                Name = reader.GetString(7),
                ExperiationTimerId = reader.GetString(8),
                Ability = reader.GetString(9),
                Effect = reader.GetString(10),
                IsPeriodic = reader.GetBoolean(11),
                IsAlert = reader.GetBoolean(12),
                DurationSec = reader.GetDouble(13),
                TimerColor = (Color)ColorConverter.ConvertFromString(reader.GetString(14)),
                TriggerType = (TimerKeyType)Enum.Parse(typeof(TimerKeyType), reader.GetString(15)),
                SpecificBoss = reader.GetString(16),
                SpecificEncounter = reader.GetString(17)

            };
        }
        private static NpgsqlConnection ConnectToDB()
        {
            var conn = new NpgsqlConnection(DatabaseIPGetter.GetCurrentConnectionString());
            conn.Open();
            return conn;
        }
    }
}
