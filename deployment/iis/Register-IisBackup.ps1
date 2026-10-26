[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$SitePath,
    [string]$TaskName = 'MercadoCercano-Backup',
    [string]$At = '02:00'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$site = (Resolve-Path -LiteralPath $SitePath).Path
$script = Join-Path $PSScriptRoot 'Invoke-IisMaintenance.ps1'
if (-not (Test-Path -LiteralPath $script -PathType Leaf)) { throw 'Falta el script de mantenimiento.' }
$time = [TimeSpan]::ParseExact($At, 'hh\:mm', [Globalization.CultureInfo]::InvariantCulture)
$executable = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$action = New-ScheduledTaskAction -Execute $executable -Argument "-NoProfile -NonInteractive -File `"$script`" -SitePath `"$site`" -Command backup"
$trigger = New-ScheduledTaskTrigger -Daily -At ([DateTime]::Today.Add($time))
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 2) -StartWhenAvailable
if ($PSCmdlet.ShouldProcess($TaskName, "Registrar respaldo diario a las $At, hora local del servidor")) {
    if (Get-ScheduledTask | Where-Object { $_.TaskName -eq $TaskName }) { throw 'La tarea ya existe. Revisala antes de reemplazarla.' }
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
        -Description 'Respaldo verificado de SQLite, imagenes, comprobantes y claves. Breve mantenimiento IIS; retencion desde appsettings.Production.json.' | Out-Null
    Write-Output "Tarea registrada para las $At. No se ejecuto el respaldo."
}
