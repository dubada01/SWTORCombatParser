using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using SWTORCombatParser.Utilities;
using System;
using System.IO;
using System.Linq;
using SWTORCombatParser.DataStructures;
using System.Globalization;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class ParseBossInfo
    {
        public DateTime start_time { get; set; }
        public double seconds_elapsed { get; set; }
        public double current_hp { get; set; }
        public string boss_name { get; set; }
        public string encounter_name { get; set; }
        public string ability_name { get; set; }
    }
    public static class BossMechanicInfoSkimmer
    {
        public static void AddBossInfoAfterCombat(Combat bossCombat, bool uploadToDb = true)
        {
            if (bossCombat.ParentEncounter.Name == "Parsing")
                return;
            if (uploadToDb == true)
            {
                uploadToDb = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("BossMechanicSkimmerConfig.json"))["shouldUploadToDB"].Value<bool>();

            }
            if (!uploadToDb)
                return;
            try
            {
                using (NpgsqlConnection connection = ConnectToDB())
                {

                    var bossInfo = bossCombat.ParentEncounter.BossInfos.First(b => b.EncounterName == bossCombat.EncounterBossDifficultyParts.Item1);
                    var bosses = bossCombat.Targets.Where(t => bossInfo.TargetIds.Contains(t.Name));
                    foreach (var boss in bosses)
                    {
                        var infos = bossCombat.AbilitiesActivated[boss];
                        foreach (var activation in infos.Where(i => !string.IsNullOrEmpty(i.Ability)))
                        {
                            var secondsElapsed = (activation.TimeStamp - bossCombat.StartTime).TotalSeconds;
                            var currentHP = (activation.SourceInfo.CurrentHP / activation.SourceInfo.MaxHP);
                            var bossName = boss.Name;
                            var encounterName = bossCombat.ParentEncounter.NamePlus;
                            var abilityName = activation.Ability;
                            if (!uploadToDb)
                                continue;
                            using (var cmd = new NpgsqlCommand("INSERT INTO public.boss_mechanics_data" +
                                " (start_time,seconds_elapsed,current_hp,boss_name,encounter_name,ability_name)" +
                                $" VALUES " +
                                $"(@p1," +
                                $"@p2," +
                                $"@p3," +
                                $"@p4," +
                                $"@p5," +
                                $"@p6)", connection)
                            {
                                Parameters  =
                                {
                                    new ("p1",GetUTCTimeStamp(DateTime.Now)),
                                    new ("p2",secondsElapsed),
                                    new ("p3",currentHP),
                                    new ("p4",bossName.MakePGSQLSafe()),
                                    new ("p5",encounterName.MakePGSQLSafe()),
                                    new ("p6",abilityName.MakePGSQLSafe())
                                }
                            })
                            {
                                var r = cmd.ExecuteNonQueryAsync().Result;
                            }
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                Logging.LogError("Boss mechanics upload database exception: " + ex.Message);
            }
        }
        private static DateTime GetUTCTimeStamp(DateTime timeZone)
        {
            return timeZone.ToUniversalTime();
        }
        private static NpgsqlConnection ConnectToDB()
        {
            var conn = new NpgsqlConnection(DatabaseIPGetter.GetCurrentConnectionString());
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
