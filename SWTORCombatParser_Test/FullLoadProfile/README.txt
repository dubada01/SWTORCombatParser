Full desktop combat-load check

This standalone Windows executable opens the real MainWindow and viewmodels,
loads an explicitly supplied combat log, waits for measured background work and
UI idle, and writes timing, memory and correctness reports. It is not part of the
production executable or the NUnit test assembly, and is not a headless benchmark.

Requirements: Windows desktop session, .NET 8 SDK, PowerShell, and the repository's
normal build dependencies. Use a representative log that contains at least one
completed combat. Do not interact with the profiler window during a measured run.

From the repository root:

  .\SWTORCombatParser_Test\FullLoadProfile\Run-Profile.ps1 -LogPath 'D:\logs\combat.txt'

Cache-reload correctness and five sequential plus five rapid completed selections:

  .\SWTORCombatParser_Test\FullLoadProfile\Run-Profile.ps1 -LogPath 'D:\logs\raid.txt' -Audit -SelectionStress

SelectionStress requires at least five completed boss combats with disk caches
besides the initial selection. It chooses the five largest cached boss combats,
checks every sequential result and the final rapid result, restores the original
selection, and checks the restored plot fingerprint and retained plot dates.
Challenge snapshots check active result ownership and restored numeric values;
their active counts make an empty challenge set explicit. Weak references track
whether previously displayed full combat objects survive the final collections.
It collects additional settled memory samples 5, 10 and 15 seconds after all
selection/initial-load async scopes return. Insufficient input fails explicitly.

Options:
  -OutputDirectory <new directory>  Use an explicit report destination.
  -Configuration Release|Debug    Defaults to Release.
  -NoBuild                         Use the binaries from the previous build.
  -Audit                           Compare every reloaded combat against its
                                   original parsed metrics, class and effects.
  -SelectionStress                 Run the sequential/rapid selection checks.
  -SwitchCombats                   Time first/last/first completed boss selection.
  -Memory                          Keep combats in memory for comparison with
                                   disk caching. Cannot combine with SelectionStress.

The launcher builds the production app, then the standalone harness. It waits
for completion and returns a nonzero exit code on build, startup, loading, parity,
selection, restored-plot or final-memory sampling failure. There are no absolute
performance thresholds: compare timings and settled memory on the same machine.
For comparable timing runs, omit Audit; capturing fingerprints adds diagnostic
work and allocations. Use fresh processes and the same input/options for each run.

Reports default to artifacts/full-load-profile/<unique run>/, which Git ignores.
An explicit destination must not already exist. No personal log path is assumed;
logs and generated reports are not committed. Reports contain local paths,
character names and combat data, so review them before sharing.

Important reports:
  check-result.json                   Overall Passed/Failed and exit code.
  setup-report.json                   Stock phase/challenge definitions validated.
  persistence-isolation.json          Settings paths redirected for this run.
  stage-report.json                   Real load/UI stage timings and allocation.
  ready.json                         Combat counts and initial selected metrics.
  steady-memory.json                 Post-GC memory after initial load.
  parity-audit.json                   Reload parity mismatches, if Audit was used.
  mismatch-*.json                     Details for each parity mismatch.
  fingerprints.json.br               Original fingerprints for comparisons.
  selection-stress.json               Latencies, resource peaks, plot counts and
                                     selected-combat checks for both rounds.
  selection-stress-samples.json       Samples taken approximately every 50 ms.
  selection-stress-final-memory.json  Settled memory after the stress scopes exit.
  *-error.txt                        Diagnostic exceptions, if any.

Stage times may be nested or overlap; do not add them together. Forced collections
are outside selection latency measurements. Immediate per-round memory samples
can retain async/UI temporaries; use the final-memory samples for retained growth.
Process CPU includes desktop accessibility/automation clients inspecting the UI.
Keep those clients consistent between runs; do not attribute idle automation work
to parsing. Surviving older combat references are reported for investigation,
not automatically treated as leaks. Memory pools and UI caches can remain after
old combat objects have been collected.

The harness forces offline mode and disables automatic parsing/timers. Settings,
combat caches and run ghosts are redirected into the report directory. It reads
the supplied log and existing installed icons, and does not modify the source log.
Stock challenge definitions are loaded synchronously before constructing the UI,
representing established settings and avoiding a fresh-profile startup race that
can otherwise leave the challenge updater empty. Stock phases use the normal
MainWindow initialization path. Setup validation fails if definitions are missing.
The launcher exits automatically after reports are complete. To leave the window
open for manual inspection, invoke bin/Release/net8.0/FullLoadProfile.exe directly
with two quoted positional arguments (log path and a new output directory), plus
optional --audit, --selection-stress, --switch or --no-cache, omitting --exit.
Direct invocation still requires the production app and harness to be built first.

For a running app that locks the normal output, build production with
-p:BaseOutputPath=obj/SelectionBuild/bin/ and build this harness with
-p:OrbsOutputDirectory=<absolute-path-to-repository>/obj/SelectionBuild/bin/Debug/net8.0.

Use --review-tabs to exercise the actual Raid Data/Plot/Details/Log Review tab
control, reopening unchanged results, changing combats with other pages hidden,
and retaining/clearing a phase filter across all tabs. review-tabs.json records
the assertions and repeated A/B selection times for the two largest recent fights.
Those times include two 50 ms idle checks. Use a log with completed boss phases.
The selection stress suite requires unloaded row references; the bounded recent
combat cache may still serve a row from memory, especially in the rapid round.
stages-SequentialFive-*.json records cumulative pipeline stages at each selection.
Production keeps at most three recently reviewed combats with a combined 200,000
event limit. Hidden pages now release obsolete results and build on first opening;
an empty plot in a Raid Data-only stress run is expected. --review-tabs checks
that the real plot is populated when opened.

The review-tabs pass also exercises the Metrics flyout and its column limit,
restoring defaults, all five Details categories and three groupings, participant
changes, local search, numeric sorting, and selection retention on reopening.
Plot checks cover rolling-average input, legend controls, effect directions,
clearing highlights, and retaining zoom when live following is disabled.
PNG renders cover all review tabs at 1400x900 and 960x540. The 16-player fixture
also verifies that the graph participant picker uses two compact rows.

Details checks verify the full totals strip after rendering every category and
grouping (including numeric precision), narrowed/widened share bars, reordered
columns, and real horizontal scrolling with rendered body/footer alignment.
Graph checks cover all Points/Trends combinations without replacing data,
saved display preferences, visible-only axis scaling, and the 0-100% health axis.
graph-frame-times.json compares warmed median off-screen renders at 1000x520
with and without event markers (25 frames each). This is a within-ScottPlot
rendering comparison, not a whole-application or cross-library benchmark.
Effects-tray checks use the real collapse/expand commands, verify recovered chart
width and the reopen tab, saved state, reopening Plot while collapsed, and
restoration of the selected effect and its highlight.

Implementation note: reflection deliberately accesses production viewmodels and
diagnostics without adding production test APIs. If private fields or resource
converters change, update this harness along with those changes. FullLoadApp.axaml
contains the production UI resources needed to host MainWindow; the copied
converter-resources.json manifest is loaded from the harness output directory.

Progression and wipe-review interactions:

  dotnet SWTORCombatParser_Test/FullLoadProfile/bin/Debug/net8.0/FullLoadProfile.dll <log-path> <new-output-directory> --progression-review --exit

Uses the actual per-pull Wipe Review command and Sessions entry, then exercises
boss and history dropdowns, attempt/death selection, team assignment, rapid
scope changes, and closing/reopening the window. It compares saved deaths and
last-hit evidence against the original combat cache and verifies that summary
indexing neither hydrates additional full combats nor changes the main selection.
PNG renders cover the operation, boss, history, and minimum-size layouts.
All settings, summaries, and combat caches are isolated under the output folder.
progression-review.json reports assertions and first-use/reopen timings; these
include the harness's 100 ms idle polling and are not raw render benchmarks.

Add --historical-loading to verify immediate fight context, delayed thin progress,
Raid Data appearing before phase publication, warm selection, rapid superseding
selections, and inline cache failure/retry. These checks temporarily gate or fail
individual cache loaders in the isolated process, restoring them afterward.
Their cold-load timings include intentional delays and are not speed benchmarks.
The original fight is restored before other suites run. The progression suite
also seeds three detached synthetic nights to check cross-night team/cohort
filtering, then clears the loaded raid to verify archived recap availability.
