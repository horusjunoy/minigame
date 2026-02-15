param(
    [int]$TimeoutSeconds = 900
)

. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "build_android"
$summaryPath = Join-Path $repoRoot ("logs\\build_android_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
$apkPath = Join-Path $repoRoot "artifacts\\builds\\android\\MinigameClient.apk"
$unityLockPath = Join-Path $repoRoot "Temp\\UnityLockfile"

function Write-Line([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

function Resolve-UnityPathWithAndroid {
    if ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) {
        $editorDir = Split-Path -Parent $env:UNITY_PATH
        $androidPath = Join-Path $editorDir "Data\\PlaybackEngines\\AndroidPlayer"
        if (Test-Path $androidPath) { return $env:UNITY_PATH }
    }

    $hubPath = Join-Path $env:ProgramFiles "Unity\\Hub\\Editor"
    if (-not (Test-Path $hubPath)) { return $null }

    $versions = Get-ChildItem -Path $hubPath -Directory | Sort-Object Name -Descending
    $ordered = @()
    $ordered += $versions | Where-Object { $_.Name -like "*-x86_64" }
    $ordered += $versions | Where-Object { $_.Name -notlike "*-x86_64" }

    foreach ($versionDir in $ordered) {
        $candidate = Join-Path $versionDir.FullName "Editor\\Unity.exe"
        $androidPath = Join-Path $versionDir.FullName "Editor\\Data\\PlaybackEngines\\AndroidPlayer"
        if ((Test-Path $candidate) -and (Test-Path $androidPath)) {
            return $candidate
        }
    }

    return $null
}

function Stop-UnityProjectProcesses([string]$ProjectPath) {
    if ($env:GITHUB_ACTIONS -eq "true" -or $env:MINIGAME_CI_KILL_ALL_UNITY -eq "1") {
        $targets = @(
            "Unity",
            "UnityCrashHandler64",
            "Unity.ILPP.Runner",
            "UnityPackageManager",
            "UnityShaderCompiler",
            "UnityAutoQuitter"
        )
        foreach ($name in $targets) {
            Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
                try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch {}
            }
        }
        return
    }

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
    [string]$UnityPath,
    [int]$Attempt,
    [bool]$SkipMirrorIlpp,
    [bool]$DisableBurst
) {
    if ($SkipMirrorIlpp) { $env:MINIGAME_SKIP_MIRROR_ILPP = "1" } else { Remove-Item Env:MINIGAME_SKIP_MIRROR_ILPP -ErrorAction SilentlyContinue }
    if ($DisableBurst) { $env:UNITY_BURST_DISABLE = "1" } else { Remove-Item Env:UNITY_BURST_DISABLE -ErrorAction SilentlyContinue }

    $attemptLog = $logPath -replace "\.log$", "_attempt$Attempt.log"
    $unityLogPath = Join-Path $repoRoot ("logs\\unity-build-android_attempt$Attempt.log")
    Remove-Item $unityLogPath -ErrorAction SilentlyContinue

    $args = @(
        "-batchmode",
        "-quit",
        "-projectPath", $repoRoot,
        "-executeMethod", "Game.Editor.BuildScripts.BuildAndroid",
        "-logFile", $unityLogPath
    )

    $start = Get-Date
    $commit = Get-GitCommit $repoRoot
    $cmdLine = "`"$UnityPath`" " + ($args -join " ")
    "attempt=$Attempt skip_mirror_ilpp=$SkipMirrorIlpp disable_burst=$DisableBurst" | Out-File -FilePath $attemptLog -Encoding utf8
    "cmd=$cmdLine" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "cwd=$repoRoot" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "commit=$commit" | Out-File -FilePath $attemptLog -Encoding utf8 -Append
    "start=$($start.ToString('o'))" | Out-File -FilePath $attemptLog -Encoding utf8 -Append

    $stdoutLog = $attemptLog -replace "\.log$", "_stdout.log"
    $stderrLog = $attemptLog -replace "\.log$", "_stderr.log"
    $process = Start-Process -FilePath $UnityPath `
        -ArgumentList $args `
        -WorkingDirectory $repoRoot `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog

    $exitCode = 0
    $timedOut = $false
    $pollMs = 15000
    $heartbeatEverySeconds = 60
    $nextHeartbeat = (Get-Date).AddSeconds($heartbeatEverySeconds)
    $deadline = $start.AddSeconds($TimeoutSeconds)

    while ($true) {
        if ($process.WaitForExit($pollMs)) {
            $exitCode = $process.ExitCode
            break
        }

        $now = Get-Date
        if ($now -ge $deadline) {
            $timedOut = $true
            try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch {}
            Stop-UnityProjectProcesses -ProjectPath $repoRoot
            $exitCode = 124
            break
        }

        if ($now -ge $nextHeartbeat) {
            $elapsed = [int](New-TimeSpan -Start $start -End $now).TotalSeconds
            $heartbeat = "heartbeat attempt=$Attempt elapsed_s=$elapsed pid=$($process.Id)"
            $heartbeat | Out-Host
            $heartbeat | Out-File -FilePath $attemptLog -Encoding utf8 -Append
            $nextHeartbeat = $now.AddSeconds($heartbeatEverySeconds)
        }
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
        MethodStarted = ($unityLog -like "*BuildAndroid: start*")
        BuildSucceeded = ($unityLog -like "*BuildAndroid: result=Succeeded*")
        IlppFault = ($unityLog -like "*Connectivity with IL Post Processor runner cannot be established yet*" -or
                     $unityLog -like "*Error when executing service method 'PostProcessAssembly'*")
    }
}

$unityPath = Resolve-UnityPathWithAndroid
if (-not $unityPath) {
    $command = "Write-Error 'Unity com Android Build Support nao encontrado. Instale o modulo Android ou defina UNITY_PATH para um editor com AndroidPlayer.'; exit 1"
    Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
}

Ensure-Dir (Join-Path $repoRoot "artifacts")
Ensure-Dir (Join-Path $repoRoot "logs")
Ensure-Dir (Split-Path -Parent $summaryPath)
Remove-Item $apkPath -ErrorAction SilentlyContinue
Prepare-Retry

$attempts = @()
$attempts += Invoke-UnityAttempt -UnityPath $unityPath -Attempt 1 -SkipMirrorIlpp:$false -DisableBurst:$false

$needsRetry = $false
$last = $attempts[-1]
if ($last.TimedOut -or $last.ExitCode -eq -1 -or $last.IlppFault -or -not $last.MethodStarted -or -not (Test-Path $apkPath)) {
    $needsRetry = $true
}

if ($needsRetry) {
    Prepare-Retry
    $attempts += Invoke-UnityAttempt -UnityPath $unityPath -Attempt 2 -SkipMirrorIlpp:$true -DisableBurst:$true
}

$final = $attempts[-1]

$needsThirdAttempt = $false
if ($final.TimedOut -or $final.ExitCode -eq -1 -or $final.IlppFault -or -not $final.MethodStarted -or -not (Test-Path $apkPath)) {
    $needsThirdAttempt = $true
}

if ($needsThirdAttempt) {
    Prepare-Retry
    $attempts += Invoke-UnityAttempt -UnityPath $unityPath -Attempt 3 -SkipMirrorIlpp:$true -DisableBurst:$true
    $final = $attempts[-1]
}

if (Test-Path $final.UnityLogPath) {
    Copy-Item $final.UnityLogPath (Join-Path $repoRoot "logs\\unity-build-android.log") -Force
}

Remove-Item Env:MINIGAME_SKIP_MIRROR_ILPP -ErrorAction SilentlyContinue
Remove-Item Env:UNITY_BURST_DISABLE -ErrorAction SilentlyContinue

if ($final.ExitCode -ne 0) {
    Write-Line "build_android Failed"
    if ($final.TimedOut) {
        Write-Line "reason=timeout_after_${TimeoutSeconds}s"
    } elseif ($final.IlppFault) {
        Write-Line "reason=ilpp_postprocess_fault"
    } elseif (-not $final.MethodStarted) {
        Write-Line "reason=unity_execute_method_not_run"
    } else {
        Write-Line "reason=unity_exit_code_$($final.ExitCode)"
    }
    Write-Line "finalizado"
    exit $final.ExitCode
}

if (-not $final.MethodStarted) {
    Write-Line "build_android Failed"
    Write-Line "reason=unity_execute_method_not_run"
    Write-Line "finalizado"
    exit 2
}

if (-not $final.BuildSucceeded) {
    Write-Line "build_android Failed"
    Write-Line "reason=build_report_not_succeeded"
    Write-Line "finalizado"
    exit 2
}

if (-not (Test-Path $apkPath)) {
    Write-Line "build_android Failed"
    if ($final.IlppFault) {
        Write-Line "reason=ilpp_postprocess_fault"
    } else {
        Write-Line "reason=artifact_missing:$apkPath"
    }
    Write-Line "finalizado"
    exit 2
}

Write-Line "build_android Passed"
Write-Line "finalizado"
