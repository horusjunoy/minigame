. "$PSScriptRoot\\_common.ps1"

$repoRoot = Get-RepoRoot
$logPath = New-LogPath $repoRoot "check"

$command = @'
$required = @(
  "Assets/Game/Core",
  "Assets/Game/Runtime",
  "Assets/Game/Network",
  "Assets/Game/Server",
  "Assets/Game/Client",
  "Assets/Game/Minigames/Stub",
  "docs"
)
foreach ($path in $required) {
  if (-not (Test-Path $path)) {
    Write-Error "Missing required path: $path"
    exit 1
  }
}
Write-Host "check ok"
'@

Invoke-LoggedCommand -Command $command -WorkingDirectory $repoRoot -LogPath $logPath
