. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "deploy_staging"
$summaryPath = Join-Path $repoRoot ("logs\\deploy_staging_results_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
Ensure-Dir (Split-Path -Parent $logPath)

function Write-Result([string]$Line) {
    $Line | Out-Host
    $Line | Out-File -FilePath $summaryPath -Encoding utf8 -Append
}

$start = Get-Date
$commit = Get-GitCommit $repoRoot
"cmd=deploy-staging" | Out-File -FilePath $logPath -Encoding utf8
"cwd=$repoRoot" | Out-File -FilePath $logPath -Encoding utf8 -Append
"commit=$commit" | Out-File -FilePath $logPath -Encoding utf8 -Append
"start=$($start.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append

$clientDir = Join-Path $repoRoot "artifacts\\builds\\client"
$serverDir = Join-Path $repoRoot "artifacts\\builds\\server"
$clientInfo = Join-Path $clientDir "build_info.json"
$serverInfo = Join-Path $serverDir "build_info.json"

if (-not (Test-Path $clientDir) -or -not (Test-Path $serverDir)) {
    Write-Result "deploy_failed missing_builds"
    exit 1
}

$version = "unknown"
if (Test-Path $clientInfo) {
    try {
        $json = Get-Content -Path $clientInfo | ConvertFrom-Json
        if ($json.version) { $version = $json.version }
    } catch {}
}

$stagingRoot = Join-Path $repoRoot "artifacts\\staging\\$version"
Ensure-Dir $stagingRoot

Copy-Item -Path $clientDir -Destination (Join-Path $stagingRoot "client") -Recurse -Force
Copy-Item -Path $serverDir -Destination (Join-Path $stagingRoot "server") -Recurse -Force

$manifest = [ordered]@{
    version = $version
    commit = $commit
    build_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    client_build_info = if (Test-Path $clientInfo) { $clientInfo } else { $null }
    server_build_info = if (Test-Path $serverInfo) { $serverInfo } else { $null }
}
$manifestPath = Join-Path $stagingRoot "staging_manifest.json"
$manifest | ConvertTo-Json -Depth 4 | Out-File -FilePath $manifestPath -Encoding utf8

Write-Result "staging_version=$version"
Write-Result "staging_path=$stagingRoot"
Write-Result "manifest=$manifestPath"
Write-Result "finalizado"

$end = Get-Date
$duration = New-TimeSpan -Start $start -End $end
"end=$($end.ToString('o'))" | Out-File -FilePath $logPath -Encoding utf8 -Append
"duration_ms=$([int]$duration.TotalMilliseconds)" | Out-File -FilePath $logPath -Encoding utf8 -Append
"exit_code=0" | Out-File -FilePath $logPath -Encoding utf8 -Append
