$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $rootDir "build-video"
$audioDir = Join-Path $buildDir "audio"

New-Item -ItemType Directory -Path $audioDir -Force | Out-Null

Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $synth.SelectVoice("Microsoft David Desktop")
    Write-Host "Selected Voice: Microsoft David Desktop (Male, en-US)"
} catch {
    Write-Warning "Could not select Microsoft David Desktop, using default system voice."
}
$synth.Rate = 0
$synth.Volume = 100

$scenes = @(
    @{ Id = "scene_01_intro"; Narration = "Welcome to FCMS Pro, a fast, private, and local-first management system designed specifically for freelancers, independent contractors, and solo creative professionals." },
    @{ Id = "scene_02_dashboard"; Narration = "The Dashboard gives you an immediate pulse on your business. Track your net profit, total revenue, active projects, and overdue alerts alongside an interactive six-month income trend." },
    @{ Id = "scene_03_commissions"; Narration = "Take full control of your project pipeline. Track commissions through every stage from Pending and In Progress to Delivered, with down payments, balances, priority tags, and deadline urgency alerts." },
    @{ Id = "scene_04_clients"; Narration = "Manage client relationships effortlessly. Store contact details, total billed history, and outstanding balances, and attach signed contracts, design briefs, and project files directly to their profile." },
    @{ Id = "scene_05_finances"; Narration = "Record payments across multiple methods to automatically update balances. Log business expenses across categories and track recurring subscriptions to understand your true net profit." },
    @{ Id = "scene_06_analytics"; Narration = "Gain clarity on your financial growth with comprehensive analytics. Review monthly revenue trajectories, identify your top-earning clients, and examine expense distributions for smarter planning." },
    @{ Id = "scene_07_search"; Narration = "Need to find something fast? Hit the forward-slash key anywhere in the app to open Global Search. Search across clients, commissions, and expenses simultaneously at lightning speed without navigating away." },
    @{ Id = "scene_08_reports"; Narration = "When tax season arrives, FCMS Pro has you covered. Export publication-quality quarterly and annual financial summary PDFs, detailing your gross revenue, deductible expenses, and net profit ready for filing." },
    @{ Id = "scene_09_themes"; Narration = "Work comfortably in any lighting environment. Seamlessly toggle between sleek Dark and crisp Light themes, select custom accent colors, and configure your preferred international currency symbol." },
    @{ Id = "scene_10_privacy"; Narration = "Best of all, FCMS Pro is completely local-first. Your financial data stays exclusively on your machine in an offline SQLite database with automatic rotating backups and full JSON export and restore." },
    @{ Id = "scene_11_outro"; Narration = "Take control of your freelance career today. Download the free, open-source desktop app for Windows, macOS, and Linux at villenaderic dot github dot io slash FcmsPro. Thank you for watching!" }
)

Write-Host "=== Generating SAPI Male Audio Narration ==="
foreach ($s in $scenes) {
    $wavPath = Join-Path $audioDir "$($s.Id).wav"
    Write-Host "Synthesizing $($s.Id)..."
    $synth.SetOutputToWaveFile($wavPath)
    $escaped = [System.Security.SecurityElement]::Escape($s.Narration)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><break time='450ms'/>$escaped<break time='650ms'/></speak>"
    $synth.SpeakSsml($ssml)
}
$synth.Dispose()
Write-Host "Audio generation finished successfully!"
