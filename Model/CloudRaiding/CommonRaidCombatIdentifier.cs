using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Raiding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class CommonRaidCombatIdentifier
    {
        public static Dictionary<DateTime, List<CombatParticipant>> GetCommonCombats(List<RaidParticipantInfo> raidMembers)
        {
            Dictionary<DateTime, List<CombatParticipant>> commonCombats = new Dictionary<DateTime, List<CombatParticipant>>();
            var localPastCombats = raidMembers.First(m=>m.PlayerName == CombatLogStateBuilder.GetLocalPlayerClassandName().PlayerName).PastCombats;
            foreach (var combat in localPastCombats)
            {
                Dictionary<Entity, double> _totalSheildingProvided = new Dictionary<Entity, double>();
                var pastCombats = raidMembers.Skip(1).Select(c => (c, c.PastCombats)).Select(pc => new CombatParticipant { Combat = pc.PastCombats.FirstOrDefault(pc => Math.Abs((pc.StartTime - combat.StartTime).TotalMilliseconds) < 1000), Participant = pc.c }).ToList();
                var validCombats = pastCombats.Where(c => c.Combat != null);
                if (validCombats.Count() == 0)
                    continue;
                pastCombats.Add(new CombatParticipant { Combat = combat, Participant = raidMembers[0] });
                commonCombats[combat.StartTime] = pastCombats;
            }
            return commonCombats;
        }
    }
}
