using System.Collections.Generic;
using Game.Core;

namespace Game.Runtime
{
    public static class EconomyEventPublisher
    {
        public static void LogPurchaseIntent(IRuntimeLogger logger, TelemetryContext telemetry, string sku, string source)
        {
            if (!EconomyFeatureFlags.IsEnabled() || logger == null)
            {
                return;
            }

            var fields = new Dictionary<string, object>
            {
                ["sku"] = sku ?? "unknown",
                ["source"] = source ?? "unknown"
            };
            logger.Log(LogLevel.Info, "purchase_intent", "Purchase intent", fields, telemetry);
        }

        public static void LogRewardGranted(IRuntimeLogger logger, TelemetryContext telemetry, string rewardId, int amount, string playerId)
        {
            if (!EconomyFeatureFlags.IsEnabled() || logger == null)
            {
                return;
            }

            var fields = new Dictionary<string, object>
            {
                ["reward_id"] = rewardId ?? "unknown",
                ["amount"] = amount,
                ["player_id"] = playerId ?? string.Empty
            };
            logger.Log(LogLevel.Info, "reward_granted", "Reward granted", fields, telemetry);
        }
    }
}
