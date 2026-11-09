Native desktop overlay integration checks

Run on Linux with .NET 8 runtime/.NET SDK, Xvfb, XTEST, XFixes, Xext,
Openbox, KWin X11/Wayland, XWayland and dbus-run-session installed:

  bash SWTORCombatParser_Test/PlatformHarness/run.sh x11
  bash SWTORCombatParser_Test/PlatformHarness/run.sh openbox
  bash SWTORCombatParser_Test/PlatformHarness/run.sh kwin
  bash SWTORCombatParser_Test/PlatformHarness/run.sh wayland

The Dockerfile supplies a disposable Debian environment. Build from repository root:
  docker build -t orbs-platform-test -f SWTORCombatParser_Test/PlatformHarness/Dockerfile SWTORCombatParser_Test/PlatformHarness

Copy the checkout into a temporary container work directory (or use a disposable
checkout as a bind mount) before building. The harness links the production X11
and capture classes. It does not start Orbs or read/change real Orbs settings.

Tests use XTEST to click real stacked windows with a renderer child. They check
delivery to the underlying game window when locked, child input when unlocked,
resize/remap recovery, both XFixes and SHAPE paths, RGB screen capture, invalid
handles, destroyed-window errors, and forwarding to another managed X11 handler.
The Wayland run uses KWin's virtual output and XWayland windows; it verifies an
explicit capture limitation instead of incorrectly treating XWayland's root as
the Wayland desktop. This does not simulate a native Wayland game, physical input,
GPU drivers, fullscreen direct scanout, mixed-monitor scaling, or a full Plasma UI.

Also run the main NUnit OverlayPlatformTests and overlay/raid-frame regressions.
macOS needs a real Mac smoke test of permissions, Retina/multiple monitors, Spaces,
lock/unlock, hide/show, resizing, and input reaching the game; compilation alone
does not validate AppKit behavior.
