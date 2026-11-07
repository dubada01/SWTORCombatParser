#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildDirectory,
    [switch]$TrustCertificateOnly
)
$ErrorActionPreference = 'Stop'
$BuildDirectory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$certificatePath = Join-Path $BuildDirectory 'TestSigning.cer'
$certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($certificatePath)
$packages = @(Get-ChildItem -LiteralPath $BuildDirectory -Recurse -File -Filter '*.appx')
if ($packages.Count -ne 1) { throw 'Expected exactly one local test package.' }
$packagePath = $packages[0].FullName
$signature = Get-AuthenticodeSignature -LiteralPath $packagePath
if (!$signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) { throw 'The package signer does not match TestSigning.cer.' }
if ($certificate.Subject -ne 'CN=BA3DD863-3798-48E1-9914-1F93B78F55BD' -or $certificate.NotAfter -le (Get-Date)) { throw 'Unexpected or expired test certificate.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $reader = New-Object IO.StreamReader($zip.GetEntry('AppxManifest.xml').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifest.Package.Identity.Name -ne 'DubaTech.Orbs.PackagingTest') { throw 'Refusing to install a Store identity; this helper is only for side-by-side tests.' }
} finally { $zip.Dispose() }
$trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
if ($TrustCertificateOnly) {
    # This is the only elevated operation. Trust one public leaf certificate, not
    # a root CA. Installation/registration happens as the original desktop user.
    Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    exit 0
}
if (!(Test-Path $trustedPath)) {
    Write-Host 'Windows will ask for administrator approval to trust this local test signing certificate.'
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -BuildDirectory "{1}" -TrustCertificateOnly' -f $PSCommandPath, $BuildDirectory
    $approval = Start-Process -FilePath powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $arguments -PassThru -Wait
    if ($approval.ExitCode -ne 0 -or !(Test-Path $trustedPath)) { throw 'Test certificate was not installed. The Store app has not been changed.' }
}
if ((Get-AuthenticodeSignature -LiteralPath $packagePath).Status -ne 'Valid') { throw 'Package signature validation failed.' }
if (Get-Process Orbs -ErrorAction SilentlyContinue) { throw 'Close Orbs before installing/testing; Orbs allows only one running instance.' }
$backup = Join-Path $BuildDirectory ('DataBackup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
$sources = @{
    DesktopAppData = Join-Path $env:LOCALAPPDATA 'DubaTech/SWTORCombatParser'
    StoreLocalCache = Join-Path $env:LOCALAPPDATA 'Packages/37053DubaTech.OldRepublicBattleParser_efsdnxq7216xe/LocalCache'
}
foreach ($name in $sources.Keys) {
    if (Test-Path -LiteralPath $sources[$name]) {
        # Windows PowerShell's Copy-Item fails on long combat-cache paths.
        # Robocopy handles them without changing system long-path policy.
        & robocopy.exe $sources[$name] (Join-Path $backup $name) /E /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS *> (Join-Path $backup "$name-backup.log")
        if ($LASTEXITCODE -ge 8) { throw "Backup failed; see $backup. The package was not installed." }
    }
}
Write-Host "Orbs shares local settings between installations. Pre-test backup: $backup"
Add-AppxPackage -Path $packagePath
Get-AppxPackage -Name 'DubaTech.Orbs.PackagingTest' | Select-Object Name, Version, PackageFamilyName, InstallLocation
Write-Host 'Installed. Launch Orbs (Packaging Test) from Start. The regular Store app remains installed.'
