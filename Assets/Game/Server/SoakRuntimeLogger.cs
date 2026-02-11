using System;
using System.Text;
using Game.Core;
using Game.Runtime;

namespace Game.Server
{
    public sealed class SoakRuntimeLogger : IRuntimeLogger
    {
        private readonly JsonRuntimeLogger _inner = new JsonRuntimeLogger();

        public int TickOverBudgetCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int DisconnectCount { get; private set; }

        public void Log(LogLevel level, string eventName, string message, object fields = null, TelemetryContext? context = null)
        {
            if (eventName == "tick_over_budget")
            {
                TickOverBudgetCount++;
            }

            if (eventName == "player_left" || eventName == "client_disconnected")
            {
                DisconnectCount++;
            }

            if (level == LogLevel.Error || eventName == "minigame_error")
            {
                ErrorCount++;
            }

            _inner.Log(level, eventName, message, fields, context);
        }

        public string BuildSummary(string minigameId, int botCount, double durationSeconds, bool endedEarly, string endReason)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine("Soak Summary");
            sb.AppendLine($"minigame_id={minigameId}");
            sb.AppendLine($"bot_count={botCount}");
            sb.AppendLine($"duration_s={durationSeconds:0.0}");
            sb.AppendLine($"ended_early={endedEarly}");
            if (!string.IsNullOrWhiteSpace(endReason))
            {
                sb.AppendLine($"end_reason={endReason}");
            }
            sb.AppendLine($"tick_over_budget={TickOverBudgetCount}");
            sb.AppendLine($"disconnects={DisconnectCount}");
            sb.AppendLine($"exceptions={ErrorCount}");
            return sb.ToString();
        }
    }
}
