namespace Game.Network
{
    public struct HelloMessage
    {
        public const int Version = 1;
        public int v;
        public string session_id;
        public string client_version;
        public string join_token;

        public HelloMessage(string sessionId, string clientVersion = "", string joinToken = "", int version = Version)
        {
            v = version;
            session_id = sessionId ?? string.Empty;
            client_version = clientVersion ?? string.Empty;
            join_token = joinToken ?? string.Empty;
        }
    }

    public struct WelcomeMessage
    {
        public const int Version = 1;
        public int v;
        public string match_id;
        public string player_id;
        public string server_time;

        public WelcomeMessage(string matchId, string playerId, string serverTime, int version = Version)
        {
            v = version;
            match_id = matchId ?? string.Empty;
            player_id = playerId ?? string.Empty;
            server_time = serverTime ?? string.Empty;
        }
    }

    public struct PlayerJoinedMessage
    {
        public const int Version = 1;
        public int v;
        public string match_id;
        public string player_id;
        public string session_id;

        public PlayerJoinedMessage(string matchId, string playerId, string sessionId, int version = Version)
        {
            v = version;
            match_id = matchId ?? string.Empty;
            player_id = playerId ?? string.Empty;
            session_id = sessionId ?? string.Empty;
        }
    }

    public struct PlayerLeftMessage
    {
        public const int Version = 1;
        public int v;
        public string match_id;
        public string player_id;
        public string session_id;
        public string reason;

        public PlayerLeftMessage(string matchId, string playerId, string sessionId, string reason, int version = Version)
        {
            v = version;
            match_id = matchId ?? string.Empty;
            player_id = playerId ?? string.Empty;
            session_id = sessionId ?? string.Empty;
            this.reason = reason ?? string.Empty;
        }
    }

    public struct PingMessage
    {
        public const int Version = 1;
        public int v;
        public string client_time;

        public PingMessage(string clientTime, int version = Version)
        {
            v = version;
            client_time = clientTime ?? string.Empty;
        }
    }

    public struct PongMessage
    {
        public const int Version = 1;
        public int v;
        public string client_time;
        public string server_time;

        public PongMessage(string clientTime, string serverTime, int version = Version)
        {
            v = version;
            client_time = clientTime ?? string.Empty;
            server_time = serverTime ?? string.Empty;
        }
    }

    public struct MoveCommand
    {
        public const int Version = 1;
        public int v;
        public float input_x;
        public float input_y;
        public string client_time;

        public MoveCommand(float inputX, float inputY, string clientTime, int version = Version)
        {
            v = version;
            input_x = inputX;
            input_y = inputY;
            client_time = clientTime ?? string.Empty;
        }
    }
}
