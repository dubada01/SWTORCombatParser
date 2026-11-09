[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LogPath,
    [string]$OutputDirectory,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$Audit,
    [switch]$SelectionStress,
    [switch]$SwitchCombats,
    [switch]$Memory,
    [switch]$LiveReplay,
    [ValidateRange(0.1, 120)]
    [double]$ReplaySpeed = 1,
    [Nullable[datetime]]$ReplayFrom,
    [Nullable[datetime]]$ReplayThrough,
    [switch]$SimulatePartialWrite,
    [switch]$AudioProbe,
    [switch]$FullOverlays
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This full desktop UI check requires Windows and an interactive desktop session.'
}
if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
    throw "Combat log does not exist: $LogPath"
}
if ($Memory -and $SelectionStress) {
    throw 'SelectionStress requires disk caching; omit Memory.'
}
if ($LiveReplay -and -not $PSBoundParameters.ContainsKey('Configuration')) { $Configuration = 'Debug' }
if ($LiveReplay -and $Configuration -ne 'Debug') { throw 'LiveReplay uses the supported Debug replay path. Specify -Configuration Debug.' }
if ($LiveReplay -and ($SelectionStress -or $SwitchCombats -or $Memory)) { throw 'LiveReplay has its own streaming selection/parity checks; omit SelectionStress, SwitchCombats and Memory.' }

$profileLogPath = (Resolve-Path -LiteralPath $LogPath).ProviderPath
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $runName = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $OutputDirectory = Join-Path $repositoryRoot (Join-Path 'artifacts/full-load-profile' $runName)
}
$profileOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location).ProviderPath $OutputDirectory))
}
if (Test-Path -LiteralPath $profileOutput) {
    throw 'Use a new output directory for each run. Existing reports are never overwritten.'
}

if (-not $NoBuild) {
    & dotnet build (Join-Path $repositoryRoot 'SWTORCombatParser.csproj') -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & dotnet build (Join-Path $PSScriptRoot 'FullLoadProfile.csproj') -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$profileExecutable = Join-Path $PSScriptRoot "bin/$Configuration/net8.0/FullLoadProfile.exe"
if (-not (Test-Path -LiteralPath $profileExecutable -PathType Leaf)) {
    throw 'The profile executable is missing. Run again without NoBuild.'
}
# Windows file paths cannot contain quotes. Trim trailing separators so the
# command-line parser cannot interpret the closing quote as an escaped quote.
$profileArguments = @(
    ('"' + $profileLogPath + '"'),
    ('"' + $profileOutput.TrimEnd('\', '/') + '"'),
    '--exit'
)
if ($Memory) { $profileArguments += '--no-cache' }
if ($Audit) { $profileArguments += '--audit' }
if ($SwitchCombats) { $profileArguments += '--switch' }
if ($SelectionStress) { $profileArguments += '--selection-stress' }
if ($LiveReplay) { $profileArguments += '--live-replay'; $profileArguments += '--replay-speed=' + $ReplaySpeed.ToString([Globalization.CultureInfo]::InvariantCulture) }
if ($null -ne $ReplayFrom) { $profileArguments += '--replay-from=' + $ReplayFrom.ToString('O') }
if ($null -ne $ReplayThrough) { $profileArguments += '--replay-through=' + $ReplayThrough.ToString('O') }
if ($SimulatePartialWrite) { $profileArguments += '--partial-write' }
if ($AudioProbe) { $profileArguments += '--audio-probe' }
if ($FullOverlays) { $profileArguments += '--full-overlays' }

Write-Host "Profile reports: $profileOutput"
$profileProcess = Start-Process -FilePath $profileExecutable -ArgumentList $profileArguments `
    -WindowStyle Hidden -PassThru -Wait
$profileProcess.Refresh()
$profileExitCode = $profileProcess.ExitCode
if ($null -eq $profileExitCode) {
    throw 'The profiler process did not report an exit code.'
}
if ($profileExitCode -ne 0) {
    Write-Error "Full UI check failed (exit $profileExitCode). Inspect $profileOutput" -ErrorAction Continue
    exit $profileExitCode
}
$resultPath = Join-Path $profileOutput 'check-result.json'
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw 'The process ended without a completed diagnostic report.'
}
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.Status -ne 'Passed') {
    throw "Full UI check reported failure. Inspect $profileOutput"
}
Write-Host "Full UI check passed. Reports: $profileOutput"
exit 0
