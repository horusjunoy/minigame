using System.Diagnostics;
using Game.Core;
using Game.Runtime;

namespace Game.Server
{
    public sealed class ServerMatchRunner
    {
        private const double TickBudgetMs = 33.0;
        private readonly IRuntimeLogger _logger;
        private readonly TelemetryContext _telemetry;

        public ServerMatchRunner(IRuntimeLogger logger, TelemetryContext telemetry)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        public void RunTick(IMinigame minigame, float dt)
        {
            var sw = Stopwatch.StartNew();
            minigame.OnTick(dt);
            sw.Stop();

            if (sw.Elapsed.TotalMilliseconds > TickBudgetMs)
            {
                _logger.Log(
                    LogLevel.Warn,
                    "tick_over_budget",
                    $"Tick budget exceeded: {sw.Elapsed.TotalMilliseconds:0.00}ms",
                    null,
                    _telemetry);
            }
        }
    }
}
