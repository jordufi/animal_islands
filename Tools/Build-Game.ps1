# Builds Animal Island for Windows and zips it (to send to a friend / play on another PC).
#
# Usage (PowerShell, with Unity CLOSED - a project can't be open twice):
#   powershell -ExecutionPolicy Bypass -File Tools\Build-Game.ps1
#
# With Unity open, use the menu instead: Tools > Island > Build Windows (.zip)  (same result).
#
# Result:  Builds\AnimalIsland_<date>_<time>.zip   (+ the unzipped folder next to it)
#          Builds\build.log                       (Unity's log, if something goes wrong)
param(
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe",
    [string]$Project = ""
)

$ErrorActionPreference = 'Stop'
# default: the project folder that contains this script's Tools folder
if (-not $Project) { $Project = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }
$Project = (Resolve-Path $Project).Path
if (-not (Test-Path (Join-Path $Project 'Assets'))) { throw "Not a Unity project: $Project" }
if (-not (Test-Path $Unity)) { throw "Unity not found: $Unity  (pass -Unity 'C:\...\Unity.exe')" }

# the project must not be open in the editor
$lock = Join-Path $Project 'Temp\UnityLockfile'
if (Test-Path $lock) {
    try { $s = [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $s.Close() }
    catch {
        Write-Host "Unity tiene el proyecto abierto. Ciérralo y vuelve a lanzar el script," -ForegroundColor Yellow
        Write-Host "o exporta desde Unity con el menú Tools > Island > Build Windows (.zip)." -ForegroundColor Yellow
        exit 1
    }
}

$builds = Join-Path $Project 'Builds'
New-Item -ItemType Directory -Force -Path $builds | Out-Null
$log = Join-Path $builds 'build.log'
$started = Get-Date
Write-Host "Compilando Animal Island (tarda unos minutos)..."
$p = Start-Process -FilePath $Unity -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$Project`"",
        '-executeMethod', 'BuildGame.CommandLine', '-logFile', "`"$log`"") -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0) {
    Write-Host "La build ha fallado (código $($p.ExitCode)). Últimas líneas de $log :" -ForegroundColor Red
    if (Test-Path $log) { Get-Content $log -Tail 30 }
    exit 1
}

$zip = Get-ChildItem $builds -Filter 'AnimalIsland_*.zip' | Where-Object { $_.LastWriteTime -ge $started } | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $zip) { Write-Host "No se encontró el .zip (mira $log)" -ForegroundColor Red; exit 1 }
$mb = [math]::Round($zip.Length / 1MB)
Write-Host "Listo: $($zip.FullName)  ($mb MB)" -ForegroundColor Green
Write-Host "Pásale ese .zip a tu colega: que lo descomprima y abra AnimalIsland.exe (instrucciones en LEEME.txt)."
