Raid frame UI checks (Windows, .NET 8)

Run from the repository root in PowerShell:

  dotnet build SWTORCombatParser.csproj -c Debug
  dotnet build SWTORCombatParser_Test/RaidFrameHarness/RaidFrameHarness.csproj -c Debug
  dotnet SWTORCombatParser_Test/RaidFrameHarness/bin/Debug/net8.0/RaidFrameHarness.dll "$PWD/obj/raid-frame-check-1"

Use a new output directory for every run. The harness references the existing
Debug Orbs.dll; rebuild both projects after changing production code. It opens
temporary UI windows, performs the checks, and exits. A nonzero exit code means
a failed check or startup error; report.json lists completed checks and errors.

The harness loads the actual raid overlay and settings views with the production
styles. It redirects settings, logs, and overlay persistence into output/settings
before constructing them. It does not start the full app, parser, or timer engine.
Offline logging is enabled. Production settings and saved layouts are untouched.

Synthetic screen coordinates are passed directly to the view's click handler;
synthetic target-clear and target-set entries are passed to the viewmodel. There
are no simulated OS clicks. Manual mode briefly installs its normal Windows mouse
hook so subscription, combat pause/resume, Finish, close, lock, resize, and disable
cleanup are exercised. Other checks cover fresh defaults, rendered grid bounds,
name swaps, accented names, duplicate placements, invalid dimensions, and timeouts.

setup.png, overlay-default-size.png, overlay.png, and manual-instructions.png are
rendered directly from these views using RenderTargetBitmap, not screen capture.
No images are uploaded and no OCR service calls are made. Hidden-overlay detection
is checked; live game input, screen capture, and OCR networking require separate
validation. Unit fixtures cover OCR image encoding, response parsing, and scaling.

The harness is excluded from the NUnit project; build/run it explicitly as above.
