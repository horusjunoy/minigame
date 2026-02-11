using System;

namespace Game.Network
{
    public interface INetworkFacade
    {
        INetworkServer Server { get; }
        INetworkClient Client { get; }
    }

    public interface INetworkServer
    {
        event Action<NetworkPeerId, HelloMessage> HelloReceived;
        event Action<NetworkPeerId> ClientDisconnected;
        event Action<NetworkPeerId, MoveCommand> MoveCommandReceived;

        void StartServer(NetworkEndpoint endpoint);
        void StopServer();
        void Disconnect(NetworkPeerId peerId, string reason);

        void SendWelcome(NetworkPeerId peerId, WelcomeMessage message);
        void BroadcastPlayerJoined(PlayerJoinedMessage message);
        void BroadcastPlayerLeft(PlayerLeftMessage message);
        void BroadcastSnapshot(SnapshotV1 snapshot);
    }

    public interface INetworkClient
    {
        event Action Connected;
        event Action Disconnected;
        event Action<WelcomeMessage> WelcomeReceived;
        event Action<PongMessage> PongReceived;
        event Action<SnapshotV1> SnapshotReceived;

        void StartClient(NetworkEndpoint endpoint);
        void StopClient();

        void SendHello(HelloMessage message);
        void SendPing(PingMessage message);
        void SendMoveCommand(MoveCommand command);
    }
}
