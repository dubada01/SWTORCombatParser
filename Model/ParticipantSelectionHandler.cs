using SWTORCombatParser.DataStructures;
using System;

namespace SWTORCombatParser.Model
{
    public static class ParticipantSelectionHandler
    {
        public static Entity CurrentlySelectedParticpant { get; set; }
        public static event Action<Entity> SelectionUpdated = delegate { };
        public static void UpdateSelection(Entity newSelection)
        {
            if (newSelection == CurrentlySelectedParticpant)
                return;
            CurrentlySelectedParticpant = newSelection;
            SelectionUpdated(CurrentlySelectedParticpant);
        }
    }
}
