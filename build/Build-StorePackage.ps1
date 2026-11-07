#Requires -Version 5.1
<#
.SYNOPSIS
Build a complete Store upload or a side-by-side, signed local test package.
.EXAMPLE
.\build\Build-StorePackage.ps1 -Channel Store
.EXAMPLE
.\build\Build-StorePackage.ps1 -Channel LocalTest -CertificateThumbprint YOUR_CERT_THUMBPRINT
#>
[CmdletBinding()]
param(
    [ValidateSet('Store', 'LocalTest')][string]$Channel = 'Store',
    [string]$Version,
    [string]$CertificateThumbprint,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$projectDirectory = Join-Path $repo 'Orbs Package Builder'
[xml]$desktopProject = Get-Content (Join-Path $repo 'SWTORCombatParser.csproj') -Raw
if (!$Version) { $Version = $desktopProject.SelectSingleNode('/Project/PropertyGroup/VersionPrefix').InnerText }
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') {
    throw 'Use a four-part Store version ending in .0, for example 2.3.1.0.'
}
$parsedVersion = [version]$Version
if ($parsedVersion.Major -eq 0 -or $parsedVersion.Major -gt 65535 -or $parsedVersion.Minor -gt 65535 -or $parsedVersion.Build -gt 65535) {
    throw 'The version must have a nonzero major component and components no greater than 65535.'
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path $vswhere)) { throw 'Install Visual Studio with MSBuild and the Windows Application Packaging Project tools.' }
$vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if (!$vs) { throw 'Visual Studio MSBuild was not found.' }
$msbuild = Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe'
$signTool = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin/10.0.22621.0/x64/signtool.exe'
[xml]$manifest = Get-Content (Join-Path $projectDirectory 'Package.appxmanifest') -Raw
$manifest.Package.Identity.Version = $Version
$expectedName = [string]$manifest.Package.Identity.Name
$certificate = $null
if ($Channel -eq 'LocalTest') {
    if (!$CertificateThumbprint) { throw 'LocalTest requires -CertificateThumbprint for a valid code-signing certificate in Cert:\CurrentUser\My.' }
    $CertificateThumbprint = $CertificateThumbprint -replace '\s', ''
    $certificate = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint"
    if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
        throw 'The test signing certificate must be valid and have its private key.'
    }
    if ($certificate.Subject -ne [string]$manifest.Package.Identity.Publisher) { throw 'The certificate subject must match the manifest Publisher.' }
    if (!($certificate.EnhancedKeyUsageList | Where-Object { [string]$_.ObjectId -eq '1.3.6.1.5.5.7.3.3' })) { throw 'The certificate must allow code signing.' }
    if (!(Test-Path $signTool)) { throw 'Install the Windows 10 SDK 10.0.22621.0 signing tools.' }
    $expectedName = 'DubaTech.Orbs.PackagingTest'
    $manifest.Package.Identity.Name = $expectedName
    $manifest.Package.Properties.DisplayName = 'Orbs (Packaging Test)'
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
    $manifest.SelectSingleNode('//uap:VisualElements', $ns).SetAttribute('DisplayName', 'Orbs (Packaging Test)')
    $manifest.SelectSingleNode('//uap:DefaultTile', $ns).SetAttribute('ShortName', 'Orbs Test')
}
elseif ($CertificateThumbprint) { throw 'Store builds are unsigned; only LocalTest needs a certificate.' }

if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repo ('.store-packages/{0}-{1}-{2}' -f $Channel, $Version, (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0) {
    throw 'Choose an empty output directory so old artifacts cannot be mistaken for this build.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$generatedManifest = Join-Path $OutputDirectory 'Package.appxmanifest'
$manifest.Save($generatedManifest)
$isTest = ($Channel -eq 'LocalTest').ToString().ToLowerInvariant()
$buildMode = if ($Channel -eq 'Store') { 'StoreUpload' } else { 'SideloadOnly' }
$arguments = @(
    (Join-Path $projectDirectory 'Orbs Package Builder.wapproj'), '/restore', '/t:Rebuild', '/nologo', '/v:minimal',
    '/p:Configuration=Release', '/p:Platform=x64', '/p:RuntimeIdentifier=win-x64', '/p:SelfContained=true',
    '/p:GenerateAppxPackageOnBuild=true', '/p:AppxPackageSigningEnabled=false', '/p:AppxBundle=Never',
    "/p:UapAppxPackageBuildMode=$buildMode", "/p:OrbsLocalTest=$isTest", "/p:OrbsPackageManifest=$generatedManifest",
    "/p:AppxPackageDir=$OutputDirectory/", "/p:Version=$Version"
)
$buildLog = Join-Path $OutputDirectory 'build.log'
Write-Host "Building $Channel $Version (full log: $buildLog)..."
& $msbuild @arguments *> $buildLog
if ($LASTEXITCODE -ne 0) {
    Get-Content $buildLog -Tail 35 | Write-Host
    throw "Packaging failed. See $buildLog"
}
$packages = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File -Filter '*.appx')
if ($packages.Count -ne 1) { throw "Expected one x64 app package, found $($packages.Count)." }
$packagePath = $packages[0].FullName

# Inspect the actual package without unpacking or modifying it. Fail before signing
# if the SDK produced the wrong identity, launcher, runtime, or missing assets.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $reader = New-Object IO.StreamReader($zip.GetEntry('AppxManifest.xml').Open())
    try { [xml]$builtManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($builtManifest.Package.Identity.Name -ne $expectedName -or $builtManifest.Package.Identity.Version -ne $Version) { throw 'Built package identity/version does not match the requested channel.' }
    $app = $builtManifest.Package.Applications.Application
    if ($app.EntryPoint -ne 'Windows.FullTrustApplication' -or ($app.Executable -replace '\\', '/') -ne 'SWTORCombatParser/Orbs.exe') { throw 'Package must activate Orbs.exe directly as a full-trust desktop application.' }
    if (@($zip.Entries | Where-Object FullName -match '(?i)PSF(Launcher|Runtime)').Count) { throw 'Unexpected PSF launcher/runtime in package.' }
    $required = @('SWTORCombatParser/Orbs.exe', 'SWTORCombatParser/Orbs.dll', 'SWTORCombatParser/coreclr.dll',
        'SWTORCombatParser/hostfxr.dll', 'SWTORCombatParser/resources/packagedIcons.zip',
        'SWTORCombatParser/DataStructures/ClassInfos/Classes.json')
    foreach ($name in $required) { if (!$zip.GetEntry($name)) { throw "Package is missing $name" } }
    foreach ($nativeName in @('libSkiaSharp.dll', 'libvlc.dll', 'libvlccore.dll')) {
        if (!@($zip.Entries | Where-Object { $_.Name -eq $nativeName }).Count) { throw "Package is missing $nativeName" }
    }
    $runtimeReader = New-Object IO.StreamReader($zip.GetEntry('SWTORCombatParser/Orbs.runtimeconfig.json').Open())
    try { $runtime = $runtimeReader.ReadToEnd() | ConvertFrom-Json } finally { $runtimeReader.Dispose() }
    if (!$runtime.runtimeOptions.PSObject.Properties['includedFrameworks']) { throw 'The package must include its .NET runtime.' }
    $payloadHashes = [ordered]@{}
    $packageContents = @{}
    foreach ($entry in ($zip.Entries | Sort-Object FullName)) {
        $sha = [Security.Cryptography.SHA256]::Create()
        $stream = $entry.Open()
        try {
            $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
            $packageContents[$entry.FullName] = $hash
            if ($entry.FullName.StartsWith('SWTORCombatParser/') -and $entry.Name) { $payloadHashes[$entry.FullName] = $hash }
        }
        finally { $stream.Dispose(); $sha.Dispose() }
    }
} finally { $zip.Dispose() }

if ($Channel -eq 'LocalTest') {
    & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'Test package signing failed.' }
    # Public certificate only: never export the private key or automatically change trust.
    Export-Certificate -Cert $certificate -FilePath (Join-Path $OutputDirectory 'TestSigning.cer') | Out-Null
}
$uploadPath = $null
if ($Channel -eq 'Store') {
    $uploads = @(Get-ChildItem -LiteralPath $OutputDirectory -File -Filter '*.appxupload')
    if ($uploads.Count -ne 1) { throw 'The Store upload artifact was not generated.' }
    $uploadPath = $uploads[0].FullName
    $upload = [IO.Compression.ZipFile]::OpenRead($uploadPath)
    try {
        $embeddedPackages = @($upload.Entries | Where-Object { $_.FullName.EndsWith('.appx') })
        if ($embeddedPackages.Count -ne 1) { throw 'The upload must contain exactly one validated app package.' }
        $stream = $embeddedPackages[0].Open()
        $embedded = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
        try {
            # The SDK packs twice; ZIP timestamps can differ even with identical
            # content. Compare every file, including the manifest and block map.
            if ($embedded.Entries.Count -ne $packageContents.Count) { throw 'Store upload has a different file count.' }
            foreach ($entry in $embedded.Entries) {
                $content = $entry.Open()
                $sha = [Security.Cryptography.SHA256]::Create()
                try { $hash = [BitConverter]::ToString($sha.ComputeHash($content)).Replace('-', '').ToLowerInvariant() }
                finally { $content.Dispose(); $sha.Dispose() }
                if ($packageContents[$entry.FullName] -ne $hash) { throw "Store upload content differs: $($entry.FullName)" }
            }
        } finally { $embedded.Dispose(); $stream.Dispose() }
    } finally { $upload.Dispose() }
}
$summary = [ordered]@{
    Channel = $Channel; Version = $Version; Identity = $expectedName
    Package = $packagePath; StoreUpload = $uploadPath
    SHA256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
    Executable = [string]$app.Executable; EntryPoint = [string]$app.EntryPoint
    SelfContained = $true; PayloadHashes = $payloadHashes
}
$summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'verification.json') -Encoding UTF8
Write-Host "Package: $packagePath"
if ($uploadPath) { Write-Host "Upload this file to Partner Center: $uploadPath" }
else { Write-Host 'Local test only. This separate identity does not replace the Store app, but Orbs shares local settings. Use Install-LocalTestPackage.ps1 to back them up before testing. See publishing_instructions.txt.' }
