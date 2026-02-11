using System;

namespace Game.Runtime
{
    public static class EconomyFeatureFlags
    {
        private const string EnabledKey = "ECONOMY_ENABLED";

        public static bool IsEnabled()
        {
            var value = Environment.GetEnvironmentVariable(EnabledKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
