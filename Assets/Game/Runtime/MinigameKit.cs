using System.Collections.Generic;
using Game.Core;

namespace Game.Runtime
{
    public static class MinigameKit
    {
        public static class Events
        {
            public const string RoundStart = "round_start";
            public const string RoundEnd = "round_end";
            public const string Countdown = "countdown_tick";
            public const string ScoreboardUpdated = "scoreboard_update";
        }

        public static void BroadcastRoundStart(IMinigameContext context, int roundIndex)
        {
            if (context == null)
            {
                return;
            }

            context.Broadcast(Events.RoundStart, new RoundEventPayload(roundIndex));
        }

        public static void BroadcastRoundEnd(IMinigameContext context, int roundIndex, EndGameReason reason)
        {
            if (context == null)
            {
                return;
            }

            context.Broadcast(Events.RoundEnd, new RoundEndPayload(roundIndex, reason.ToString()));
        }

        public static void BroadcastCountdown(IMinigameContext context, int secondsLeft)
        {
            if (context == null)
            {
                return;
            }

            context.Broadcast(Events.Countdown, new CountdownPayload(secondsLeft));
        }

        public static void BroadcastScoreboard(IMinigameContext context)
        {
            if (context == null)
            {
                return;
            }

            var snapshot = context.GetScoreboard().Snapshot();
            var entries = new List<ScoreboardEntry>(snapshot.Count);
            foreach (var entry in snapshot)
            {
                entries.Add(new ScoreboardEntry(entry.Key.Value, entry.Value));
            }

            context.Broadcast(Events.ScoreboardUpdated, new ScoreboardPayload(entries.ToArray()));
        }

        public readonly struct RoundEventPayload
        {
            public readonly int round;
            public RoundEventPayload(int roundIndex) => round = roundIndex;
        }

        public readonly struct RoundEndPayload
        {
            public readonly int round;
            public readonly string reason;
            public RoundEndPayload(int roundIndex, string reasonValue)
            {
                round = roundIndex;
                reason = reasonValue ?? string.Empty;
            }
        }

        public readonly struct CountdownPayload
        {
            public readonly int seconds_left;
            public CountdownPayload(int secondsLeft) => seconds_left = secondsLeft;
        }

        public readonly struct ScoreboardPayload
        {
            public readonly ScoreboardEntry[] entries;
            public ScoreboardPayload(ScoreboardEntry[] entries) => this.entries = entries ?? new ScoreboardEntry[0];
        }

        public readonly struct ScoreboardEntry
        {
            public readonly string player_id;
            public readonly int score;

            public ScoreboardEntry(string playerId, int scoreValue)
            {
                player_id = playerId ?? string.Empty;
                score = scoreValue;
            }
        }
    }
}
