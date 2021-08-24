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
        public static void UpdateRaidGroupMetaData(List<CombatParticipant> combats)
        {
            UpdateFriendlyShielding(combats);
        }

        private static void UpdateFriendlyShielding(List<CombatParticipant> combats)
        {
            var combatsByParticipant = combats.GroupBy(c => c.Participant);
            foreach (var participant in combatsByParticipant)
            {
                foreach (var combat in participant)
                {
                    Dictionary<Entity, double> _totalSheildingProvided = new Dictionary<Entity, double>();
                    var commonCombats = combats.Select(c => c).Where(pc => Math.Abs((pc.Combat.StartTime - combat.Combat.StartTime).TotalMilliseconds) < 1000).ToList();
                    var validCombats = commonCombats.Where(c => c != null);
                    if (validCombats.Count() == 1)
                        commonCombats = new List<CombatParticipant> { combat };
                    else
                        commonCombats.Add(combat);
                    var allSheildingLogs = commonCombats.SelectMany(c => c.Combat?.IncomingSheildedLogs);
                    foreach (var participantCombat in commonCombats)
                    {
                        var state = participantCombat.Participant.ParticipantCurrentState;
                        var modifiers = state.Modifiers;
                        var healingShieldModifiers = modifiers.Where(m => m.Name == "Static Barrier" || m.Name == "Force Armor").ToList();

                        foreach (var sheildEffect in healingShieldModifiers)
                        {
                            if (!_totalSheildingProvided.ContainsKey(sheildEffect.Source))
                            {
                                _totalSheildingProvided[sheildEffect.Source] = 0;
                            }
                            var absorbsDuringShield = allSheildingLogs.Where(l => l.TimeStamp > sheildEffect.StartTime && l.TimeStamp <= sheildEffect.StopTime).ToList();
                            _totalSheildingProvided[sheildEffect.Source] += absorbsDuringShield.Sum(l => l.Value.Modifier.DblValue);
                        }
                        foreach (var source in _totalSheildingProvided.Keys)
                        {
                            Trace.WriteLine($"Sheilding done by {source.Name}: { _totalSheildingProvided[source]} in combat at {combat.Combat.StartTime}");
                            var sheildingSource = commonCombats.First(m => m.Participant.PlayerName == source.Name);
                            sheildingSource.Combat.TotalProvidedSheilding = _totalSheildingProvided[source];
                        }
                    }
                }
            }
        }
    }
}
