using System;
using System.Collections.Generic;
using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace Game.Minigames.Race
{
    public sealed class RaceMinigame : IMinigame
    {
        private const int DefaultMatchDurationSeconds = 480;
        private const int DefaultScoreToWin = 3;
        private const float TrackLength = 60f;
        private const float BaseSpeed = 2.5f;
        private const float SpeedJitter = 1.5f;

        private sealed class RacerState
        {
            public PlayerId PlayerId;
            public float Progress;
            public int Laps;
            public bool Finished;
        }

        private IMinigameContext _context;
        private readonly Dictionary<PlayerId, RacerState> _racers = new Dictionary<PlayerId, RacerState>();
        private readonly List<RacerState> _finishOrder = new List<RacerState>();
        private System.Random _rng;
        private float _elapsed;
        private float _matchDuration;
        private int _scoreToWin;
        private bool _ended;

        public void OnLoad(IMinigameContext context)
        {
            _context = context;
            var settings = context.Settings ?? new Settings();
            var duration = settings.match_duration_s > 0 ? settings.match_duration_s : DefaultMatchDurationSeconds;
            duration = Mathf.Clamp(duration, 180, 900);
            _matchDuration = duration;
            _scoreToWin = settings.score_to_win > 0 ? settings.score_to_win : DefaultScoreToWin;
            _rng = new System.Random((int)DateTime.UtcNow.Ticks);

            _context.Logger.Log(
                LogLevel.Info,
                "minigame_loaded",
                "Race minigame loaded",
                $"duration_s={_matchDuration},score_to_win={_scoreToWin}",
                _context.Telemetry);
        }

        public void OnGameStart()
        {
            _elapsed = 0f;
            _ended = false;
            _racers.Clear();
            _finishOrder.Clear();
            _context.Logger.Log(LogLevel.Info, "match_started", "Race match started", null, _context.Telemetry);
        }

        public void OnPlayerJoin(PlayerRef player)
        {
            if (_racers.ContainsKey(player.Id))
            {
                return;
            }

            var racer = new RacerState
            {
                PlayerId = player.Id,
                Progress = 0f,
                Laps = 0,
                Finished = false
            };
            _racers[player.Id] = racer;
            _context.SetScore(player, 0);
            BroadcastStandings();
        }

        public void OnPlayerLeave(PlayerRef player)
        {
            _racers.Remove(player.Id);
            _context.Logger.Log(LogLevel.Info, "player_left", $"Player left race: {player}", null, _context.Telemetry);
        }

        public void OnTick(float dt)
        {
            if (_ended)
            {
                return;
            }

            _elapsed += dt;
            if (_elapsed >= _matchDuration)
            {
                _ended = true;
                _context.EndGame(EndGameReason.Timeout);
                return;
            }

            foreach (var racer in _racers.Values)
            {
                if (racer.Finished)
                {
                    continue;
                }

                var speed = BaseSpeed + (float)_rng.NextDouble() * SpeedJitter;
                racer.Progress += speed * dt;
                if (racer.Progress >= TrackLength)
                {
                    racer.Progress -= TrackLength;
                    racer.Laps += 1;
                    _context.AddScore(new PlayerRef(racer.PlayerId), 1);
                    BroadcastStandings();
                    _context.Broadcast("lap_completed", new LapPayload(racer.PlayerId.Value, racer.Laps));

                    if (_scoreToWin > 0 && racer.Laps >= _scoreToWin)
                    {
                        racer.Finished = true;
                        _finishOrder.Add(racer);
                        _context.Broadcast("racer_finished", new FinishPayload(racer.PlayerId.Value, _finishOrder.Count));

                        if (_finishOrder.Count == 1)
                        {
                            _ended = true;
                            _context.EndGame(EndGameReason.Completed);
                        }
                    }
                }
            }
        }

        public void OnGameEnd(GameResult result)
        {
            _context.Logger.Log(LogLevel.Info, "match_ended", $"Race match ended: {result.Reason}", null, _context.Telemetry);
        }

        private void BroadcastStandings()
        {
            var snapshot = _context.GetScoreboard().Snapshot();
            var payload = new StandingsPayload(snapshot);
            _context.Broadcast("race_standings", payload);
        }

        private sealed class LapPayload
        {
            public string player_id;
            public int laps;

            public LapPayload(string playerId, int laps)
            {
                player_id = playerId;
                this.laps = laps;
            }
        }

        private sealed class FinishPayload
        {
            public string player_id;
            public int place;

            public FinishPayload(string playerId, int place)
            {
                player_id = playerId;
                this.place = place;
            }
        }

        private sealed class StandingsPayload
        {
            public List<Entry> standings;

            public StandingsPayload(IReadOnlyDictionary<PlayerId, int> snapshot)
            {
                standings = new List<Entry>();
                foreach (var entry in snapshot)
                {
                    standings.Add(new Entry(entry.Key.Value, entry.Value));
                }
            }

            public sealed class Entry
            {
                public string player_id;
                public int laps;

                public Entry(string playerId, int laps)
                {
                    player_id = playerId;
                    this.laps = laps;
                }
            }
        }
    }
}
