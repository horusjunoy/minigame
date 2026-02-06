namespace Game.Core
{
    public readonly struct MatchId
    {
        public readonly string Value;
        public MatchId(string value) => Value = value;
        public override string ToString() => Value;
    }

    public readonly struct PlayerId
    {
        public readonly string Value;
        public PlayerId(string value) => Value = value;
        public override string ToString() => Value;
    }

    public readonly struct SessionId
    {
        public readonly string Value;
        public SessionId(string value) => Value = value;
        public override string ToString() => Value;
    }

    public readonly struct MinigameId
    {
        public readonly string Value;
        public MinigameId(string value) => Value = value;
        public override string ToString() => Value;
    }
}
