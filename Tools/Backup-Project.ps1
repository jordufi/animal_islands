# Backs up everything needed to rebuild Animal Island: the Unity project (without the
# regenerable Library/Temp/Logs folders) and the Blender source files.
#
# Usage (PowerShell):
#   powershell -ExecutionPolicy Bypass -File Tools\Backup-Project.ps1 -Destination F:\
#
# Creates  <Destination>\AnimalIsland_Backups\AnimalIsland_<date>_<time>\  with:
#   Unity_animal_island1.zip   Assets, Packages, ProjectSettings, UserSettings, Tools
#   Blender_BeachAnimals.zip   C:\Users\<you>\Documents\BeachAnimals (all .blend, textures, renders)
#   README.txt                 what is inside and how to restore it
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [string]$UnityProject = "",
    [string]$BlenderFolder = "$env:USERPROFILE\Documents\BeachAnimals"
)

$ErrorActionPreference = 'Stop'
# default: the project folder that contains this script's Tools folder
if (-not $UnityProject) { $UnityProject = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }
$UnityProject = (Resolve-Path $UnityProject).Path
if (-not (Test-Path (Join-Path $UnityProject 'Assets'))) { throw "Not a Unity project: $UnityProject" }
if (-not (Test-Path $Destination)) { throw "Destination not found: $Destination" }

$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$target = Join-Path $Destination "AnimalIsland_Backups\AnimalIsland_$stamp"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Write-Host "Backing up to $target"

# --- Unity project: copy the needed folders to a staging folder, then zip
$staging = Join-Path $env:TEMP "AnimalIsland_backup_$stamp"
New-Item -ItemType Directory -Force -Path $staging | Out-Null
foreach ($folder in 'Assets', 'Packages', 'ProjectSettings', 'UserSettings', 'Tools') {
    $src = Join-Path $UnityProject $folder
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $staging $folder) -Recurse -Force
        Write-Host "  + $folder"
    }
}
Get-ChildItem $UnityProject -File | Where-Object { $_.Extension -in '.vsconfig', '.md' } | Copy-Item -Destination $staging
$unityZip = Join-Path $target 'Unity_animal_island1.zip'
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $unityZip -CompressionLevel Optimal
Remove-Item $staging -Recurse -Force

# --- Blender sources
$blenderZip = Join-Path $target 'Blender_BeachAnimals.zip'
if (Test-Path $BlenderFolder) {
    Compress-Archive -Path (Join-Path $BlenderFolder '*') -DestinationPath $blenderZip -CompressionLevel Optimal
    Write-Host "  + BeachAnimals (Blender)"
} else {
    Write-Warning "Blender folder not found: $BlenderFolder"
}

# --- verify: every zip opens and contains files
Add-Type -AssemblyName System.IO.Compression.FileSystem
$report = foreach ($zip in Get-ChildItem $target -Filter '*.zip') {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip.FullName)
    $count = $archive.Entries.Count
    $archive.Dispose()
    '{0}  {1:N1} MB  {2} files' -f $zip.Name, ($zip.Length / 1MB), $count
}

@"
Animal Island backup - $stamp

Unity_animal_island1.zip
  Unity project without Library/Temp/Logs (Unity regenerates them on first open, it takes a few minutes).
  Restore: unzip into an empty folder and open that folder with Unity Hub (Unity 6.6).
  The game starts from Assets/Scenes/MainMenu.unity.

Blender_BeachAnimals.zip
  Contents of ${BlenderFolder} - Island.blend, Island_TestMap.blend, every animal/old man .blend,
  the weapon .blend files (Weapons), textures and renders.
  Restore: unzip into Documents\BeachAnimals.

Contents:
$($report -join "`r`n")
"@ | Set-Content -Path (Join-Path $target 'README.txt') -Encoding UTF8

Write-Host ""
$report | ForEach-Object { Write-Host "  $_" }
Write-Host "Backup finished: $target"
