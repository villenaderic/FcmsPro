$ErrorActionPreference = "Continue"

$rootDir = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $rootDir "build-video"
$audioDir = Join-Path $buildDir "audio"
$framesDir = Join-Path $buildDir "frames"
$clipsDir = Join-Path $buildDir "clips"
$ffmpeg = Join-Path $rootDir ".video-tools\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
$outputVideo = Join-Path $rootDir "FCMS_Pro_Project_Demo.mp4"
$tempMerged = Join-Path $buildDir "merged_raw.mp4"
$whoosh = Join-Path $buildDir "sfx_whoosh.wav"
$bgMusic = Join-Path $buildDir "bg_music.wav"

New-Item -ItemType Directory -Path $clipsDir -Force | Out-Null

$scenes = @(
    "scene_01_hook_web",
    "scene_02_web_features",
    "scene_03_download_page",
    "scene_04_install_walkthrough",
    "scene_05_dashboard",
    "scene_06_commissions",
    "scene_07_clients",
    "scene_08_finances",
    "scene_09_analytics",
    "scene_10_search",
    "scene_11_reports",
    "scene_12_themes",
    "scene_13_privacy",
    "scene_14_outro"
)

Write-Host "=== 1. Encoding 14 Video Clips with Voiceover & Transition SFX ==="
$clipFiles = @()
for ($i = 0; $i -lt $scenes.Count; $i++) {
    $sceneId = $scenes[$i]
    $framePath = Join-Path $framesDir "$sceneId.png"
    $audioPath = Join-Path $audioDir "$sceneId.mp3"
    $clipPath = Join-Path $clipsDir "$sceneId.mp4"
    $clipFiles += $clipPath

    # Probe duration
    $durStr = (& $ffmpeg -i $audioPath 2>&1 | Select-String "Duration: (\d{2}:\d{2}:\d{2}\.\d{2})").Matches.Groups[1].Value
    $durSec = if ($durStr) { ([TimeSpan]::Parse($durStr)).TotalSeconds } else { 10.0 }

    Write-Host "Encoding [$($i+1)/$($scenes.Count)] $sceneId.mp4 ($([Math]::Round($durSec, 1))s)..."

    if ($i -gt 0 -and (Test-Path $whoosh)) {
        & $ffmpeg -y -loop 1 -i $framePath -i $audioPath -i $whoosh -vf "fade=t=in:st=0:d=0.3" -filter_complex "[1:a]volume=1.0[voice];[2:a]volume=0.30[sfx];[voice][sfx]amix=inputs=2:duration=first:dropout_transition=0[aout]" -map 0:v -map "[aout]" -c:v libx264 -preset ultrafast -tune stillimage -c:a aac -b:a 192k -pix_fmt yuv420p -shortest $clipPath 2>$null
    } else {
        & $ffmpeg -y -loop 1 -i $framePath -i $audioPath -vf "fade=t=in:st=0:d=0.3" -c:v libx264 -preset ultrafast -tune stillimage -c:a aac -b:a 192k -pix_fmt yuv420p -shortest $clipPath 2>$null
    }
}

Write-Host "`n=== 2. Concatenating Clips into Base Video ==="
$concatTxt = Join-Path $buildDir "concat.txt"
$lines = $clipFiles | ForEach-Object { "file '$($_.Replace('\', '/'))'" }
[System.IO.File]::WriteAllLines($concatTxt, $lines)

& $ffmpeg -y -f concat -safe 0 -i $concatTxt -c copy $tempMerged 2>$null

Write-Host "`n=== 3. Mixing Background Tech Soundtrack ==="
node (Join-Path $rootDir "tools\mix-audio.js")
