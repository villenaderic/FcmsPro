$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $rootDir "build-video"
$audioDir = Join-Path $buildDir "audio"
$framesDir = Join-Path $buildDir "frames"
$clipsDir = Join-Path $buildDir "clips"
$ffmpeg = Join-Path $rootDir ".video-tools\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
$outputVideo = Join-Path $rootDir "FCMS_Pro_Project_Demo.mp4"

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

Write-Host "=== Encoding 14 High-Quality Video Clips with Cinematic Fade ==="
$clipFiles = @()
foreach ($sceneId in $scenes) {
    $framePath = Join-Path $framesDir "$sceneId.png"
    $audioPath = Join-Path $audioDir "$sceneId.mp3"
    $clipPath = Join-Path $clipsDir "$sceneId.mp4"
    $clipFiles += $clipPath

    Write-Host "Encoding $sceneId.mp4..."
    & $ffmpeg -y -loop 1 -i $framePath -i $audioPath -vf "fade=t=in:st=0:d=0.35" -c:v libx264 -tune stillimage -c:a aac -b:a 192k -pix_fmt yuv420p -shortest $clipPath
}

Write-Host "`n=== Concatenating into Final YouTube Video ==="
$concatTxt = Join-Path $buildDir "concat.txt"
$lines = $clipFiles | ForEach-Object { "file '$($_.Replace('\', '/'))'" }
[System.IO.File]::WriteAllLines($concatTxt, $lines)

& $ffmpeg -y -f concat -safe 0 -i $concatTxt -c copy $outputVideo

Write-Host "`n=== Video Assembly Complete! ==="
$fi = Get-Item $outputVideo
Write-Host "Output Video: $outputVideo"
Write-Host "Size: $($fi.Length) bytes ($([Math]::Round($fi.Length / 1MB, 2)) MB)"
