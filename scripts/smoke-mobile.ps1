param(
    [int]$TimeoutSeconds = 600
)

. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "smoke_mobile"
$summaryPath = Join-Path $repoRoot ("logs\\smoke_mobile_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
$unityLockPath = Join-Path $repoRoot "Temp\\UnityLockfile"
$unityPath = Resolve-UnityPath

if (-not $unityPath) {
    $command = "Write-Error 'Unity Editor nao encontrado. Defina UNITY_PATH.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

function Write-Line([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

function Stop-UnityProjectProcesses([string]$ProjectPath) {
    $escaped = [Regex]::Escape($ProjectPath)
    $running = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq "Unity.exe" -and
            $_.CommandLine -and
            $_.CommandLine -match $escaped
        }

    foreach ($proc in $running) {
        try { Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Prepare-Retry {
    Stop-UnityProjectProcesses -ProjectPath $repoRoot
    Remove-Item $unityLockPath -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

function Invoke-UnityAttempt(
    [int]$Attempt,
    [bool]$SkipMirrorIlpp,
    [bool]$DisableBurst,
    [string]$ExecuteMethod
) {
    if ($SkipMirrorIlpp) { $env:MINIGAME_SKIP_MIRROR_ILPP = "1" } else { Remove-Item Env:MINIGAME_SKIP_MIRROR_ILPP -ErrorAction SilentlyContinue }
    if ($DisableBurst) { $env:UNITY_BURST_DISABLE = "1" } else { Remove-Item Env:UNITY_BURST_DISABLE -ErrorAction SilentlyContinue }

    $attemptLog = $logPath -replace "\.log$", "_attempt$Attempt.log"
    $unityLogPath = Join-Path $repoRoot ("logs\\unity-smoke-mobile_attempt$Attempt.log")
    Remove-Item $unityLogPath -ErrorAction SilentlyContinue

    $args = @(
        "-batchmode",
        "-quit",
        "-projectPath", $repoRoot,
        "-executeMethod", $ExecuteMethod,
        "-logFile", $unityLogPath
    )

    $start = Get-Date
    $commit = Get-GitCommit $repoRoot
    $cmdLine = "`"$unityPath`" " + ($args -join " ")
    "attempt=$Attempt skip_mirror_ilpp=$SkipMirrorIlpp disable_burst=$DisableBurst" | Out-File -FilePath $attemptLog -Encoding utf8
    "cmd=$cmdLine" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "cwd=$repoRoot" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "commit=$commit" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "start=$($start.ToString('o'))" | Out-File -FilePath $attemptLog -Encoding utf8 -Append

    $stdoutLog = $attemptLog -replace "\.log$", "_stdout.log"
    $stderrLog = $attemptLog -replace "\.log$", "_stderr.log"
    $process = Start-Process -FilePath $unityPath `
        -ArgumentList $args `
        -WorkingDirectory $repoRoot `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog

    $exitCode = 0
    $timedOut = $false
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $timedOut = $true
        try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch {}
        Stop-UnityProjectProcesses -ProjectPath $repoRoot
        $exitCode = 124
    } else {
        $exitCode = $process.ExitCode
    }

    if (Test-Path $stdoutLog) { Get-Content $stdoutLog | Out-File -FilePath $attemptLog -Encoding utf8 -Append }
    if (Test-Path $stderrLog) { Get-Content $stderrLog | Out-File -FilePath $attemptLog -Encoding utf8 -Append }

    $unityLog = ""
    if (Test-Path $unityLogPath) {
        $unityLog = Get-Content $unityLogPath -Raw
    }

    $end = Get-Date
    $duration = New-TimeSpan -Start $start -End $end
    "end=$($end.ToString('o'))" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "exit_code=$exitCode" | Out-File -FilePath $attemptLog -Encoding utf8 -Append

    return [pscustomobject]@{
        ExitCode = $exitCode
        TimedOut = $timedOut
        AttemptLog = $attemptLog
        UnityLogPath = $unityLogPath
        SmokeOk = ($unityLog -like "*mobile_smoke_ok*")
        RuntimeCompleted = ($unityLog -like "*Smoke: runtime completed.*")
        IlppFault = ($unityLog -like "*Connectivity with IL Post Processor runner cannot be established yet*" -or
                     $unityLog -like "*Error when executing service method 'PostProcessAssembly'*")
        MissingExecuteMethod = ($unityLog -like "*executeMethod method '* could not be found.*")
    }
}

Ensure-Dir (Split-Path -Parent $summaryPath)
Ensure-Dir (Join-Path $repoRoot "logs")

$primaryMethod = "Game.Editor.RuntimeSmokeRunner.RunMobile"
$fallbackMethod = "Game.Editor.RuntimeSmokeRunner.Run"
$selectedMethod = $primaryMethod

$attempts = @()
$attempts += Invoke-UnityAttempt -Attempt 1 -SkipMirrorIlpp:$false -DisableBurst:$false -ExecuteMethod $selectedMethod

$last = $attempts[-1]
$needsRetry = $false
if ($last.TimedOut -or $last.ExitCode -eq -1 -or $last.IlppFault -or -not $last.SmokeOk) {
    $needsRetry = $true
}

if ($needsRetry) {
    Prepare-Retry
    $attempts += Invoke-UnityAttempt -Attempt 2 -SkipMirrorIlpp:$true -DisableBurst:$true -ExecuteMethod $selectedMethod
}

$final = $attempts[-1]

if ($final.ExitCode -ne 0 -and $final.MissingExecuteMethod -and $selectedMethod -eq $primaryMethod) {
    $selectedMethod = $fallbackMethod
    Prepare-Retry
    $attempts += Invoke-UnityAttempt -Attempt 3 -SkipMirrorIlpp:$true -DisableBurst:$true -ExecuteMethod $selectedMethod
    $final = $attempts[-1]
}

if (Test-Path $final.UnityLogPath) {
    Copy-Item $final.UnityLogPath (Join-Path $repoRoot "logs\\unity-smoke-mobile.log") -Force
}

Remove-Item Env:MINIGAME_SKIP_MIRROR_ILPP -ErrorAction SilentlyContinue
Remove-Item Env:UNITY_BURST_DISABLE -ErrorAction SilentlyContinue

if ($final.ExitCode -ne 0) {
    Write-Line "smoke_mobile Failed"
    if ($final.TimedOut) {
        Write-Line "reason=timeout_after_${TimeoutSeconds}s"
    } elseif ($final.IlppFault) {
        Write-Line "reason=ilpp_postprocess_fault"
    } else {
        Write-Line "reason=unity_exit_code_$($final.ExitCode)"
    }
    Write-Line "finalizado"
    exit $final.ExitCode
}

if (-not $final.SmokeOk) {
    if ($selectedMethod -eq $fallbackMethod -and $final.RuntimeCompleted) {
        Write-Line "smoke_mobile Passed"
        Write-Line "finalizado"
        exit 0
    }

    Write-Line "smoke_mobile Failed"
    if ($final.IlppFault) {
        Write-Line "reason=ilpp_postprocess_fault"
    } else {
        Write-Line "reason=missing_mobile_smoke_ok"
    }
    Write-Line "finalizado"
    exit 2
}

Write-Line "smoke_mobile Passed"
Write-Line "finalizado"
