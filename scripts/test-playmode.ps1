. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "test_playmode"
$summaryPath = Join-Path $repoRoot ("logs\\test_playmode_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
$unityPath = Resolve-UnityPath

if (-not $unityPath) {
    $command = "Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

Ensure-Dir (Join-Path $repoRoot "logs")

$args = @(
    "-batchmode",
    "-projectPath", $repoRoot,
    "-executeMethod", "Game.Editor.NetworkSmokeBatchRunner.Run",
    "-logFile", (Join-Path $repoRoot "logs\\unity-tests-playmode.log")
)
$timeoutSeconds = 120
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $unityPath
$startInfo.WorkingDirectory = $repoRoot
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.Arguments = ($args -join " ")
$startInfo.Environment["SMOKE_LOG_DIR"] = $repoRoot

$start = Get-Date
$commit = Get-GitCommit $repoRoot
Ensure-Dir (Split-Path -Parent $logPath)
"cmd=""$unityPath"" $($args -join " ")" | Out-File -FilePath $logPath -Encoding utf8
"cwd=$repoRoot" | Out-File -FilePath $logPath -Encoding utf8 -Append
"commit=$commit" | Out-File -FilePath $logPath -Encoding utf8 -Append
"start=$($start.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo
$null = $process.Start()

if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
    try { $process.Kill() } catch { }
    "PlayMode smoke timed out after ${timeoutSeconds}s." | Out-File -FilePath $logPath -Encoding utf8 -Append
    $unityExit = 1
} else {
    $unityExit = $process.ExitCode
}

$stdout = $process.StandardOutput.ReadToEnd()
$stderr = $process.StandardError.ReadToEnd()
if ($stdout) { $stdout | Out-File -FilePath $logPath -Encoding utf8 -Append }
if ($stderr) { $stderr | Out-File -FilePath $logPath -Encoding utf8 -Append }

$end = Get-Date
$duration = New-TimeSpan -Start $start -End $end
"end=$($end.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append
"duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $logPath -Encoding utf8 -Append
"exit_code=$unityExit" | Out-File -FilePath $logPath -Encoding utf8 -Append

$resultsPath = Join-Path $repoRoot "logs\\network-smoke-result.log"
Ensure-Dir (Split-Path -Parent $summaryPath)

function Write-TestLine([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

if (Test-Path $resultsPath) {
    $lines = Get-Content -Path $resultsPath
    if ($lines | Where-Object { $_ -like "*network_smoke_ok*" }) {
        Write-TestLine "NetworkFacade_Handshake_Completes Passed"
        Write-TestLine "finalizado"
        exit 0
    }
    if ($lines | Where-Object { $_ -like "*network_smoke_fail*" }) {
        Write-TestLine "NetworkFacade_Handshake_Completes Failed"
        Write-TestLine "finalizado"
        exit 1
    }
}

Write-TestLine "NetworkFacade_Handshake_Completes Failed"
Write-TestLine "finalizado"
if ($unityExit -and $unityExit -ne 0) {
    exit $unityExit
}
exit 1
