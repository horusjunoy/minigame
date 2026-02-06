namespace Game.Core
{
    public readonly struct PlayerRef
    {
        public readonly PlayerId Id;
        public PlayerRef(PlayerId id) => Id = id;
        public override string ToString() => Id.ToString();
    }
}
