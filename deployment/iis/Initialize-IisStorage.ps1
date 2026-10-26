[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$SitePath,
    [Parameter(Mandatory = $true)][string]$AppPoolName
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$site = (Resolve-Path -LiteralPath $SitePath).Path
$settings = Get-Content -LiteralPath (Join-Path $site 'appsettings.Production.json') -Raw | ConvertFrom-Json
$root = [IO.Path]::GetFullPath($settings.Storage.Root).TrimEnd('\')
$backups = [IO.Path]::GetFullPath($settings.Operations.BackupRoot).TrimEnd('\')
foreach ($path in @($root, $backups)) {
    if ($path -eq [IO.Path]::GetPathRoot($path).TrimEnd('\') -or (Test-Path -LiteralPath $path)) {
        throw 'Usa directorios privados nuevos, nunca una raiz de volumen o una carpeta existente.'
    }
}
$paths = @($site.TrimEnd('\'), $root, $backups)
for ($i = 0; $i -lt $paths.Count; $i++) {
    for ($j = $i + 1; $j -lt $paths.Count; $j++) {
        if ($paths[$i] -eq $paths[$j] -or $paths[$i].StartsWith($paths[$j] + '\', [StringComparison]::OrdinalIgnoreCase) -or
            $paths[$j].StartsWith($paths[$i] + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Las tres rutas deben estar separadas, sin anidarse.' }
    }
}
$identity = New-Object Security.Principal.NTAccount("IIS AppPool\$AppPoolName")
$sid = $identity.Translate([Security.Principal.SecurityIdentifier]).Value
if ($PSCmdlet.ShouldProcess("$root; $backups; $site", 'Crear almacenamiento privado y asignar permisos IIS/SYSTEM/Administradores')) {
    foreach ($path in @($root, $backups)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
        & icacls $path '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F'
        if ($LASTEXITCODE -ne 0) { throw "No se pudieron proteger los permisos de $path." }
    }
    & icacls $root '/grant:r' "*${sid}:(OI)(CI)M"
    if ($LASTEXITCODE -ne 0) { throw 'Fallo el permiso del App Pool sobre los datos.' }
    & icacls $site '/grant:r' "*${sid}:(OI)(CI)RX"
    if ($LASTEXITCODE -ne 0) { throw 'Fallo el permiso de lectura del sitio.' }
    foreach ($name in @('database', 'products', 'payment-evidence', 'keys')) {
        New-Item -ItemType Directory -Path (Join-Path $root $name) | Out-Null
    }
    if (-not [Diagnostics.EventLog]::SourceExists('MercadoCercano')) {
        New-EventLog -LogName Application -Source MercadoCercano
    }
}
