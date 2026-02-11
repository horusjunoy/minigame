using System.Collections;
using System.Globalization;
using System.Text;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    public sealed class JsonRuntimeLogger : IRuntimeLogger
    {
        public void Log(LogLevel level, string eventName, string message, object fields = null, TelemetryContext? context = null)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            AppendField(sb, "ts", System.DateTime.UtcNow.ToString("o"));
            AppendField(sb, "lvl", level.ToString().ToUpperInvariant());
            AppendField(sb, "event", eventName);
            AppendField(sb, "msg", message);

            if (context.HasValue)
            {
                var ctx = context.Value;
                AppendField(sb, "match_id", ctx.MatchId.ToString());
                AppendField(sb, "minigame_id", ctx.MinigameId.ToString());
                AppendField(sb, "player_id", ctx.PlayerId.ToString());
                AppendField(sb, "session_id", ctx.SessionId.ToString());
                AppendField(sb, "build_version", ctx.BuildVersion);
                AppendField(sb, "server_instance_id", ctx.ServerInstanceId);
            }

            if (fields != null)
            {
                AppendField(sb, "fields", fields);
            }

            if (sb[sb.Length - 1] == ',')
            {
                sb.Length -= 1;
            }

            sb.Append('}');
            Debug.Log(sb.ToString());
        }

        private static void AppendField(StringBuilder sb, string key, object value)
        {
            if (value == null)
            {
                return;
            }

            sb.Append('\"').Append(Escape(key)).Append('\"').Append(':');
            AppendValue(sb, value);
            sb.Append(',');
        }

        private static string Escape(string input) => input.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static void AppendValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            switch (value)
            {
                case string str:
                    sb.Append('\"').Append(Escape(str)).Append('\"');
                    return;
                case bool boolean:
                    sb.Append(boolean ? "true" : "false");
                    return;
                case int intValue:
                    sb.Append(intValue.ToString(CultureInfo.InvariantCulture));
                    return;
                case long longValue:
                    sb.Append(longValue.ToString(CultureInfo.InvariantCulture));
                    return;
                case float floatValue:
                    sb.Append(floatValue.ToString("0.###", CultureInfo.InvariantCulture));
                    return;
                case double doubleValue:
                    sb.Append(doubleValue.ToString("0.###", CultureInfo.InvariantCulture));
                    return;
                case decimal decimalValue:
                    sb.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                    return;
                case IDictionary dict:
                    AppendDictionary(sb, dict);
                    return;
                default:
                    sb.Append('\"').Append(Escape(value.ToString())).Append('\"');
                    return;
            }
        }

        private static void AppendDictionary(StringBuilder sb, IDictionary dict)
        {
            sb.Append('{');
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                sb.Append('\"').Append(Escape(entry.Key.ToString())).Append('\"').Append(':');
                AppendValue(sb, entry.Value);
                sb.Append(',');
            }

            if (sb[sb.Length - 1] == ',')
            {
                sb.Length -= 1;
            }
            sb.Append('}');
        }
    }
}
