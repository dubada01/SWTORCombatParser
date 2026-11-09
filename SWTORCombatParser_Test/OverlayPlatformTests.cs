using System;
using NUnit.Framework;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Native;

namespace SWTORCombatParser_Test;

public class OverlayPlatformTests
{
    [TestCase(1, 0, false)]
    [TestCase(1, 1, true)]
    [TestCase(2, 0, true)]
    [TestCase(0, 9, false)]
    public void InputShapesRequireShape11(int major, int minor, bool supported) =>
        Assert.That(LinuxOverlayInput.SupportsInputShape(major, minor), Is.EqualTo(supported));

    [TestCase("wayland", null, true)]
    [TestCase("Wayland", "", true)]
    [TestCase(null, "wayland-0", true)]
    [TestCase("x11", null, false)]
    [TestCase(null, null, false)]
    public void DetectWaylandEvenWhenDisplayAlsoExists(string session, string socket, bool expected) =>
        Assert.That(OverlayPlatformStatus.IsWayland(session, socket), Is.EqualTo(expected));

    [TestCase(0x00ff0000u, 0x00ff0000u, 255)]
    [TestCase(0x00008000u, 0x0000ff00u, 128)]
    [TestCase(0x0000f800u, 0x0000f800u, 255)]
    [TestCase(0x00000400u, 0x000007e0u, 130)]
    [TestCase(0x3ff00000u, 0x3ff00000u, 255)]
    [TestCase(0u, 0u, 0)]
    public void CaptureHandles16_24And30BitVisuals(uint pixel, uint mask, int expected) =>
        Assert.That(LinuxScreenCapturer.Channel(pixel, mask), Is.EqualTo(expected));

    [TestCase("Wayland")]
    [TestCase("HWND")]
    [TestCase(null)]
    public void NeverSendX11RequestsToAnotherBackend(string descriptor) =>
        Assert.Throws<PlatformNotSupportedException>(() => LinuxOverlayInput.Apply(new IntPtr(42), descriptor, true));

    [Test]
    public void NeverSendObjectiveCMessagesToAnotherBackend() =>
        Assert.Throws<PlatformNotSupportedException>(() => MacOverlayInput.Apply(new IntPtr(42), "HWND", true));

    [Test]
    public void MacOverlayCanJoinAnotherAppsFullscreenSpaceWithoutConflictingRoles()
    {
        const long incompatible = (1L << 1) | (1L << 7) | (1L << 9) | (1L << 16) | (1L << 17);
        const long unrelated = (1L << 4) | (1L << 6) | (1L << 12);
        const long overlayBehavior = (1L << 0) | (1L << 8) | (1L << 18);
        var behavior = MacOverlayInput.GetOverlayCollectionBehavior(incompatible | unrelated, true);
        Assert.That(behavior, Is.EqualTo(unrelated | overlayBehavior));
        Assert.That(MacOverlayInput.GetOverlayCollectionBehavior(behavior, true), Is.EqualTo(behavior));
    }

    [Test]
    public void OlderMacOSKeepsLegacySpaceBehaviorWithoutNewApplicationFlag()
    {
        Assert.That(MacOverlayInput.GetOverlayCollectionBehavior(0, false), Is.EqualTo((1L << 0) | (1L << 8)));
    }

    [TestCase(0, 100)]
    [TestCase(100, -1)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void CaptureRejectsInvalidDimensionsBeforeNativeCalls(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinuxScreenCapturer().CaptureScreenArea(0, 0, width, height));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MacOSScreenCapturer().CaptureScreenArea(0, 0, width, height));
    }
}
