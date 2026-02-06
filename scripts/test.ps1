. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "test"
$summaryPath = Join-Path $repoRoot ("logs\\test_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
$unityPath = Resolve-UnityPath

if (-not $unityPath) {
    $command = "Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

Ensure-Dir (Join-Path $repoRoot "artifacts")

$args = @(
    "-batchmode",
    "-quit",
    "-projectPath", $repoRoot,
    "-executeMethod", "Game.Editor.BatchTestRunner.RunEditMode",
    "-testPlatform", "EditMode",
    "-testResults", (Join-Path $repoRoot "artifacts\\test-results.xml"),
    "-logFile", (Join-Path $repoRoot "logs\\unity-tests.log")
)
$unityExit = Invoke-LoggedProcess -FilePath $unityPath -Arguments $args -WorkingDirectory $repoRoot -LogPath $logPath -AllowFailure

$resultsPath = Join-Path $repoRoot "artifacts\\test-results.xml"
Ensure-Dir (Split-Path -Parent $summaryPath)

function Write-TestLine([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

if (Test-Path $resultsPath) {
    [xml]$xml = Get-Content -Path $resultsPath
    $testCases = $xml.SelectNodes("//test-case")
    $hasFailure = $false
    foreach ($case in $testCases) {
        $name = $case.name
        $result = $case.result
        if ($result -eq "Failed") {
            $hasFailure = $true
        }
        Write-TestLine "$name $result"
    }
    Write-TestLine "finalizado"
    if ($hasFailure) {
        exit 1
    }
    exit 0
}

$unityLog = Join-Path $repoRoot "logs\\unity-tests.log"
if (Test-Path $unityLog) {
    $lines = Get-Content -Path $unityLog | Where-Object { $_ -like "TEST|*" }
    foreach ($line in $lines) {
        $parts = $line.Split("|")
        if ($parts.Length -ge 3) {
            Write-TestLine "$($parts[2]) $($parts[1])"
        }
    }
    Write-TestLine "finalizado"
    if ($unityExit -and $unityExit -ne 0) {
        exit $unityExit
    }
    exit 1
}

Write-TestLine "finalizado"
if ($unityExit -and $unityExit -ne 0) {
    exit $unityExit
}
exit 1
