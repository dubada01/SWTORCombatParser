using System.Reflection;
using System.Timers;
using NUnit.Framework;
using SWTORCombatParser.DataStructures.RoomOverlay;
using SWTORCombatParser.Model.Overlays;

namespace SWTORCombatParser_Test;

public class OverlayResourceTests
{
    [Test]
    public void IpcptHazardStopsItsTimerAndCanBeRestarted()
    {
        var hazard = new IPCPT_Hazard(null, new RoomOverlaySettings());
        var field = typeof(IPCPT_Hazard).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance);
        var timer = (Timer)field.GetValue(hazard);
        try
        {
            hazard.Start();
            Assert.That(timer.Enabled, Is.True);
            hazard.Stop();
            Assert.That(timer.Enabled, Is.False);
            hazard.Start();
            Assert.That(timer.Enabled, Is.True);
            hazard.Stop();
            Assert.That(timer.Enabled, Is.False);
        }
        finally
        {
            timer.Dispose();
        }
    }
}
