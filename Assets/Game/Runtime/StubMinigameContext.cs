using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    public sealed class StubMinigameContext : IMinigameContext, ITickBoundContext, IMinigameBudgetProvider
    {
        private readonly List<PlayerRef> _players = new List<PlayerRef>();
        private readonly Scoreboard _scoreboard = new Scoreboard();
        private readonly HashSet<EntityId> _activeEntities = new HashSet<EntityId>();
        private readonly MinigamePermissions _permissions;
        private int _broadcastsThisTick;
        private int _sendsThisTick;

        public Settings Settings { get; }
        public TelemetryContext Telemetry { get; }
        public IRuntimeLogger Logger { get; }
        public bool HasEnded { get; private set; }
        public GameResult LastResult { get; private set; }
        public int ActiveEntityCount => _activeEntities.Count;
        public double TickBudgetMs => _permissions != null ? _permissions.tick_budget_ms : 33.0;
        public int AllocationSampleRate => _permissions != null ? _permissions.allocation_sample_rate : 60;

        public StubMinigameContext(TelemetryContext telemetry, IRuntimeLogger logger, Settings settings = null, MinigamePermissions permissions = null)
        {
            Settings = settings ?? new Settings();
            Telemetry = telemetry;
            Logger = logger;
            _permissions = permissions ?? new MinigamePermissions();
        }

        public IReadOnlyList<PlayerRef> GetPlayers() => _players;

        public EntityRef Spawn(EntityId id, Vector3 pos, Quaternion rot, SpawnOptions options = null)
        {
            if (_permissions != null && _permissions.max_entities > 0 && _activeEntities.Count >= _permissions.max_entities)
            {
                Logger.Log(LogLevel.Warn, "permission_blocked", "Spawn blocked: max_entities", new { max = _permissions.max_entities }, Telemetry);
                return new EntityRef(id);
            }

            _activeEntities.Add(id);
            return new EntityRef(id);
        }

        public void Despawn(EntityRef entity)
        {
            _activeEntities.Remove(entity.Id);
        }

        public void SetScore(PlayerRef player, int value) => _scoreboard.SetScore(player.Id, value);
        public void AddScore(PlayerRef player, int delta) => _scoreboard.AddScore(player.Id, delta);
        public Scoreboard GetScoreboard() => _scoreboard;

        public void EndGame(EndGameReason reason, GameResult? overrideResult = null)
        {
            var result = overrideResult ?? new GameResult(reason);
            HasEnded = true;
            LastResult = result;
            Logger.Log(LogLevel.Info, "match_ended", $"EndGame: {result.Reason}", null, Telemetry);
        }

        public void Broadcast(string eventName, object payload)
        {
            if (!ValidateEvent("broadcast", eventName, payload, ref _broadcastsThisTick, _permissions?.max_broadcasts_per_tick ?? 0, out var safeName, out var safePayload))
            {
                return;
            }

            Logger.Log(LogLevel.Info, "broadcast", safeName, safePayload, Telemetry);
        }

        public void SendTo(PlayerRef player, string eventName, object payload)
        {
            if (!ValidateEvent("send_to_player", eventName, payload, ref _sendsThisTick, _permissions?.max_sends_per_tick ?? 0, out var safeName, out var safePayload))
            {
                return;
            }

            Logger.Log(LogLevel.Info, "send_to_player", safeName, safePayload, Telemetry);
        }

        public void AddPlayer(PlayerRef player) => _players.Add(player);

        public void BeginTick()
        {
            _broadcastsThisTick = 0;
            _sendsThisTick = 0;
        }

        private bool ValidateEvent(string eventType, string eventName, object payload, ref int counter, int maxPerTick, out string safeName, out object safePayload)
        {
            safeName = eventName ?? string.Empty;
            safePayload = payload;
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Logger.Log(LogLevel.Warn, "payload_invalid", $"{eventType}: event name empty", null, Telemetry);
                return false;
            }

            if (_permissions != null && _permissions.max_event_name_len > 0 && eventName.Length > _permissions.max_event_name_len)
            {
                Logger.Log(LogLevel.Warn, "payload_clamped", $"{eventType}: event name too long", new { max = _permissions.max_event_name_len }, Telemetry);
                safeName = eventName.Substring(0, _permissions.max_event_name_len);
            }

            if (_permissions != null && maxPerTick > 0)
            {
                counter += 1;
                if (counter > maxPerTick)
                {
                    Logger.Log(LogLevel.Warn, "permission_blocked", $"{eventType}: rate limit", new { max = maxPerTick }, Telemetry);
                    return false;
                }
            }

            if (_permissions != null && _permissions.max_payload_len > 0 && payload is string text && text.Length > _permissions.max_payload_len)
            {
                Logger.Log(LogLevel.Warn, "payload_clamped", $"{eventType}: payload too long", new { max = _permissions.max_payload_len }, Telemetry);
                safePayload = text.Substring(0, _permissions.max_payload_len);
            }

            return true;
        }
    }
}
