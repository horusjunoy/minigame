using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    public interface IMinigameContext
    {
        Settings Settings { get; }
        TelemetryContext Telemetry { get; }
        IRuntimeLogger Logger { get; }
        IReadOnlyList<PlayerRef> GetPlayers();

        EntityRef Spawn(EntityId id, Vector3 pos, Quaternion rot, SpawnOptions options = null);
        void Despawn(EntityRef entity);

        void SetScore(PlayerRef player, int value);
        void AddScore(PlayerRef player, int delta);
        Scoreboard GetScoreboard();

        void EndGame(EndGameReason reason, GameResult? overrideResult = null);

        void Broadcast(string eventName, object payload);
        void SendTo(PlayerRef player, string eventName, object payload);
    }
}
