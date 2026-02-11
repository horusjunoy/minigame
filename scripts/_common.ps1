Set-StrictMode -Version Latest

function Get-RepoRoot {
    if ($PSScriptRoot) {
        return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    }

    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    if (-not $scriptDir) {
        return (Resolve-Path ".").Path
    }

    return (Resolve-Path (Join-Path $scriptDir "..")).Path
}

function Ensure-Dir([string]$Path) {
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Get-GitCommit([string]$RepoRoot) {
    try {
        return (git -C $RepoRoot rev-parse HEAD 2>$null).Trim()
    } catch {
        return "unknown"
    }
}

function New-LogPath([string]$RepoRoot, [string]$Prefix) {
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    return Join-Path $RepoRoot "logs\$($Prefix)_$stamp.log"
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory=$true)][string]$Command,
        [Parameter(Mandatory=$true)][string]$WorkingDirectory,
        [Parameter(Mandatory=$true)][string]$LogPath
    )

    $start = Get-Date
    $commit = Get-GitCommit $WorkingDirectory

    Ensure-Dir (Split-Path -Parent $LogPath)

    "cmd=$Command" | Out-File -FilePath $LogPath -Encoding utf8
    "cwd=$WorkingDirectory" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "commit=$commit" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "start=$($start.ToString('o'))" | Out-File -FilePath $LogPath -Encoding utf8 -Append

    $exitCode = 0
    try {
        & powershell -NoProfile -Command $Command 2>&1 | Tee-Object -FilePath $LogPath -Append | Out-Host
        $exitCode = $LASTEXITCODE
    } catch {
        $exitCode = 1
        $_ | Out-File -FilePath $LogPath -Encoding utf8 -Append
    }

    $end = Get-Date
    $duration = New-TimeSpan -Start $start -End $end
    "end=$($end.ToString('o'))" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "exit_code=$exitCode" | Out-File -FilePath $LogPath -Encoding utf8 -Append

    if ($exitCode -ne 0) {
        exit $exitCode
    }
}

function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [Parameter(Mandatory=$true)][string]$WorkingDirectory,
        [Parameter(Mandatory=$true)][string]$LogPath,
        [switch]$AllowFailure
    )

    $start = Get-Date
    $commit = Get-GitCommit $WorkingDirectory

    Ensure-Dir (Split-Path -Parent $LogPath)

    $cmdLine = "`"$FilePath`" " + ($Arguments -join " ")
    "cmd=$cmdLine" | Out-File -FilePath $LogPath -Encoding utf8
    "cwd=$WorkingDirectory" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "commit=$commit" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "start=$($start.ToString('o'))" | Out-File -FilePath $LogPath -Encoding utf8 -Append

    $exitCode = 0
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $LogPath -Append | Out-Host
        $exitCode = $LASTEXITCODE
    } catch {
        $exitCode = 1
        $_ | Out-File -FilePath $LogPath -Encoding utf8 -Append
    } finally {
        Pop-Location
    }

    $end = Get-Date
    $duration = New-TimeSpan -Start $start -End $end
    "end=$($end.ToString('o'))" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    "exit_code=$exitCode" | Out-File -FilePath $LogPath -Encoding utf8 -Append

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        exit $exitCode
    }

    if ($AllowFailure) {
        return $exitCode
    }
}

function Resolve-UnityPath {
    if ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) {
        return $env:UNITY_PATH
    }

    $hubPath = Join-Path $env:ProgramFiles "Unity\\Hub\\Editor"
    if (Test-Path $hubPath) {
        $latest = Get-ChildItem -Path $hubPath -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($latest) {
            $candidate = Join-Path $latest.FullName "Editor\\Unity.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    return $null
}

function New-BuildMetadata {
    param(
        [Parameter(Mandatory=$true)][string]$RepoRoot,
        [Parameter(Mandatory=$true)][string]$Target
    )

    $commit = Get-GitCommit $RepoRoot
    $shortCommit = if ($commit -and $commit -ne "unknown" -and $commit.Length -ge 8) { $commit.Substring(0, 8) } else { "unknown" }
    $utcNow = (Get-Date).ToUniversalTime()
    $stamp = $utcNow.ToString("yyyyMMdd_HHmmss")
    $baseVersion = "0.1.0-dev"
    $versionFile = Join-Path $RepoRoot "build_version.txt"
    if (Test-Path $versionFile) {
        $content = (Get-Content $versionFile -ErrorAction SilentlyContinue | Select-Object -First 1)
        if ($content) {
            $baseVersion = $content.Trim()
        }
    }
    if ($baseVersion -match "\+") {
        $version = $baseVersion
    } else {
        $version = "$baseVersion+$shortCommit.$stamp"
    }

    return [ordered]@{
        version = $version
        commit = $commit
        build_utc = $utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        target = $Target
    }
}

function Write-BuildMetadata {
    param(
        [Parameter(Mandatory=$true)][string]$OutputDir,
        [Parameter(Mandatory=$true)][hashtable]$Metadata
    )

    Ensure-Dir $OutputDir
    $path = Join-Path $OutputDir "build_info.json"
    $Metadata | ConvertTo-Json -Depth 4 | Out-File -FilePath $path -Encoding utf8
    return $path
}
