using System;

namespace SWTORCombatParser.ViewModels.Overlays.Notes;

public class RaidNoteImage {
    public Guid Id { get; set; } = Guid.NewGuid();

    // PNG bytes, base64-encoded for JSON
    public string PngBase64 { get; set; } = "";

    // Canvas placement
    public double X { get; set; }
    public double Y { get; set; }

    // Size
    public double W { get; set; } = 240;
    public double H { get; set; } = 180;
}
