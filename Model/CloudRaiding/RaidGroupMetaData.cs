using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.ViewModels.Raiding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class CombatParticipant
    {
        public Combat Combat;
        public RaidParticipantInfo Participant;
    }

    public static class RaidGroupMetaData
    {
        public static void UpdateRaidGroupMetaData(List<CombatParticipant> currentCombats)
        {
            UpdateFriendlyShielding(currentCombats);
        }

        private static void UpdateFriendlyShielding(List<CombatParticipant> currentCombats)
        {
            var validCombats = currentCombats.Where(pc => pc != null && pc.Combat.StartTime != DateTime.MinValue).ToList();
            if (validCombats.Count() == 0)
                return;
            var allSheildingLogs = validCombats.SelectMany(c => c.Combat?.IncomingSheildedLogs).ToList();
            foreach (var participantCombat in validCombats)
            {
                var state = participantCombat.Participant.ParticipantCurrentState;
                AddSheildingToLogs.AddSheildLogs(state, allSheildingLogs, participantCombat.Combat);
            }
        }
    }
}
