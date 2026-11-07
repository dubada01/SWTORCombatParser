#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$script:StoreBase = 'https://manage.devcenter.microsoft.com/v1.0/my'
$script:AppId = '9P63T6ZQMHBZ'
$script:Identity = '37053DubaTech.OldRepublicBattleParser'
$script:Publisher = 'CN=BA3DD863-3798-48E1-9914-1F93B78F55BD'

function Get-OrbsStorePackageInfo {
    param([Parameter(Mandatory)][string]$PackagePath)
    $path = (Resolve-Path -LiteralPath $PackagePath).Path
    if ([IO.Path]::GetExtension($path) -ne '.appxupload') { throw 'Use the Store .appxupload artifact from Build-StorePackage.ps1.' }
    $upload = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $packages = @($upload.Entries | Where-Object { $_.FullName.EndsWith('.appx') })
        if ($packages.Count -ne 1) { throw 'Expected exactly one x64 app package inside the upload.' }
        $stream = $packages[0].Open()
        $package = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
        try {
            $entry = $package.GetEntry('AppxManifest.xml')
            if (!$entry) { throw 'Package manifest is missing.' }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($manifest.Package.Identity.Name -ne $script:Identity -or $manifest.Package.Identity.Publisher -ne $script:Publisher) {
                throw 'This is not the Orbs Store identity. Never upload the separately named local test package.'
            }
            if ($manifest.Package.Identity.ProcessorArchitecture -ne 'x64') { throw 'This release script currently supports the x64 Orbs package only.' }
            if ($manifest.Package.Applications.Application.EntryPoint -ne 'Windows.FullTrustApplication') { throw 'Expected the full-trust Orbs desktop application.' }
            $version = [version]$manifest.Package.Identity.Version
            if ($version.Revision -ne 0) { throw 'Store package versions must end in .0.' }
            [pscustomobject]@{
                AppId = $script:AppId; Identity = $script:Identity; Version = $version.ToString()
                PackagePath = $path; FileName = [IO.Path]::GetFileName($path)
                SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            }
        } finally { $package.Dispose(); $stream.Dispose() }
    } finally { $upload.Dispose() }
}

function New-OrbsSubmissionData {
    param([hashtable]$Submission, [ValidateSet('Flight','Production')][string]$Target,
        [string]$FileName, [ValidateSet('Immediate','Manual')][string]$PublishMode)
    # Deep-copy the published submission. Preserve listings, prices, permissions,
    # rollout options and older packages, including architectures we do not build.
    $updated = $Submission | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $key = if ($Target -eq 'Flight') { 'flightPackages' } else { 'applicationPackages' }
    $existing = @($updated[$key] | Where-Object { $null -ne $_ })
    if ($existing | Where-Object { $_.fileName -eq $FileName -and $_.fileStatus -ne 'PendingDelete' }) {
        throw 'This package filename already exists in the submission. Check its status or build a newer version.'
    }
    $updated[$key] = $existing + @(@{
        fileName = $FileName; fileStatus = 'PendingUpload'
        minimumDirectXVersion = 'None'; minimumSystemRam = 'None'
    })
    $updated.targetPublishMode = $PublishMode
    $updated.targetPublishDate = '1601-01-01T00:00:00Z'
    return $updated
}

function Get-OrbsStoreToken {
    $tenantValue = $env:PARTNER_CENTER_TENANT_ID
    $clientValue = $env:PARTNER_CENTER_CLIENT_ID
    $secretValue = $env:PARTNER_CENTER_CLIENT_SECRET
    if (!$tenantValue -and !$clientValue -and !$secretValue) {
        $credentialPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DubaTech/StorePublishing/credentials.clixml'
        if (Test-Path -LiteralPath $credentialPath) {
            $saved = Import-Clixml -LiteralPath $credentialPath
            $tenantValue = $saved.TenantId
            $clientValue = $saved.Credential.UserName
            $secretValue = $saved.Credential.GetNetworkCredential().Password
        }
    }
    if (!$tenantValue -or !$clientValue -or !$secretValue) {
        throw 'Configure the encrypted local Store credentials or set all three PARTNER_CENTER environment variables. Do not put secrets in source control.'
    }
    $tenant = [guid]$tenantValue
    $client = [guid]$clientValue
    try {
        $token = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$tenant/oauth2/token" `
            -ContentType 'application/x-www-form-urlencoded' -TimeoutSec 120 -MaximumRedirection 0 -Body @{
                grant_type = 'client_credentials'; client_id = $client.ToString()
                client_secret = $secretValue
                resource = 'https://manage.devcenter.microsoft.com'
            }
        if (!$token.access_token) { throw 'No token returned.' }
        return $token.access_token
    } catch { throw 'Partner Center authentication failed. Check the tenant, registered application, client-secret expiry and Manager role. Credential details were not logged.' }
    finally { $secretValue = $null }
}

function Invoke-OrbsStoreRequest {
    param([ValidateSet('Get','Post','Put')][string]$Method, [string]$Path, [string]$Token, $Body)
    $request = @{
        Method = $Method; Uri = "$script:StoreBase/$Path"; Headers = @{ Authorization = "Bearer $Token" }
        TimeoutSec = 120; MaximumRedirection = 0; ErrorAction = 'Stop'
    }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 100 -Compress))
    } elseif ($Method -eq 'Post') {
        # The Store API rejects PowerShell's default form content type, even
        # for endpoints such as commit that do not require request fields.
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes('{}')
    }
    try {
        $result = Invoke-RestMethod @request
        if ($null -ne $result) { return ($result | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable) }
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'network error' }
        throw "Partner Center $Method $Path failed ($code). Check account access and pending submissions. No automatic retry or deletion was performed."
    }
}

function Send-OrbsStoreUpload {
    param([string]$UploadUrl, [string]$PackagePath)
    $uri = [uri]$UploadUrl
    if ($uri.Scheme -ne 'https' -or $uri.Port -ne 443 -or !$uri.Host.EndsWith('.blob.core.windows.net') -or !$uri.Query) {
        throw 'Partner Center returned an unexpected upload destination.'
    }
    # The API takes an outer ZIP containing the unchanged .appxupload (symbols
    # included). No unpacking/repacking of the signed-content package is needed.
    $zipPath = Join-Path ([IO.Path]::GetTempPath()) ('orbs-store-' + [guid]::NewGuid() + '.zip')
    try {
        $zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
        try { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $PackagePath, [IO.Path]::GetFileName($PackagePath), [IO.Compression.CompressionLevel]::NoCompression) }
        finally { $zip.Dispose() }
        try {
            Invoke-RestMethod -Method Put -Uri $uri -InFile $zipPath -ContentType 'application/zip' `
                -Headers @{ 'x-ms-blob-type' = 'BlockBlob'; 'x-ms-version' = '2019-12-12'; 'x-ms-date' = [DateTime]::UtcNow.ToString('R') } `
                -TimeoutSec 1800 -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        } catch { throw 'Package upload failed. The temporary SAS upload URL was not logged; the submission was not committed.' }
    } finally { if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force } }
}

function Invoke-OrbsStoreRelease {
    [CmdletBinding()]
    param(
        [ValidateSet('Plan','Submit','Status')][string]$Action = 'Plan',
        [ValidateSet('Flight','Production')][string]$Target = 'Flight',
        [string]$PackagePath, [string]$FlightId, [string]$SubmissionId,
        [ValidateSet('Immediate','Manual')][string]$PublishMode
    )
    if (!$PublishMode) { $PublishMode = if ($Target -eq 'Flight') { 'Immediate' } else { 'Manual' } }
    if ($Target -eq 'Production' -and $FlightId) { throw 'Production does not accept a FlightId.' }
    if ($FlightId) { [void][guid]::Parse($FlightId) }
    if ($Action -ne 'Plan' -and $Target -eq 'Flight' -and !$FlightId) { throw 'Choose the existing flight using -FlightId.' }
    if ($Action -eq 'Status' -and $SubmissionId -notmatch '^\d+$') { throw 'Status requires the numeric -SubmissionId returned by Submit.' }
    if ($Action -eq 'Submit' -and $SubmissionId -and ($Target -ne 'Flight' -or $SubmissionId -notmatch '^\d+$')) { throw 'Resuming a draft requires a numeric flight SubmissionId.' }
    if ($Action -ne 'Status') {
        if (!$PackagePath) { throw 'Provide the Store .appxupload using -PackagePath.' }
        $package = Get-OrbsStorePackageInfo -PackagePath $PackagePath
    }
    if ($Action -eq 'Plan') {
        return [pscustomobject]@{ Action='Plan'; Target=$Target; FlightId=$FlightId; PublishMode=$PublishMode
            AppId=$script:AppId; Version=$package.Version; SHA256=$package.SHA256; PackagePath=$package.PackagePath; NetworkCalls=0 }
    }
    $token = Get-OrbsStoreToken
    $appPath = "applications/$script:AppId"
    $targetPath = if ($Target -eq 'Flight') { "$appPath/flights/$FlightId" } else { $appPath }
    if ($Action -eq 'Status') {
        $status = Invoke-OrbsStoreRequest -Method Get -Path "$targetPath/submissions/$SubmissionId/status" -Token $token
        return [pscustomobject]@{ AppId=$script:AppId; Target=$Target; FlightId=$FlightId; SubmissionId=$SubmissionId; Status=$status.status; Details=$status.statusDetails }
    }
    $app = Invoke-OrbsStoreRequest -Method Get -Path $appPath -Token $token
    if ($app.packageIdentityName -ne $script:Identity -or $app.publisherName -ne $script:Publisher) { throw 'The Partner Center app does not match the validated package identity/publisher.' }
    $targetInfo = $app
    $pendingKey = 'pendingApplicationSubmission'
    $publishedKey = 'lastPublishedApplicationSubmission'
    $packageKey = 'applicationPackages'
    if ($Target -eq 'Flight') {
        $targetInfo = Invoke-OrbsStoreRequest -Method Get -Path $targetPath -Token $token
        if (!@($targetInfo.groupIds | Where-Object { ![string]::IsNullOrWhiteSpace($_) }).Count) { throw 'The flight must have a tester group before submitting.' }
        $pendingKey = 'pendingFlightSubmission'; $publishedKey = 'lastPublishedFlightSubmission'; $packageKey = 'flightPackages'
    }
    $pendingId = [string]$targetInfo[$pendingKey].id
    if ($pendingId -and (!$SubmissionId -or $SubmissionId -ne $pendingId)) { throw "A pending submission already exists: $pendingId. It was not changed or deleted." }
    if ($SubmissionId -and !$pendingId) { throw 'The specified draft is not the current pending flight submission.' }
    if ($targetInfo[$publishedKey].id) {
        $published = Invoke-OrbsStoreRequest -Method Get -Path "$targetPath/submissions/$($targetInfo[$publishedKey].id)" -Token $token
        foreach ($old in $published[$packageKey]) {
            if ($old.version -and $old.architecture -eq 'x64' -and [version]$old.version -ge [version]$package.Version) {
                throw "Version $($package.Version) is not newer than the target's existing x64 package $($old.version)."
            }
        }
    }
    if ($SubmissionId) {
        $status = Invoke-OrbsStoreRequest -Method Get -Path "$targetPath/submissions/$SubmissionId/status" -Token $token
        if ($status.status -ne 'PendingCommit') { throw 'The specified flight draft is not awaiting submission.' }
        $submission = Invoke-OrbsStoreRequest -Method Get -Path "$targetPath/submissions/$SubmissionId" -Token $token
    } else {
        $submission = Invoke-OrbsStoreRequest -Method Post -Path "$targetPath/submissions" -Token $token
    }
    $newId = [string]$submission.id
    if ($newId -notmatch '^\d+$') { throw 'The API did not return a valid submission ID. Inspect Partner Center before retrying.' }
    try {
        $updated = New-OrbsSubmissionData -Submission $submission -Target $Target -FileName $package.FileName -PublishMode $PublishMode
        Invoke-OrbsStoreRequest -Method Put -Path "$targetPath/submissions/$newId" -Token $token -Body $updated | Out-Null
        Send-OrbsStoreUpload -UploadUrl $submission.fileUploadUrl -PackagePath $package.PackagePath
        Invoke-OrbsStoreRequest -Method Post -Path "$targetPath/submissions/$newId/commit" -Token $token | Out-Null
        [pscustomobject]@{ AppId=$script:AppId; Target=$Target; FlightId=$FlightId; SubmissionId=$newId
            Version=$package.Version; SHA256=$package.SHA256; PublishMode=$PublishMode; Status='Commit requested; certification not yet verified' }
    } catch {
        Write-Warning "Submission $newId was retained. Inspect its status before retrying; this script never replaces an existing draft."
        throw
    }
}

Export-ModuleMember -Function Invoke-OrbsStoreRelease
