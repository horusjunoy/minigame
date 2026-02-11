using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Runtime
{
    public static class MinigameMemoryProfiler
    {
        private static long _peakAllocatedBytes;

        public static void LogSnapshot(IRuntimeLogger logger, TelemetryContext telemetry, string phase)
        {
            if (logger == null)
            {
                return;
            }

            var allocated = Profiler.GetTotalAllocatedMemoryLong();
            if (allocated > _peakAllocatedBytes)
            {
                _peakAllocatedBytes = allocated;
            }

            var fields = new Dictionary<string, object>
            {
                ["phase"] = phase ?? string.Empty,
                ["gc_total_bytes"] = GC.GetTotalMemory(false),
                ["mono_used_bytes"] = Profiler.GetMonoUsedSizeLong(),
                ["allocated_bytes"] = allocated,
                ["peak_allocated_bytes"] = _peakAllocatedBytes,
                ["gameobject_count"] = Resources.FindObjectsOfTypeAll<GameObject>().Length
            };

            logger.Log(LogLevel.Info, "memory_snapshot", "Memory snapshot", fields, telemetry);
        }
    }
}
