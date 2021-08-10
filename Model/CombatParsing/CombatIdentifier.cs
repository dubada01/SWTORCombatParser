﻿using SWTORCombatParser.DataStructures.RaidInfos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public static class CombatIdentifier
    {
        public static Combat ParseOngoingCombat(List<ParsedLogEntry> ongoingLogs)
        {
            if (!ongoingLogs.Any(l => l.Source.IsPlayer))
                return new Combat();
            var newCombat = new Combat()
            {
                CharacterName = ongoingLogs.First(l => l.Source.IsPlayer).Source.Name,
                StartTime = ongoingLogs.First().TimeStamp,
                EndTime = ongoingLogs.Last().TimeStamp,
                Targets = GetTargets(ongoingLogs),
                RaidBossInfo = GetBossInfo(ongoingLogs),
                Logs = ongoingLogs
            };
            CombatMetaDataParse.PopulateMetaData(ref newCombat);
            return newCombat;
        }
        private static List<string> GetTargets(List<ParsedLogEntry> logs)
        {
            return logs.Select(l=>l.Target).Where(t=>!t.IsCharacter).Select(npc=>npc.Name).Distinct().ToList();
        }
        private static string GetBossInfo(List<ParsedLogEntry> logs)
        {
            RaidInfo raidOfInterest = null;
            string difficulty = "";
            string numberOfPlayers = "";
            foreach (var log in logs)
            {
                var raids = RaidNameLoader.SupportedRaids;
                if (!string.IsNullOrEmpty(log.Value.StrValue) && raids.Select(r => r.LogName).Any(ln => log.Value.StrValue.Contains(ln)) && raidOfInterest == null)
                {
                    raidOfInterest = raids.First(r => log.Value.StrValue.Contains(r.LogName));
                    difficulty = RaidNameLoader.SupportedRaidDifficulties.First(f => log.Value.StrValue.Contains(f));
                    numberOfPlayers = RaidNameLoader.SupportedNumberOfPlayers.First(f => log.Value.StrValue.Contains(f));
                }
                if (raidOfInterest == null)
                    continue;
                if (raidOfInterest.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Source.Name) || raidOfInterest.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Target.Name))
                {
                    var boss = raidOfInterest.BossInfos.First(b => b.TargetNames.Contains(log.Source.Name) || b.TargetNames.Contains(log.Target.Name));
                    return boss.EncounterName + " {" + numberOfPlayers.Replace("Player", "") + difficulty + "}";
                }
            }
            return "";
        }
    }
}