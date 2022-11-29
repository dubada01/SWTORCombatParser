using System.Collections.Generic;

namespace SWTORCombatParser.DataStructures.RoomOverlay
{
    public class RoomOverlayUpdate
    {
        public double DisplayTimeSecondsElapsed => TriggerTimeSecondeElapsed - VisibleBufferSeconds;
        public double TriggerTimeSecondeElapsed { get; set; } = 0;
        public double VisibleBufferSeconds { get; set; } = 5;
        public string ImageOverlayPath { get; set; } = "";
        public string ImageOverlayPathExtra { get; set; } = "";

    }
    public class RoomOverlaySettings
    {
        public List<RoomOverlayUpdate> UpateObjects { get; set; } = new List<RoomOverlayUpdate>();
        public string EncounterName { get; set; } = "";
        public double Top { get; set; }
        public double Left { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
