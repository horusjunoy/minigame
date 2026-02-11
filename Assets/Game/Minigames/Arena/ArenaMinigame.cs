using System;
using System.Collections.Generic;
using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace Game.Minigames.Arena
{
    public sealed class ArenaMinigame : IMinigame
    {
        private const int DefaultMatchDurationSeconds = 600;
        private const int DefaultScoreToWin = 10;
        private const int DefaultPickupCount = 6;
        private const float PickupRespawnSeconds = 2.5f;
        private const float CollectIntervalSeconds = 1.0f;
        private const float ArenaHalfSize = 8f;

        private sealed class PickupState
        {
            public EntityId Id;
            public EntityRef Entity;
            public Vector3 Position;
            public bool Active;
            public float RespawnAt;
        }

        private IMinigameContext _context;
        private readonly List<PickupState> _pickups = new List<PickupState>();
        private readonly Dictionary<PlayerId, EntityRef> _playerEntities = new Dictionary<PlayerId, EntityRef>();
        private System.Random _rng;
        private float _elapsed;
        private float _matchDuration;
        private int _scoreToWin;
        private float _nextCollectTime;
        private int _nextPickupId;
        private int _nextCollectorIndex;
        private bool _ended;

        public void OnLoad(IMinigameContext context)
        {
            _context = context;
            var settings = context.Settings ?? new Settings();
            if (settings.match_duration_s < 0)
            {
                _matchDuration = Mathf.Max(1, -settings.match_duration_s);
            }
            else
            {
                var duration = settings.match_duration_s > 0 ? settings.match_duration_s : DefaultMatchDurationSeconds;
                duration = Mathf.Clamp(duration, 300, 900);
                _matchDuration = duration;
            }

            if (settings.score_to_win < 0)
            {
                _scoreToWin = 0;
            }
            else
            {
                _scoreToWin = settings.score_to_win > 0 ? settings.score_to_win : DefaultScoreToWin;
            }
            _rng = new System.Random((int)DateTime.UtcNow.Ticks);

            _context.Logger.Log(
                LogLevel.Info,
                "minigame_loaded",
                "Arena minigame loaded",
                $"duration_s={_matchDuration},score_to_win={_scoreToWin}",
                _context.Telemetry);
        }

        public void OnGameStart()
        {
            _elapsed = 0f;
            _ended = false;
            _nextCollectTime = CollectIntervalSeconds;
            _pickups.Clear();
            _nextPickupId = 0;

            for (var i = 0; i < DefaultPickupCount; i++)
            {
                SpawnPickup(i == 0);
            }

            _context.Logger.Log(LogLevel.Info, "match_started", "Arena match started", null, _context.Telemetry);
        }

        public void OnPlayerJoin(PlayerRef player)
        {
            if (!_playerEntities.ContainsKey(player.Id))
            {
                var pos = RandomPosition();
                var entity = _context.Spawn(new EntityId($"player_{player.Id.Value}"), pos, Quaternion.identity, new SpawnOptions
                {
                    IsServerAuthority = true
                });
                _playerEntities[player.Id] = entity;
            }

            _context.SetScore(player, 0);
            BroadcastScoreboard();
            _context.Logger.Log(LogLevel.Info, "player_joined", $"Player joined arena: {player}", null, _context.Telemetry);
        }

        public void OnPlayerLeave(PlayerRef player)
        {
            if (_playerEntities.TryGetValue(player.Id, out var entity))
            {
                _context.Despawn(entity);
                _playerEntities.Remove(player.Id);
            }

            _context.Logger.Log(LogLevel.Info, "player_left", $"Player left arena: {player}", null, _context.Telemetry);
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

            UpdatePickups();

            if (_elapsed >= _nextCollectTime)
            {
                _nextCollectTime = _elapsed + CollectIntervalSeconds;
                TryCollectPickup();
            }
        }

        public void OnGameEnd(GameResult result)
        {
            LogRewardIfEnabled();
            CleanupEntities();
            _context.Logger.Log(LogLevel.Info, "match_ended", $"Arena match ended: {result.Reason}", null, _context.Telemetry);
        }

        private void UpdatePickups()
        {
            for (var i = 0; i < _pickups.Count; i++)
            {
                var pickup = _pickups[i];
                if (pickup.Active)
                {
                    continue;
                }

                if (_elapsed >= pickup.RespawnAt)
                {
                    ActivatePickup(pickup);
                }
            }
        }

        private void TryCollectPickup()
        {
            var players = _context.GetPlayers();
            if (players == null || players.Count == 0)
            {
                return;
            }

            var pickup = GetNextActivePickup();
            if (pickup == null)
            {
                return;
            }

            var player = players[_nextCollectorIndex % players.Count];
            _nextCollectorIndex = (_nextCollectorIndex + 1) % players.Count;

            pickup.Active = false;
            pickup.RespawnAt = _elapsed + PickupRespawnSeconds;
            _context.Despawn(pickup.Entity);

            _context.AddScore(player, 1);
            BroadcastScoreboard();
            _context.Broadcast("pickup_collected", new PickupCollectedPayload(player.Id.Value, pickup.Id.Value));

            var currentScore = GetScore(player.Id);
            if (_scoreToWin > 0 && currentScore >= _scoreToWin)
            {
                _ended = true;
                _context.EndGame(EndGameReason.Completed);
            }
        }

        private int GetScore(PlayerId playerId)
        {
            var snapshot = _context.GetScoreboard().Snapshot();
            return snapshot.TryGetValue(playerId, out var score) ? score : 0;
        }

        private PickupState GetNextActivePickup()
        {
            for (var i = 0; i < _pickups.Count; i++)
            {
                if (_pickups[i].Active)
                {
                    return _pickups[i];
                }
            }

            return null;
        }

        private void SpawnPickup(bool immediate)
        {
            var pickup = new PickupState();
            _pickups.Add(pickup);
            if (immediate)
            {
                ActivatePickup(pickup);
            }
            else
            {
                pickup.Active = false;
                pickup.RespawnAt = _elapsed + (float)_rng.NextDouble() * PickupRespawnSeconds;
            }
        }

        private void ActivatePickup(PickupState pickup)
        {
            pickup.Id = new EntityId($"pickup_{_nextPickupId++}");
            pickup.Position = RandomPosition();
            pickup.Entity = _context.Spawn(pickup.Id, pickup.Position, Quaternion.identity);
            pickup.Active = true;
            pickup.RespawnAt = 0f;

            _context.Broadcast("pickup_spawned", new PickupSpawnedPayload(pickup.Id.Value, pickup.Position.x, pickup.Position.y, pickup.Position.z));
        }

        private Vector3 RandomPosition()
        {
            var x = Mathf.Lerp(-ArenaHalfSize, ArenaHalfSize, (float)_rng.NextDouble());
            var z = Mathf.Lerp(-ArenaHalfSize, ArenaHalfSize, (float)_rng.NextDouble());
            return new Vector3(x, 0f, z);
        }

        private void BroadcastScoreboard()
        {
            var snapshot = _context.GetScoreboard().Snapshot();
            var payload = new ScoreboardPayload(snapshot);
            _context.Broadcast("score_updated", payload);
        }

        private void LogRewardIfEnabled()
        {
            var topPlayer = GetTopPlayer();
            if (topPlayer == null)
            {
                return;
            }

            EconomyEventPublisher.LogRewardGranted(_context.Logger, _context.Telemetry, "arena_win_reward", 1, topPlayer.Value.Value);
        }

        private PlayerId? GetTopPlayer()
        {
            var snapshot = _context.GetScoreboard().Snapshot();
            PlayerId? winner = null;
            var bestScore = int.MinValue;
            foreach (var entry in snapshot)
            {
                if (entry.Value > bestScore)
                {
                    bestScore = entry.Value;
                    winner = entry.Key;
                }
            }

            return winner;
        }

        private void CleanupEntities()
        {
            foreach (var entity in _playerEntities.Values)
            {
                _context.Despawn(entity);
            }
            _playerEntities.Clear();

            for (var i = 0; i < _pickups.Count; i++)
            {
                var pickup = _pickups[i];
                if (pickup.Active)
                {
                    _context.Despawn(pickup.Entity);
                    pickup.Active = false;
                }
            }
            _pickups.Clear();
        }

        private sealed class PickupSpawnedPayload
        {
            public string pickup_id;
            public float x;
            public float y;
            public float z;

            public PickupSpawnedPayload(string pickupId, float x, float y, float z)
            {
                pickup_id = pickupId;
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        private sealed class PickupCollectedPayload
        {
            public string player_id;
            public string pickup_id;

            public PickupCollectedPayload(string playerId, string pickupId)
            {
                player_id = playerId;
                pickup_id = pickupId;
            }
        }

        private sealed class ScoreboardPayload
        {
            public List<Entry> scores;

            public ScoreboardPayload(IReadOnlyDictionary<PlayerId, int> snapshot)
            {
                scores = new List<Entry>();
                foreach (var entry in snapshot)
                {
                    scores.Add(new Entry(entry.Key.Value, entry.Value));
                }
            }

            public sealed class Entry
            {
                public string player_id;
                public int score;

                public Entry(string playerId, int score)
                {
                    player_id = playerId;
                    this.score = score;
                }
            }
        }
    }
}
