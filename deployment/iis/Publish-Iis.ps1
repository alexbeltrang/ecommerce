[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [Parameter(Mandatory = $true)][string]$ProductionConfigPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configPath = (Resolve-Path -LiteralPath $ProductionConfigPath).Path
$target = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $target) { throw 'Destination debe ser una carpeta nueva; no se sobrescriben sitios existentes.' }
$settings = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
foreach ($directory in @($settings.Storage.Root, $settings.Operations.BackupRoot)) {
    $private = [IO.Path]::GetFullPath($directory).TrimEnd('\')
    if (-not [IO.Path]::IsPathRooted($directory) -or $target.StartsWith($private + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $private.StartsWith($target.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $private -eq $target) {
        throw 'Publicacion, datos y respaldos deben estar separados y usar rutas absolutas.'
    }
}
Push-Location (Join-Path $repository 'src\MercadoCercano.Web')
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion de Angular.' }
} finally { Pop-Location }
Push-Location $repository
try {
    & dotnet publish 'src\MercadoCercano.Api\MercadoCercano.Api.csproj' --configuration Release --no-restore --output $target --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la publicacion de ASP.NET Core.' }
} finally { Pop-Location }
Copy-Item -LiteralPath $configPath -Destination (Join-Path $target 'appsettings.Production.json')
Write-Output "Paquete listo en $target. No se configuro IIS, no se aplicaron migraciones y no se inicio ningun servidor."
