Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$maintenance = Join-Path $repository 'deployment\iis\Invoke-IisMaintenance.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('mercado-iis-script-test-' + [Guid]::NewGuid().ToString('N'))
$site = Join-Path $root 'site'
$data = Join-Path $root 'data'
$offline = Join-Path $site 'app_offline.htm'
New-Item -ItemType Directory -Path $site -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $data 'database') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $site 'MercadoCercano.Api.dll'), 'isolated placeholder; never executed')
[IO.File]::WriteAllText((Join-Path $site 'appsettings.Production.json'), (@{ Storage = @{ Root = $data } } | ConvertTo-Json))
$global:IisMaintenanceTestState = @{ Fail = $false; Calls = @(); Events = @(); Offline = $offline; ReplaceOffline = $false }
function dotnet {
    if (-not (Test-Path -LiteralPath $global:IisMaintenanceTestState.Offline)) { throw 'CLI must run while IIS is offline.' }
    $global:IisMaintenanceTestState.Calls += ($args -join ' ')
    if ($global:IisMaintenanceTestState.ReplaceOffline) { [IO.File]::WriteAllText($global:IisMaintenanceTestState.Offline, 'changed by another operator') }
    if ($global:IisMaintenanceTestState.Fail) { $global:LASTEXITCODE = 1 } else { $global:LASTEXITCODE = 0 }
}
function Write-EventLog {
    param($LogName, $Source, $EntryType, $EventId, $Message)
    $global:IisMaintenanceTestState.Events += $EventId
}
function Assert-True($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Fails([scriptblock]$Operation) {
    $failed = $false
    try { & $Operation } catch { $failed = $true }
    Assert-True $failed 'Expected an explicit failure.'
}
try {
    & $maintenance -SitePath $site -Command backup
    Assert-True (-not (Test-Path -LiteralPath $offline)) 'Successful backup must reopen the site.'
    Assert-True ($global:IisMaintenanceTestState.Events -contains 1000) 'Successful operations must be logged.'
    $global:IisMaintenanceTestState.Fail = $true
    Assert-Fails { & $maintenance -SitePath $site -Command backup }
    Assert-True (-not (Test-Path -LiteralPath $offline)) 'Failed read-only backup must reopen the site.'
    Assert-Fails { & $maintenance -SitePath $site -Command migrate }
    Assert-True (Test-Path -LiteralPath $offline) 'Failed migration must leave the site offline.'
    Remove-Item -LiteralPath $offline
    $global:IisMaintenanceTestState.Fail = $false
    [IO.File]::WriteAllText((Join-Path $data 'database\mercado-cercano.db'), 'placeholder')
    $global:IisMaintenanceTestState.Calls = @()
    & $maintenance -SitePath $site -Command migrate
    Assert-True ($global:IisMaintenanceTestState.Calls.Count -eq 2) 'An existing database must be backed up before migration.'
    Assert-True ($global:IisMaintenanceTestState.Calls[0].EndsWith('--maintenance backup')) 'Backup must precede migration.'
    [IO.File]::WriteAllText($offline, 'pre-existing maintenance')
    Assert-Fails { & $maintenance -SitePath $site -Command backup }
    Assert-True ((Get-Content -LiteralPath $offline -Raw) -eq 'pre-existing maintenance') 'Existing maintenance must never be removed.'
    Remove-Item -LiteralPath $offline
    Assert-Fails { & $maintenance -SitePath $site -Command restore -BackupName 'backup-20261020T0200000000000Z-1234abcd' }
    Assert-True (-not (Test-Path -LiteralPath $offline)) 'Restore confirmation is required before touching the site.'
    $global:IisMaintenanceTestState.ReplaceOffline = $true
    Assert-Fails { & $maintenance -SitePath $site -Command backup }
    Assert-True ((Get-Content -LiteralPath $offline -Raw) -eq 'changed by another operator') 'Another operator maintenance flag must be retained.'
    Write-Output 'Isolated IIS workflow assertions passed. dotnet and Event Log were mocked; no IIS, HTTP or tasks were started.'
} finally {
    Remove-Item Function:\dotnet
    Remove-Item Function:\Write-EventLog
    Remove-Variable -Name IisMaintenanceTestState -Scope Global
    Remove-Item -LiteralPath $root -Recurse -Force
}
