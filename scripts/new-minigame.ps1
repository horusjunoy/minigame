param(
    [Parameter(Mandatory=$true)][string]$Name,
    [string]$MinigameId,
    [string]$DisplayName,
    [string]$Version = "0.1.0",
    [int]$MatchDurationSeconds = 300,
    [int]$ScoreToWin = 3,
    [switch]$Force
)

. "$PSScriptRoot\_common.ps1"

$repoRoot = Get-RepoRoot
$nameTrimmed = $Name.Trim()
if (-not $nameTrimmed) {
    Write-Error "Name is required"
    exit 1
}

function To-PascalCase([string]$value) {
    $clean = $value -replace '[^a-zA-Z0-9_]',' '
    $parts = $clean.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -eq 0) { return "Minigame" }
    $out = ""
    foreach ($p in $parts) {
        if ($p.Length -eq 1) { $out += $p.ToUpperInvariant() }
        else { $out += ($p.Substring(0,1).ToUpperInvariant() + $p.Substring(1)) }
    }
    return $out
}

$pascal = To-PascalCase $nameTrimmed
$minigameId = if ($MinigameId) { $MinigameId } else { ($pascal.ToLowerInvariant() + "_v1") }
$display = if ($DisplayName) { $DisplayName } else { $pascal }
$folder = Join-Path $repoRoot "Assets\Game\Minigames\$pascal"

if ((Test-Path $folder) -and -not $Force) {
    Write-Error "Minigame folder already exists: $folder (use -Force to overwrite)"
    exit 1
}

Ensure-Dir $folder

$className = "${pascal}Minigame"
$namespace = "Game.Minigames.$pascal"
$asmdefName = "Game.Minigames.$pascal"

$asmdefPath = Join-Path $folder "Game.Minigames.$pascal.asmdef"
$manifestPath = Join-Path $folder "$className.manifest.json"
$classPath = Join-Path $folder "$className.cs"
$tuningPath = Join-Path $folder "${pascal}Tuning.cs"

$asmdefContent = @"
{
  \"name\": \"$asmdefName\",
  \"references\": [
    \"Game.Core\",
    \"Game.Runtime\"
  ],
  \"includePlatforms\": [],
  \"excludePlatforms\": [],
  \"allowUnsafeCode\": false,
  \"overrideReferences\": false,
  \"precompiledReferences\": [],
  \"autoReferenced\": true,
  \"defineConstraints\": [],
  \"versionDefines\": [],
  \"noEngineReferences\": false
}
"@

$manifestContent = @"
{
  \"schema_version\": 1,
  \"id\": \"$minigameId\",
  \"display_name\": \"$display\",
  \"version\": \"$Version\",
  \"content_version\": \"$Version\",
  \"server_entry\": \"$namespace.$className, $asmdefName\",
  \"client_entry\": \"\",
  \"addressables\": {
    \"scenes\": [],
    \"prefabs\": []
  },
  \"settings\": {
    \"match_duration_s\": $MatchDurationSeconds,
    \"score_to_win\": $ScoreToWin
  }
}
"@

$classContent = @"
using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace $namespace
{
    public sealed class $className : IMinigame
    {
        private IMinigameContext _context;
        private ${pascal}Tuning _tuning;

        public void OnLoad(IMinigameContext context)
        {
            _context = context;
            _tuning = Resources.Load<${pascal}Tuning>("Minigames/$pascal/${pascal}Tuning");
            if (_tuning == null)
            {
                _tuning = ScriptableObject.CreateInstance<${pascal}Tuning>();
            }
            _tuning.Validate();
            _context.Logger.Log(LogLevel.Info, \"minigame_loaded\", \"$pascal minigame loaded\", null, _context.Telemetry);
        }

        public void OnGameStart()
        {
            MinigameKit.BroadcastRoundStart(_context, 1);
            _context.Logger.Log(LogLevel.Info, \"match_started\", \"$pascal match started\", null, _context.Telemetry);
        }

        public void OnPlayerJoin(PlayerRef player)
        {
            _context.Logger.Log(LogLevel.Info, \"player_joined\", $"Player joined: {player}", null, _context.Telemetry);
        }

        public void OnPlayerLeave(PlayerRef player)
        {
            _context.Logger.Log(LogLevel.Info, \"player_left\", $"Player left: {player}", null, _context.Telemetry);
        }

        public void OnTick(float dt)
        {
        }

        public void OnGameEnd(GameResult result)
        {
            MinigameKit.BroadcastRoundEnd(_context, 1, result.Reason);
            _context.Logger.Log(LogLevel.Info, \"match_ended\", $"$pascal match ended: {result.Reason}", null, _context.Telemetry);
        }
    }
}
"@

$tuningContent = @"
using UnityEngine;

namespace $namespace
{
    [CreateAssetMenu(menuName = \"Minigames/$pascal/Tuning\", fileName = \"${pascal}Tuning\")]
    public sealed class ${pascal}Tuning : ScriptableObject
    {
        public int scoreToWinOverride;
        public int matchDurationSecondsOverride;

        public void Validate()
        {
            scoreToWinOverride = Mathf.Max(0, scoreToWinOverride);
            matchDurationSecondsOverride = Mathf.Max(0, matchDurationSecondsOverride);
        }

        private void OnValidate()
        {
            Validate();
        }
    }
}
"@

$asmdefContent | Set-Content -Path $asmdefPath -Encoding utf8
$manifestContent | Set-Content -Path $manifestPath -Encoding utf8
$classContent | Set-Content -Path $classPath -Encoding utf8
$tuningContent | Set-Content -Path $tuningPath -Encoding utf8

Write-Host "Created minigame at $folder"
Write-Host "- $asmdefPath"
Write-Host "- $manifestPath"
Write-Host "- $classPath"
Write-Host "- $tuningPath"
