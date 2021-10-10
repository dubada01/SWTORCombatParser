using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Alerts
{
    public static class OutrangedHealerAlert
    {
        public static event Action<(Entity, List<Entity>)> NotifyOutrangedHealers = delegate { };
        public static void CheckForOutrangingHealers()
        {
            var currentState = CombatLogStateBuilder.CurrentState;
            var positions = currentState.CurrentCharacterPositions;
            var healers = currentState.PlayerClasses.Where(kvp => kvp.Value != null && kvp.Value.Role == DataStructures.Role.Healer).Select(kvp=>kvp.Key).ToList();
            var localPlayer = currentState.CurrentCharacterPositions.Keys.First(e => e.IsLocalPlayer);
            var localPlayerPosition = positions[localPlayer];
            var outrangedHealers = new List<Entity>();
            foreach(var healer in healers)
            {
                if (healer == localPlayer)
                    continue;
                var healerPosition = positions[healer];
                var distance = DistanceCalculator.CalculateDistanceBetweenEntities(healerPosition,localPlayerPosition);
                if (distance > 30)
                    outrangedHealers.Add(healer);
            }
            if (outrangedHealers.Any())
                NotifyOutrangedHealers((localPlayer, outrangedHealers));
        }
    }
}
