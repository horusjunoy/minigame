using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace Game.Minigames.CoinRush
{
    public sealed class CoinRushMinigame : IMinigame
    {
        private const string TuningResourcePath = "Minigames/CoinRush/CoinRushTuning";

        private IMinigameContext _context;
        private float _elapsed;
        private float _nextAwardTime;
        private int _scoreToWin;
        private float _matchDurationSeconds;
        private int _pointsPerTick;
        private float _tickIntervalSeconds;

        public void OnLoad(IMinigameContext context)
        {
            _context = context;
            var tuning = Resources.Load<CoinRushTuning>(TuningResourcePath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<CoinRushTuning>();
            }

            tuning.Validate();

            _scoreToWin = ResolveScoreToWin(tuning, context.Settings);
            _matchDurationSeconds = ResolveMatchDuration(tuning, context.Settings);
            _pointsPerTick = tuning.pointsPerTick;
            _tickIntervalSeconds = tuning.tickIntervalSeconds;
            _elapsed = 0f;
            _nextAwardTime = _tickIntervalSeconds;

            _context.Logger.Log(LogLevel.Info, "minigame_loaded", "CoinRush minigame loaded", null, _context.Telemetry);
        }

        public void OnGameStart()
        {
            MinigameKit.BroadcastRoundStart(_context, 1);
            _context.Logger.Log(LogLevel.Info, "match_started", "CoinRush match started", null, _context.Telemetry);
        }

        public void OnPlayerJoin(PlayerRef player)
        {
            _context.Logger.Log(LogLevel.Info, "player_joined", $"Player joined: {player}", null, _context.Telemetry);
        }

        public void OnPlayerLeave(PlayerRef player)
        {
            _context.Logger.Log(LogLevel.Info, "player_left", $"Player left: {player}", null, _context.Telemetry);
        }

        public void OnTick(float dt)
        {
            if (_context == null)
            {
                return;
            }

            _elapsed += Mathf.Max(0f, dt);
            if (_matchDurationSeconds > 0f && _elapsed >= _matchDurationSeconds)
            {
                _context.EndGame(EndGameReason.Timeout);
                return;
            }

            if (_tickIntervalSeconds <= 0f)
            {
                return;
            }

            if (_elapsed < _nextAwardTime)
            {
                return;
            }

            _nextAwardTime = _elapsed + _tickIntervalSeconds;
            var players = _context.GetPlayers();
            for (var i = 0; i < players.Count; i++)
            {
                _context.AddScore(players[i], _pointsPerTick);
            }

            MinigameKit.BroadcastScoreboard(_context);
            if (_scoreToWin > 0 && HasWinner(players))
            {
                _context.EndGame(EndGameReason.Completed);
            }
        }

        public void OnGameEnd(GameResult result)
        {
            MinigameKit.BroadcastRoundEnd(_context, 1, result.Reason);
            _context.Logger.Log(LogLevel.Info, "match_ended", $"CoinRush match ended: {result.Reason}", null, _context.Telemetry);
        }

        private bool HasWinner(System.Collections.Generic.IReadOnlyList<PlayerRef> players)
        {
            var scoreboard = _context.GetScoreboard().Snapshot();
            for (var i = 0; i < players.Count; i++)
            {
                if (scoreboard.TryGetValue(players[i].Id, out var score) && score >= _scoreToWin)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveScoreToWin(CoinRushTuning tuning, Settings settings)
        {
            if (tuning.scoreToWinOverride > 0)
            {
                return tuning.scoreToWinOverride;
            }

            return settings != null && settings.score_to_win > 0 ? settings.score_to_win : 10;
        }

        private static float ResolveMatchDuration(CoinRushTuning tuning, Settings settings)
        {
            if (tuning.matchDurationSecondsOverride > 0)
            {
                return tuning.matchDurationSecondsOverride;
            }

            return settings != null && settings.match_duration_s > 0 ? settings.match_duration_s : 300f;
        }
    }
}
