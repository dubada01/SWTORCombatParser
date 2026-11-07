#Requires -Version 7.2
# Offline contract tests: all authentication, API calls and uploads are mocked.
$ErrorActionPreference = 'Stop'
$script:passed = 0
function Assert-That($condition, $message) {
    if (!$condition) { throw $message }
    $script:passed++
}
function Assert-Throws([scriptblock]$action, [string]$pattern) {
    try { & $action | Out-Null } catch {
        Assert-That ($_.Exception.Message -match $pattern) "Unexpected error: $($_.Exception.Message)"
        return
    }
    throw "Expected error matching $pattern"
}
$modulePath = Join-Path $PSScriptRoot 'StoreSubmission.psm1'
$fixtureRoot = Join-Path (Split-Path $PSScriptRoot) ('.store-packages/api-tests-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
function New-Fixture($name, $identity) {
    $innerPath = Join-Path $fixtureRoot "$name.appx"
    $inner = [IO.Compression.ZipFile]::Open($innerPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $writer = [IO.StreamWriter]::new($inner.CreateEntry('AppxManifest.xml').Open())
        try { $writer.Write('<Package><Identity Name="' + $identity + '" Publisher="CN=BA3DD863-3798-48E1-9914-1F93B78F55BD" Version="2.3.1.0" ProcessorArchitecture="x64"/><Applications><Application EntryPoint="Windows.FullTrustApplication"/></Applications></Package>') }
        finally { $writer.Dispose() }
    } finally { $inner.Dispose() }
    $outerPath = Join-Path $fixtureRoot "$name.appxupload"
    $outer = [IO.Compression.ZipFile]::Open($outerPath, [IO.Compression.ZipArchiveMode]::Create)
    try { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($outer, $innerPath, "$name.appx") }
    finally { $outer.Dispose() }
    return $outerPath
}
$good = New-Fixture 'Orbs_2.3.1.0_x64' '37053DubaTech.OldRepublicBattleParser'
$bad = New-Fixture 'TestIdentity' 'DubaTech.Orbs.PackagingTest'
$script:state = $null
function Reset-Mocks {
    $module = Import-Module $modulePath -Force -PassThru
    $script:state = @{ Calls=[Collections.Generic.List[string]]::new(); Put=$null; Pending=$false; NoGroups=$false; WrongApp=$false; UploadFails=$false; Version=$null }
    & $module {
        param($state)
        $script:TestState = $state
        function script:Get-OrbsStoreToken { $script:TestState.Calls.Add('Authenticate'); return 'fake-token' }
        function script:Invoke-OrbsStoreRequest {
            param($Method,$Path,$Token,$Body)
            $script:TestState.Calls.Add("$Method $Path")
            if ($Method -eq 'Put') { $script:TestState.Put=$Body; return }
            if ($Path.EndsWith('/status')) { return @{status=$(if($script:TestState.Pending){'PendingCommit'}else{'Published'});statusDetails=@{errors=@()}} }
            if ($Path.EndsWith('/commit')) { return @{} }
            if ($Method -eq 'Get' -and $Path.EndsWith('/submissions/999')) {
                return @{id='999';fileUploadUrl='https://example.blob.core.windows.net/test?sig=mock';flightPackages=@()}
            }
            if ($Method -eq 'Post') {
                return @{id='12345';fileUploadUrl='https://example.blob.core.windows.net/test?sig=mock'
                    applicationPackages=@(@{fileName='old.appx';fileStatus='Uploaded';architecture='ARM64'})
                    flightPackages=@(@{fileName='old.appx';fileStatus='Uploaded';architecture='ARM64'})
                    listings=@{'en-us'=@{baseListing=@{description='Keep this'}}};pricing=@{priceId='Free'} }
            }
            if ($Path -match '/submissions/') {
                return @{applicationPackages=@(@{version=$script:TestState.Version;architecture='x64'})
                    flightPackages=@(@{version=$script:TestState.Version;architecture='x64'})}
            }
            if ($Path -match '/flights/') {
                return @{groupIds=$(if($script:TestState.NoGroups){$null}else{@('tester-group')})
                    pendingFlightSubmission=$(if($script:TestState.Pending){@{id='999'}}else{$null})
                    lastPublishedFlightSubmission=$(if($script:TestState.Version){@{id='111'}}else{$null})}
            }
            return @{packageIdentityName=$(if($script:TestState.WrongApp){'wrong'}else{'37053DubaTech.OldRepublicBattleParser'})
                publisherName='CN=BA3DD863-3798-48E1-9914-1F93B78F55BD'
                pendingApplicationSubmission=$(if($script:TestState.Pending){@{id='999'}}else{$null})
                lastPublishedApplicationSubmission=$(if($script:TestState.Version){@{id='111'}}else{$null})}
        }
        function script:Send-OrbsStoreUpload {
            param($UploadUrl,$PackagePath)
            $script:TestState.Calls.Add('Upload')
            if ($script:TestState.UploadFails) { throw 'Mock upload failure' }
        }
    } $script:state
}
$flight = '11111111-2222-3333-4444-555555555555'
Reset-Mocks
$plan = Invoke-OrbsStoreRelease -PackagePath $good
Assert-That ($plan.Action -eq 'Plan' -and $plan.Version -eq '2.3.1.0') 'Offline plan did not inspect the package.'
Assert-That ($state.Calls.Count -eq 0) 'Plan contacted the network.'
Assert-Throws { Invoke-OrbsStoreRelease -PackagePath $bad } 'not the Orbs Store identity'
Assert-Throws { Invoke-OrbsStoreRelease -Action Submit -PackagePath $good } 'FlightId'
Assert-Throws { Invoke-OrbsStoreRelease -Target Production -FlightId $flight -PackagePath $good } 'does not accept'
Assert-Throws { Invoke-OrbsStoreRelease -Action Submit -Target Production -SubmissionId '999' -PackagePath $good } 'numeric flight SubmissionId'
Assert-Throws { Invoke-OrbsStoreRelease -Action Status -FlightId $flight -SubmissionId '../other' } 'numeric'

Reset-Mocks
$result = Invoke-OrbsStoreRelease -Action Submit -Target Flight -FlightId $flight -PackagePath $good
Assert-That ($result.SubmissionId -eq '12345' -and $result.PublishMode -eq 'Immediate') 'Flight submission result is incorrect.'
Assert-That ($state.Put.flightPackages.Count -eq 2 -and $state.Put.flightPackages[0].architecture -eq 'ARM64') 'Older architectures were removed.'
Assert-That ($state.Put.flightPackages[1].fileStatus -eq 'PendingUpload') 'New package was not marked for upload.'
Assert-That ($state.Calls.IndexOf('Upload') -lt $state.Calls.IndexOf("Post applications/9P63T6ZQMHBZ/flights/$flight/submissions/12345/commit")) 'Commit occurred before upload.'

Reset-Mocks
$state.Pending=$true
$result=Invoke-OrbsStoreRelease -Action Submit -Target Flight -FlightId $flight -SubmissionId '999' -PackagePath $good
Assert-That ($result.SubmissionId -eq '999' -and $state.Put.flightPackages.Count -eq 1) 'Initial flight draft was not resumed.'
Assert-That (@($state.Calls | Where-Object { $_ -match '^Post .*/submissions$' }).Count -eq 0) 'Created an extra draft while resuming.'
Assert-Throws { Invoke-OrbsStoreRelease -Action Submit -Target Flight -FlightId $flight -SubmissionId '998' -PackagePath $good } 'pending submission'

Reset-Mocks
$result = Invoke-OrbsStoreRelease -Action Submit -Target Production -PackagePath $good
Assert-That ($result.PublishMode -eq 'Manual' -and $state.Put.targetPublishMode -eq 'Manual') 'Production must default to a manual release.'
Assert-That ($state.Put.listings.'en-us'.baseListing.description -eq 'Keep this' -and $state.Put.pricing.priceId -eq 'Free') 'Listing or pricing changed.'
Reset-Mocks
$result = Invoke-OrbsStoreRelease -Action Submit -Target Production -PublishMode Immediate -PackagePath $good
Assert-That ($state.Put.targetPublishMode -eq 'Immediate') 'Explicit automatic production release was not honored.'

foreach ($case in @('Pending','NoGroups','WrongApp','Version','UploadFails')) {
    Reset-Mocks
    $state[$case] = if ($case -eq 'Version') { '2.3.1.0' } else { $true }
    $pattern = switch($case){Pending{'pending submission'} NoGroups{'tester group'} WrongApp{'does not match'} Version{'not newer'} UploadFails{'upload failure'}}
    Assert-Throws { Invoke-OrbsStoreRelease -Action Submit -FlightId $flight -PackagePath $good -WarningAction SilentlyContinue } $pattern
    Assert-That (@($state.Calls | Where-Object { $_ -match '/commit$' }).Count -eq 0) "Committed after $case failure."
    if($case -ne 'UploadFails'){Assert-That (@($state.Calls | Where-Object { $_ -like 'Post *' }).Count -eq 0) "Created a draft after $case failure."}
}
Reset-Mocks
$status = Invoke-OrbsStoreRelease -Action Status -Target Production -SubmissionId '12345'
Assert-That ($status.Status -eq 'Published' -and @($state.Calls | Where-Object { $_ -like 'Post *' -or $_ -like 'Put *' }).Count -eq 0) 'Status caused a mutation.'
$module=Import-Module $modulePath -Force -PassThru
$result=& $module {
    function script:Invoke-RestMethod { param($Method,$Uri,$Headers,$TimeoutSec,$MaximumRedirection,$ContentType,$Body,$ErrorAction) return @{contentType=$ContentType;body=[Text.Encoding]::UTF8.GetString($Body)} }
    Invoke-OrbsStoreRequest -Method Post -Path 'fake' -Token 'fake'
}
Assert-That ($result.contentType -eq 'application/json; charset=utf-8' -and $result.body -eq '{}') 'Bodyless POST must send JSON content.'
Write-Output "$script:passed assertions passed; no real authentication, upload, or Partner Center changes."
