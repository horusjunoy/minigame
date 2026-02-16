using System.Diagnostics;
using Game.Core;
using Game.Runtime;

namespace Game.Server
{
    public sealed class ServerMatchRunner
    {
        private readonly IRuntimeLogger _logger;
        private readonly TelemetryContext _telemetry;
        private int _tickIndex;
        private long _lastAllocatedBytes;

        public ServerMatchRunner(IRuntimeLogger logger, TelemetryContext telemetry)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        public void RunTick(IMinigame minigame, IMinigameContext context, float dt)
        {
            if (context is ITickBoundContext tickBound)
            {
                tickBound.BeginTick();
            }

            var budgetMs = ResolveTickBudget(context);
            var allocationSampleRate = ResolveAllocationSampleRate(context);
            var sw = Stopwatch.StartNew();
            minigame.OnTick(dt);
            sw.Stop();

            if (sw.Elapsed.TotalMilliseconds > budgetMs)
            {
                _logger.Log(
                    LogLevel.Warn,
                    "tick_over_budget",
                    $"Tick budget exceeded: {sw.Elapsed.TotalMilliseconds:0.00}ms",
                    new { tick_budget_ms = budgetMs },
                    _telemetry);
            }

            _tickIndex += 1;
            if (allocationSampleRate > 0 && _tickIndex % allocationSampleRate == 0)
            {
                var allocated = System.GC.GetTotalMemory(false);
                var delta = _lastAllocatedBytes == 0 ? 0 : allocated - _lastAllocatedBytes;
                _lastAllocatedBytes = allocated;
                _logger.Log(
                    LogLevel.Info,
                    "tick_allocations_sampled",
                    "Tick allocations sampled",
                    new { allocated_bytes = allocated, delta_bytes = delta, sample_rate = allocationSampleRate },
                    _telemetry);
            }
        }

        private static double ResolveTickBudget(IMinigameContext context)
        {
            if (context is IMinigameBudgetProvider provider)
            {
                return provider.TickBudgetMs > 0 ? provider.TickBudgetMs : 33.0;
            }

            return 33.0;
        }

        private static int ResolveAllocationSampleRate(IMinigameContext context)
        {
            if (context is IMinigameBudgetProvider provider)
            {
                return provider.AllocationSampleRate > 0 ? provider.AllocationSampleRate : 60;
            }

            return 60;
        }
    }
}
