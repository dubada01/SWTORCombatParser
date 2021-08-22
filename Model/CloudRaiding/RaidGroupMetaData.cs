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
        public static void UpdateRaidGroupMetaData(ref List<RaidParticipantInfo> raidMembers)
        {
            UpdateFriendlyShielding(ref raidMembers);
        }

        private static void UpdateFriendlyShielding(ref List<RaidParticipantInfo> raidMembers)
        {
            foreach (var combat in raidMembers[0].PastCombats)
            {
                Dictionary<Entity, double> _totalSheildingProvided = new Dictionary<Entity, double>();
                var pastCombats = raidMembers.Skip(1).Select(c =>(c, c.PastCombats)).Select(pc=> new CombatParticipant {Combat = pc.PastCombats.FirstOrDefault(pc => Math.Abs((pc.StartTime - combat.StartTime).TotalMilliseconds) < 1000) ,Participant = pc.c} ).ToList();
                var validCombats = pastCombats.Where(c => c.Combat != null);
                if (validCombats.Count() == 0)
                    continue;
                pastCombats.Add(new CombatParticipant { Combat = combat, Participant = raidMembers[0] });
                var allSheildingLogs = pastCombats.SelectMany(c => c.Combat.IncomingSheildedLogs);
                foreach (var participant in pastCombats)
                {
                    var state = participant.Participant.ParticipantCurrentState;
                    var modifiers = state.Modifiers;
                    var healingShieldModifiers = modifiers.Where(m => m.Name == "Static Barrier" || m.Name == "Force Armor");
                  
                    foreach (var sheildEffect in healingShieldModifiers)
                    {
                      
                        if (!_totalSheildingProvided.ContainsKey(sheildEffect.Source))
                        {
                            _totalSheildingProvided[sheildEffect.Source] = 0;
                        }
                        var absorbsDuringShield = allSheildingLogs.Where(l => l.TimeStamp > sheildEffect.StartTime && l.TimeStamp <= sheildEffect.StopTime);

                        _totalSheildingProvided[sheildEffect.Source] += absorbsDuringShield.Sum(l => l.Value.Modifier.DblValue);
                    }
                    foreach (var source in _totalSheildingProvided.Keys)
                    {

                        var sheildingSource =pastCombats.First(m => m.Participant.PlayerName == source.Name);
                        sheildingSource.Combat.TotalProvidedSheilding = _totalSheildingProvided[source];
                    }
                }

            }

        }
    }
}
