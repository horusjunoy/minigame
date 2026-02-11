using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    public sealed class StubMinigameContext : IMinigameContext
    {
        private readonly List<PlayerRef> _players = new List<PlayerRef>();
        private readonly Scoreboard _scoreboard = new Scoreboard();
        private readonly HashSet<EntityId> _activeEntities = new HashSet<EntityId>();

        public Settings Settings { get; }
        public TelemetryContext Telemetry { get; }
        public IRuntimeLogger Logger { get; }
        public bool HasEnded { get; private set; }
        public GameResult LastResult { get; private set; }
        public int ActiveEntityCount => _activeEntities.Count;

        public StubMinigameContext(TelemetryContext telemetry, IRuntimeLogger logger, Settings settings = null)
        {
            Settings = settings ?? new Settings();
            Telemetry = telemetry;
            Logger = logger;
        }

        public IReadOnlyList<PlayerRef> GetPlayers() => _players;

        public EntityRef Spawn(EntityId id, Vector3 pos, Quaternion rot, SpawnOptions options = null)
        {
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
            Logger.Log(LogLevel.Info, "broadcast", eventName, payload, Telemetry);
        }

        public void SendTo(PlayerRef player, string eventName, object payload)
        {
            Logger.Log(LogLevel.Info, "send_to_player", eventName, payload, Telemetry);
        }

        public void AddPlayer(PlayerRef player) => _players.Add(player);
    }
}
