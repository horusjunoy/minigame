namespace Game.Core
{
    public readonly struct TelemetryContext
    {
        public readonly MatchId MatchId;
        public readonly MinigameId MinigameId;
        public readonly PlayerId PlayerId;
        public readonly SessionId SessionId;
        public readonly string BuildVersion;
        public readonly string ServerInstanceId;

        public TelemetryContext(
            MatchId matchId,
            MinigameId minigameId,
            PlayerId playerId,
            SessionId sessionId,
            string buildVersion,
            string serverInstanceId)
        {
            MatchId = matchId;
            MinigameId = minigameId;
            PlayerId = playerId;
            SessionId = sessionId;
            BuildVersion = buildVersion;
            ServerInstanceId = serverInstanceId;
        }
    }
}
