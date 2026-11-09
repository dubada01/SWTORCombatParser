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

Linux startup regression (self-contained Linux x64 publish mounted at /app)
Run check-startup.py inside a disposable Debian-based Docker container, as root,
with Python 3, Xvfb, libX11 and libXtst. Do not run on a workstation: the test
temporarily renames the container's libssl.so.3 and restores it in finally.
Mount this harness read-only at /checks and the published app read-only at /app.
Use --network none so the test has no external connectivity. Start a local Xvfb
server and run Python with DISPLAY set, for example inside the container:
  Xvfb :77 -screen 0 1920x1080x24 -nolisten tcp -ac &
  export DISPLAY=:77
  python3 /checks/check-startup.py
Wait for Xvfb to become ready before Python. The test checks --check-runtime with
and without OpenSSL, sends Return to the actual Continue offline button, verifies
the saved preference, recovers an empty phase file with a backup, and confirms
the main window opens on both the first launch and a saved-offline restart.

macOS needs a real Mac smoke test of permissions, Retina/multiple monitors, Spaces,
lock/unlock, hide/show, resizing, and input reaching the game; compilation alone
does not validate AppKit behavior.
