param(
    [string]$Version = "sf_19"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$engineDirectory = Join-Path $projectRoot "Engines\Stockfish"
$archiveUrl = "https://github.com/official-stockfish/Stockfish/releases/download/$Version/stockfish-windows-x86-64-universal.zip"
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("online-chess-stockfish-" + [Guid]::NewGuid())
$archivePath = Join-Path $temporaryDirectory "stockfish.zip"
$extractPath = Join-Path $temporaryDirectory "extract"

New-Item -ItemType Directory -Force -Path $temporaryDirectory, $extractPath, $engineDirectory | Out-Null

try {
    Write-Host "Downloading Stockfish from $archiveUrl"
    Invoke-WebRequest -Uri $archiveUrl -OutFile $archivePath
    Expand-Archive -Path $archivePath -DestinationPath $extractPath -Force

    $executable = Get-ChildItem -Path $extractPath -Recurse -File -Filter "stockfish-windows-x86-64.exe" | Select-Object -First 1
    if (-not $executable) {
        $executable = Get-ChildItem -Path $extractPath -Recurse -File -Filter "stockfish*.exe" |
            Where-Object { $_.Name -notmatch "avx2|bmi2|avx512|vnni" } |
            Select-Object -First 1
    }
    if (-not $executable) {
        throw "No compatible Stockfish executable was found in the downloaded archive."
    }

    Copy-Item $executable.FullName (Join-Path $engineDirectory "stockfish.exe") -Force

    foreach ($licenseName in @("Copying.txt", "AUTHORS", "README.md")) {
        $licenseFile = Get-ChildItem -Path $extractPath -Recurse -File -Filter $licenseName | Select-Object -First 1
        if ($licenseFile) {
            Copy-Item $licenseFile.FullName (Join-Path $engineDirectory $licenseName) -Force
        }
    }

    Write-Host "Stockfish installed at: $engineDirectory\stockfish.exe" -ForegroundColor Green
    Write-Host "Rebuild the WinForms project so the engine is copied to the output directory."
}
finally {
    Remove-Item -Path $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
