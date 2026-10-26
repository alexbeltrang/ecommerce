[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SitePath,
    [Parameter(Mandatory = $true)][ValidateSet('migrate', 'create-admin', 'backup', 'verify-backup', 'restore')][string]$Command,
    [string]$BackupName,
    [switch]$ConfirmRestore,
    [switch]$NewMachine
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$site = (Resolve-Path -LiteralPath $SitePath).Path
$dll = Join-Path $site 'MercadoCercano.Api.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'No se encontro el ejecutable publicado.' }
if ($Command -eq 'restore' -and -not $ConfirmRestore) { throw 'Restaurar exige -ConfirmRestore y un respaldo verificado.' }
if ($NewMachine -and $Command -ne 'restore') { throw '-NewMachine solo se permite al restaurar.' }
$arguments = @($dll, '--maintenance', $Command)
if ($Command -in @('restore', 'verify-backup')) {
    if ($BackupName -notmatch '^backup-[0-9]{8}T[0-9]{13}Z-[a-f0-9]{8}$') { throw 'Indica el nombre exacto de un respaldo completo, no una ruta.' }
    $arguments += $BackupName
}
if ($Command -eq 'restore') {
    $arguments += '--confirm-restore'
    if ($NewMachine) { $arguments += '--new-machine' }
}
$offline = Join-Path $site 'app_offline.htm'
if (Test-Path -LiteralPath $offline) { throw 'El sitio ya tiene app_offline.htm. No se modifica ni retira mantenimiento ajeno.' }
$stream = [IO.File]::Open($offline, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
$marker = [Guid]::NewGuid().ToString('N')
try {
    $offlineText = '<!doctype html><html lang="es"><meta charset="utf-8"><title>Mantenimiento</title><h1>Mantenimiento programado</h1><p>Volveremos en unos minutos. No repitas pagos ni pedidos mientras tanto.</p></html><!-- ' + $marker + ' -->'
    $text = [Text.Encoding]::UTF8.GetBytes($offlineText)
    $stream.Write($text, 0, $text.Length)
} finally { $stream.Dispose() }
$completed = $false
try {
    if ($Command -eq 'migrate') {
        $settings = Get-Content -LiteralPath (Join-Path $site 'appsettings.Production.json') -Raw | ConvertFrom-Json
        $dataRoot = $settings.Storage.Root
        if ($env:Storage__Root) { $dataRoot = $env:Storage__Root }
        if (Test-Path -LiteralPath (Join-Path $dataRoot 'database\mercado-cercano.db') -PathType Leaf) {
            & dotnet $dll '--maintenance' 'backup'
            if ($LASTEXITCODE -ne 0) { throw 'No se pudo verificar el respaldo previo. No se ejecutaran migraciones.' }
        }
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "El mantenimiento $Command fallo con codigo $LASTEXITCODE." }
    Write-EventLog -LogName Application -Source MercadoCercano -EntryType Information -EventId 1000 -Message "Mantenimiento $Command completado."
    $completed = $true
} catch {
    Write-EventLog -LogName Application -Source MercadoCercano -EntryType Error -EventId 1001 -Message "Mantenimiento $Command fallo. Revisa la consola y el historial de la tarea."
    throw
} finally {
    if ($completed -or $Command -in @('backup', 'verify-backup', 'create-admin')) {
        if ((Test-Path -LiteralPath $offline) -and (Get-Content -LiteralPath $offline -Raw) -eq $offlineText) {
            Remove-Item -LiteralPath $offline
        } else {
            throw 'El archivo de mantenimiento cambio durante la operacion; no se retiro automaticamente.'
        }
    } else {
        Write-Warning 'El sitio permanece offline: revisa y recupera el estado antes de retirar app_offline.htm manualmente.'
    }
}
