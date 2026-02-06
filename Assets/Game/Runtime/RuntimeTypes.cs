using System.Collections.Generic;
using Game.Core;

namespace Game.Runtime
{
    public readonly struct EntityId
    {
        public readonly string Value;
        public EntityId(string value) => Value = value;
        public override string ToString() => Value;
    }

    public readonly struct EntityRef
    {
        public readonly EntityId Id;
        public EntityRef(EntityId id) => Id = id;
        public override string ToString() => Id.ToString();
    }

    public sealed class SpawnOptions
    {
        public bool IsServerAuthority { get; set; } = true;
    }

    public sealed class Scoreboard
    {
        private readonly Dictionary<PlayerId, int> _scores = new Dictionary<PlayerId, int>();

        public void SetScore(PlayerId playerId, int value) => _scores[playerId] = value;
        public void AddScore(PlayerId playerId, int delta) =>
            _scores[playerId] = _scores.TryGetValue(playerId, out var current) ? current + delta : delta;

        public IReadOnlyDictionary<PlayerId, int> Snapshot() => _scores;
    }

    public interface IRuntimeLogger
    {
        void Log(LogLevel level, string eventName, string message, object fields = null, TelemetryContext? context = null);
    }
}
