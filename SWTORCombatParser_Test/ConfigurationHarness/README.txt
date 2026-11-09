Configuration UI checks (Windows, .NET 8)

Run from the repository root in PowerShell:

  dotnet build SWTORCombatParser.csproj -c Debug
  dotnet build SWTORCombatParser_Test/ConfigurationHarness/ConfigurationHarness.csproj -c Debug
  dotnet SWTORCombatParser_Test/ConfigurationHarness/bin/Debug/net8.0/ConfigurationHarness.dll "$PWD/obj/configuration-check-1"

Use a new output directory for every run. The harness references the existing
Debug Orbs.dll, so rebuild both projects after production changes. It creates
temporary UI windows, performs checks, writes report.json and PNG renders, and
exits. A nonzero exit code means a failed assertion or startup error. This project
is excluded from the normal NUnit build; build and run it explicitly.

If a running Orbs instance locks the normal build output, build production with
-p:BaseOutputPath=obj/IsolatedBuild/bin/ and build this harness with
-p:OrbsOutputDirectory=<absolute-path-to-isolated-output>/Debug/net8.0.

The harness loads actual production views and viewmodels, with Orbs resources and
styles. It uses isolated settings under output/settings, offline mode, disabled
auto parsing, and a private RunGhostStore. All known persistence-manager paths are
redirected before any production viewmodels are constructed. No production app
startup, live parser, OCR capture, or cloud sharing/import commands are invoked.
The output records redirected paths in isolation.json. No OS input is synthesized.

Snapshots cover phase, challenge, and timer editors; timer health and variable
branches; all three configuration lists with sample encounter rows; metric colors;
settings; all overlay tabs; and the raid frame setup window. Each is rendered at
its default and declared minimum size using RenderTargetBitmap, not screen capture.
The sample DPS and healing metrics are enabled through the actual overlay command.

Behavioral assertions cover typed percentage input through the NumericUpDown
TextBox and LostFocus route; opening the explicit metric color button without
changing selection; live overlay content/background opacity and opaque controls;
clamping/reset; Enter-to-Add routing for all four phase draft lists; and native close versus successful save for edited phases,
challenges, and timers. The actual Timers menu command is checked against its
cached window, source-dropdown selection, and close/reopen. Reverse display-label
binding is checked to preserve the stored timer trigger without throwing.
Timer selection checks cover the immediate Shared fallback, UI-thread updates
after defaults load, switching the actual tab control in both directions, keeping
each tab's selection during source-list refreshes, and detecting a new discipline
without replacing the active encounter selection.
The timer table also renders its sorted minimum-size state
and writes header layout diagnostics. TimerSharePopup is intentionally excluded because it copies
to the real clipboard on open. These checks complement NUnit and RaidFrameHarness;
they do not validate a live SWTOR session or OCR server availability.

The editor interaction suite writes editor-workflows.json. It opens production
manager/editor windows and invokes actual button and checkbox automation peers,
opens dropdowns and changes their selections, and follows their real bindings and
commands. It covers every timer trigger, phase start/end type, challenge type and
variable action; Appearance/Variables switching; nested condition save/cancel;
new/edit/cancel/native close/reopen; Enabled/Active persistence; hidden/audio timer
settings; duplicate/delete of test entries; encounter/boss changes; invalid input
and corrected saves; and repeated phase/challenge menu opens. Assertions check
both visible controls and the isolated persisted records. Dialog ownership is
checked through Window.Owner (native modality does not set IsEffectivelyEnabled).

RaidingEditorChecks also exercises the challenge result dropdown through save and
reopen (damage totals/rates and current/maximum stacks), verifies that unsupported
filters are hidden, checks the phase default and timing labels, and opens timer
behavior using its actual expander toggle. It renders every challenge type and a
health-to-duration phase at normal and minimum window sizes. This catches theme
conflicts such as the app's global ToggleButton template hiding expander labels.

For manual Windows interaction with those same production views, launch a fresh
output directory followed by --interactive. The small "Orbs UI test session"
launcher exposes the actual Timers, Phases, and Challenges menu commands. It uses
the same isolated offline persistence. Close the launcher to end the session.
Interactive mode does not run the assertions automatically.

For before/after comparison, save the original Orbs.dll before building production
changes, build this harness, copy that original DLL over the harness output's
Orbs.dll, and run with a fresh output directory followed by --baseline. That mode
only renders APIs available before the configuration pass and skips new behavior
checks. It does not rebuild or roll back production. Rebuild the harness afterward
to restore the current production DLL before final validation.

Overlay presentation checks run separately with a fresh output directory and
--overlays. They render the production boss, encounter timer, discipline timer,
metric, challenge and race overlays in Compact, Detailed and narrow layouts.
Fixtures include long names, two bosses, DoTs, HP mechanics, an active absorb
shield, populated race checkpoints and invalid/unknown health. The suite opens
the real style dropdowns, verifies independent persisted preferences, changes
styles while locked, and hides/reopens windows. It reads native Windows extended
styles to verify WS_EX_TRANSPARENT, checks saved geometry and non-activation,
and checks the shared opacity controls. The contrast sheet composites these
renders onto a synthetic bright/dark background. No live game, OS clicks,
network requests or user profile changes are involved.

Raid Data refresh checks use --raid-data with a fresh output directory. The real
DataGrid receives 100 rendered value updates and asserts that its columns, rows,
cells, selected player and resized column survive without collection resets or
cell detachments. It also checks live totals, changing sort ranks, header sorting,
adding/removing metrics, 24-player scrolling, joins/departures, combat switching,
replacement character metadata, background-thread updates and reopening.
Sorting assertions compare the displayed row order using visual positions, not
just the model collection. They cover both directions, switching metrics, saved
sort settings, stable ties, and selection/scroll retention during rank changes.
Use --raid-data-interactive for real Windows header clicks against the same grid
with live updates every two seconds. It writes model/displayed order and the
saved sort to sort-state.json. Close the test window to end that session.

Log Review checks use --log-review with a fresh output directory. The production
page receives 200,000 synthetic events and verifies lazy row creation, actual
row virtualization, delayed icons, field/phrase/exclusion/alternative searches,
regex errors, dropdowns, participant filtering, selected-event details, context
and return navigation, deep scrolling, unchanged refreshes and live appends.
Participant direction checks use the real Source/Target/Either dropdown with
event categories, multiple participants, surrounding context, and return/reset.
Assertions also cover query cancellation, no-match states, narrow layout,
reopening, combat changes, and death-review seeking from a background caller.
Jedipedia checks open the actual lookup menu, change the selected row, reopen it,
and verify ability/effect commands and unavailable IDs without launching a browser.
report.json contains timing observations; search-profile files separate worker
filtering from UI publication. Overall dispatcher timings include test assertions
and screenshot rendering and are not a frame-rate benchmark.
Use --log-review-interactive for manual keyboard/mouse verification against the
same page and large fixture. Close the test window to exit. The copy-ID and
open-log actions are exposed but the automatic checks do not invoke them.
