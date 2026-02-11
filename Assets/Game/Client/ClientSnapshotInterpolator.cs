using System.Collections.Generic;
using Game.Network;
using UnityEngine;

namespace Game.Client
{
    public sealed class ClientSnapshotInterpolator
    {
        private readonly List<SnapshotV1> _buffer = new List<SnapshotV1>(32);
        private const int MaxBufferSize = 32;
        private const float InterpolationDelayMs = 150f;
        private float _nextLogTime;

        public void AddSnapshot(SnapshotV1 snapshot)
        {
            _buffer.Add(snapshot);
            if (_buffer.Count > MaxBufferSize)
            {
                _buffer.RemoveAt(0);
            }
        }

        public void Update(float realtimeSinceStartup)
        {
            if (_buffer.Count < 2)
            {
                return;
            }

            var targetTimeMs = _buffer[^1].server_time_ms - (long)InterpolationDelayMs;
            if (!TryFindSnapshots(targetTimeMs, out var older, out var newer))
            {
                return;
            }

            var span = newer.server_time_ms - older.server_time_ms;
            if (span <= 0)
            {
                return;
            }

            var t = Mathf.Clamp01((float)(targetTimeMs - older.server_time_ms) / span);
            if (realtimeSinceStartup < _nextLogTime)
            {
                return;
            }

            _nextLogTime = realtimeSinceStartup + 1f;
            if (older.entities == null || older.entities.Length == 0)
            {
                return;
            }

            var a = older.entities[0];
            var b = FindEntity(newer, a.player_id);
            if (b.player_id == null)
            {
                b = newer.entities[0];
            }

            var ix = Mathf.Lerp(a.px, b.px, t);
            var iy = Mathf.Lerp(a.py, b.py, t);
            var iz = Mathf.Lerp(a.pz, b.pz, t);
            Debug.Log($"snapshot_interpolated pos=({ix:0.00},{iy:0.00},{iz:0.00})");
        }

        private static SnapshotEntityV1 FindEntity(SnapshotV1 snapshot, string playerId)
        {
            if (snapshot.entities == null)
            {
                return default;
            }

            for (var i = 0; i < snapshot.entities.Length; i++)
            {
                if (snapshot.entities[i].player_id == playerId)
                {
                    return snapshot.entities[i];
                }
            }

            return default;
        }

        private bool TryFindSnapshots(long targetTimeMs, out SnapshotV1 older, out SnapshotV1 newer)
        {
            older = default;
            newer = default;

            for (var i = 0; i < _buffer.Count - 1; i++)
            {
                var a = _buffer[i];
                var b = _buffer[i + 1];
                if (a.server_time_ms <= targetTimeMs && targetTimeMs <= b.server_time_ms)
                {
                    older = a;
                    newer = b;
                    return true;
                }
            }

            return false;
        }
    }
}
