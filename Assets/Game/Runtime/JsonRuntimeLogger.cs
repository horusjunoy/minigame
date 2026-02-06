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
                AppendField(sb, "fields", fields.ToString());
            }

            if (sb[sb.Length - 1] == ',')
            {
                sb.Length -= 1;
            }

            sb.Append('}');
            Debug.Log(sb.ToString());
        }

        private static void AppendField(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            sb.Append('\"').Append(Escape(key)).Append('\"').Append(':');
            sb.Append('\"').Append(Escape(value)).Append('\"').Append(',');
        }

        private static string Escape(string input) => input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
