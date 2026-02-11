. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "pipeline"

$command = @'
.\scripts\build-client.ps1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
.\scripts\build-server.ps1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
.\scripts\test.ps1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
.\scripts\smoke-e2e.ps1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
.\scripts\deploy-staging.ps1
'@

Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
