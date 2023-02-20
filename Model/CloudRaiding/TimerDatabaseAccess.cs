using Npgsql;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using Newtonsoft.Json;

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
                using (var cmd = new NpgsqlCommand("INSERT INTO public.timer_export" +
                "(timer_id,timer_contents)" +
                $" VALUES (@p1,@p2)", connection){
                        Parameters =
                        {
                            new ("p1",newTimer.ShareId),
                            new ("p2",JsonConvert.SerializeObject(newTimer)),
                        }
                    })
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static Timer GetTimerFromId(string timerId)
        {
            using (NpgsqlConnection connection = ConnectToDB())
            {
                using (var cmd = new NpgsqlCommand("SELECT * FROM public.timer_export " +
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
        private static Timer GetTimer(NpgsqlDataReader reader)
        {
            var stringContents = reader.GetString(2);
            return JsonConvert.DeserializeObject<Timer>(stringContents);
        }
        private static NpgsqlConnection ConnectToDB()
        {
            var conn = new NpgsqlConnection(DatabaseIPGetter.GetCurrentConnectionString());
            conn.Open();
            return conn;
        }
    }
}
