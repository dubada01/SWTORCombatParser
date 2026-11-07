#Requires -Version 7.2
<#
.SYNOPSIS
Preview, submit, or check an Orbs Store flight/production update.
.DESCRIPTION
Plan is offline. Submit creates, uploads and commits a submission for certification.
Flights publish after certification by default; production defaults to a manual
Partner Center release. Set -PublishMode Immediate for an automatic public release.
Credentials are read from PARTNER_CENTER_TENANT_ID, PARTNER_CENTER_CLIENT_ID
and PARTNER_CENTER_CLIENT_SECRET, or the Windows-encrypted local credential
file documented in STORE-AUTOMATION.txt when all three variables are absent.
#>
[CmdletBinding()]
param(
    [ValidateSet('Plan','Submit','Status')][string]$Action = 'Plan',
    [ValidateSet('Flight','Production')][string]$Target = 'Flight',
    [string]$PackagePath, [string]$FlightId, [string]$SubmissionId,
    [ValidateSet('Immediate','Manual')][string]$PublishMode
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'StoreSubmission.psm1') -Force
Invoke-OrbsStoreRelease @PSBoundParameters | ConvertTo-Json -Depth 30
