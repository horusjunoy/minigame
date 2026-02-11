namespace Game.Network
{
    public readonly struct NetworkEndpoint
    {
        public readonly string Address;
        public readonly ushort Port;

        public NetworkEndpoint(string address, ushort port)
        {
            Address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address;
            Port = port;
        }

        public static NetworkEndpoint Localhost(ushort port = 7770) => new NetworkEndpoint("127.0.0.1", port);

        public override string ToString() => $"{Address}:{Port}";
    }

    public readonly struct NetworkPeerId
    {
        public readonly string Value;
        public NetworkPeerId(string value) => Value = value ?? string.Empty;
        public override string ToString() => Value;
    }
}
