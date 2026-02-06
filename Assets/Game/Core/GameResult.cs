namespace Game.Core
{
    public enum EndGameReason
    {
        Completed,
        Timeout,
        Aborted,
        Error
    }

    public readonly struct GameResult
    {
        public readonly EndGameReason Reason;
        public GameResult(EndGameReason reason) => Reason = reason;
    }
}
