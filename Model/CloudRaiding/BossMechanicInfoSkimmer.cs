using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private static string _dbConnectionString => ReadEncryptedString(JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@"connectionConfig.json"))["ConnectionString"].ToString());
        public static void ClearCache()
        {
            File.Delete(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "boss_mechanics_data_new.csv"));
        }
        public static async void AddBossInfoAfterCombat(Combat bossCombat, bool uploadToDb = true)
        {
            if(uploadToDb == true)
            {
                uploadToDb = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("BossMechanicSkimmerConfig.json"))["shouldUploadToDB"].Value<bool>();

            }
            //var clearCache = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("BossMechanicSkimmerConfig.json"))["shouldClearCacheWithEachBoss"].Value<bool>();
            //if (clearCache)
                //ClearCache();
            using (NpgsqlConnection connection = ConnectToDB())
            {
                try
                {
                    var bossInfo = bossCombat.ParentEncounter.BossInfos.First(b => b.EncounterName == bossCombat.EncounterBossDifficultyParts.Item1);
                    var bosses = bossCombat.Targets.Where(t => bossInfo.TargetNames.Contains(t.Name));
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
                            //File.AppendAllText(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),"boss_mechanics_data_new.csv"), JsonConvert.SerializeObject(new ParseBossInfo { start_time = bossCombat.StartTime, seconds_elapsed = secondsElapsed, boss_name = bossName, current_hp = currentHP, encounter_name = encounterName, ability_name = abilityName }) + "\n");
                            if (!uploadToDb)
                                continue;
                            using (var cmd = new NpgsqlCommand("INSERT INTO public.boss_mechanics_data" +
                                " (start_time,seconds_elapsed,current_hp,boss_name,encounter_name,ability_name)" +
                                $" VALUES ('{bossCombat.StartTime.ToUniversalTime()}','{secondsElapsed}','{currentHP}','{bossName.MakePGSQLSafe()}','{encounterName.MakePGSQLSafe()}','{abilityName.MakePGSQLSafe()}')", connection))
                            {
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
                catch(Exception ex)
                {

                }

            }
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
