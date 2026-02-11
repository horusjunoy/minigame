using System;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using MirrorTransport = Mirror.Transport;
using UnityEngine;

namespace Game.Network.Transport.Mirror
{
    public sealed class MirrorNetworkFacade : MonoBehaviour, INetworkFacade
    {
        [SerializeField] private NetworkManager networkManager;

        private MirrorNetworkServer _server;
        private MirrorNetworkClient _client;
        private INetworkTransport _transport;

        public INetworkServer Server => _server;
        public INetworkClient Client => _client;
        public INetworkTransport Transport => _transport;

        private void Awake()
        {
            if (networkManager == null)
            {
                networkManager = FindObjectOfType<NetworkManager>();
            }

            if (networkManager == null)
            {
                var managerObject = new GameObject("NetworkManager");
                var transport = EnsureTransport(managerObject, null);
                networkManager = managerObject.AddComponent<NetworkManager>();
                if (transport != null)
                {
                    networkManager.transport = transport;
                }
            }
            else
            {
                EnsureTransport(networkManager.gameObject, networkManager);
            }

            _server = new MirrorNetworkServer(networkManager);
            _client = new MirrorNetworkClient(networkManager);
            _transport = new MirrorTransportAdapter(MirrorTransport.active);
        }

        private void OnEnable()
        {
            _server?.RegisterHandlers();
            _client?.RegisterHandlers();
        }

        private void OnDisable()
        {
            _server?.UnregisterHandlers();
            _client?.UnregisterHandlers();
        }

        private static MirrorTransport EnsureTransport(GameObject managerObject, NetworkManager manager)
        {
            if (MirrorTransport.active != null)
            {
                if (manager != null && manager.transport == null)
                {
                    manager.transport = MirrorTransport.active;
                }

                return MirrorTransport.active;
            }

            var transport = managerObject.GetComponent<MirrorTransport>();
            if (transport == null)
            {
                var telepathyType = Type.GetType("Mirror.TelepathyTransport, Mirror.Transports");
                if (telepathyType == null)
                {
                    telepathyType = Type.GetType("Mirror.TelepathyTransport, Mirror.Transport.Telepathy");
                }
                if (telepathyType == null)
                {
                    telepathyType = Type.GetType("Mirror.TelepathyTransport, Mirror");
                }

                if (telepathyType != null && typeof(MirrorTransport).IsAssignableFrom(telepathyType))
                {
                    transport = (MirrorTransport)managerObject.AddComponent(telepathyType);
                }
            }

            if (transport != null)
            {
                MirrorTransport.active = transport;
                if (manager != null)
                {
                    manager.transport = transport;
                }
            }

            return transport;
        }
    }

    internal sealed class MirrorNetworkServer : INetworkServer
    {
        private readonly NetworkManager _networkManager;
        private readonly Dictionary<string, NetworkConnectionToClient> _connections = new Dictionary<string, NetworkConnectionToClient>();

        public event Action<NetworkPeerId, HelloMessage> HelloReceived;
        public event Action<NetworkPeerId> ClientDisconnected;
        public event Action<NetworkPeerId, MoveCommand> MoveCommandReceived;

        public MirrorNetworkServer(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public void RegisterHandlers()
        {
            NetworkServer.RegisterHandler<HelloNetworkMessage>(OnHelloMessage, false);
            NetworkServer.RegisterHandler<PingNetworkMessage>(OnPingMessage, false);
            NetworkServer.RegisterHandler<MoveCommandNetworkMessage>(OnMoveCommandMessage, false);
            NetworkServer.OnDisconnectedEvent += OnDisconnected;
        }

        public void UnregisterHandlers()
        {
            NetworkServer.UnregisterHandler<HelloNetworkMessage>();
            NetworkServer.UnregisterHandler<PingNetworkMessage>();
            NetworkServer.UnregisterHandler<MoveCommandNetworkMessage>();
            NetworkServer.OnDisconnectedEvent -= OnDisconnected;
        }

        public void StartServer(NetworkEndpoint endpoint)
        {
            ApplyEndpoint(endpoint);
            _networkManager.StartServer();
        }

        public void StopServer()
        {
            _networkManager.StopServer();
        }

        public void Disconnect(NetworkPeerId peerId, string reason)
        {
            if (!_connections.TryGetValue(peerId.Value, out var connection))
            {
                return;
            }

#if UNITY_EDITOR
            Debug.Log($"MirrorNetworkServer: disconnect {peerId.Value} reason={reason}");
#endif
            connection.Disconnect();
        }

        public void SendError(NetworkPeerId peerId, ServerErrorMessage message)
        {
            if (!_connections.TryGetValue(peerId.Value, out var connection))
            {
                return;
            }

            connection.Send(new ServerErrorNetworkMessage(message));
        }

        public void SendWelcome(NetworkPeerId peerId, WelcomeMessage message)
        {
            if (!_connections.TryGetValue(peerId.Value, out var connection))
            {
                return;
            }

#if UNITY_EDITOR
            Debug.Log("MirrorNetworkServer: sending welcome");
#endif
            connection.Send(new WelcomeNetworkMessage(message));
        }

        public void BroadcastPlayerJoined(PlayerJoinedMessage message)
        {
            NetworkServer.SendToAll(new PlayerJoinedNetworkMessage(message));
        }

        public void BroadcastPlayerLeft(PlayerLeftMessage message)
        {
            NetworkServer.SendToAll(new PlayerLeftNetworkMessage(message));
        }

        public void BroadcastSnapshot(SnapshotV1 snapshot)
        {
            NetworkServer.SendToAll(new SnapshotNetworkMessage(snapshot));
        }

        private void OnHelloMessage(NetworkConnectionToClient connection, HelloNetworkMessage message)
        {
            var peerId = new NetworkPeerId(connection.connectionId.ToString());
            _connections[peerId.Value] = connection;
#if UNITY_EDITOR
            Debug.Log("MirrorNetworkServer: hello received");
#endif
            HelloReceived?.Invoke(peerId, message.ToDto());
        }

        private void OnPingMessage(NetworkConnectionToClient connection, PingNetworkMessage message)
        {
            var peerId = new NetworkPeerId(connection.connectionId.ToString());
            _connections[peerId.Value] = connection;

            connection.Send(new PongNetworkMessage(new PongMessage(message.client_time, DateTime.UtcNow.ToString("o"))));
        }

        private void OnMoveCommandMessage(NetworkConnectionToClient connection, MoveCommandNetworkMessage message)
        {
            var peerId = new NetworkPeerId(connection.connectionId.ToString());
            _connections[peerId.Value] = connection;
            MoveCommandReceived?.Invoke(peerId, message.ToDto());
        }

        private void OnDisconnected(NetworkConnectionToClient connection)
        {
            var peerId = new NetworkPeerId(connection.connectionId.ToString());
            ClientDisconnected?.Invoke(peerId);
        }

        private static void ApplyEndpoint(NetworkEndpoint endpoint)
        {
            TrySetPort(endpoint.Port);
        }

        private static void TrySetPort(ushort port)
        {
            var transport = MirrorTransport.active;
            if (transport == null)
            {
                return;
            }

            var type = transport.GetType();
            var portField = type.GetField("port", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (portField != null)
            {
                if (portField.FieldType == typeof(ushort))
                {
                    portField.SetValue(transport, port);
                }
                else if (portField.FieldType == typeof(int))
                {
                    portField.SetValue(transport, (int)port);
                }
            }

            var portProperty = type.GetProperty("Port", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (portProperty != null && portProperty.CanWrite)
            {
                if (portProperty.PropertyType == typeof(ushort))
                {
                    portProperty.SetValue(transport, port);
                }
                else if (portProperty.PropertyType == typeof(int))
                {
                    portProperty.SetValue(transport, (int)port);
                }
            }
        }
    }

    internal sealed class MirrorNetworkClient : INetworkClient
    {
        private readonly NetworkManager _networkManager;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<WelcomeMessage> WelcomeReceived;
        public event Action<ServerErrorMessage> ErrorReceived;
        public event Action<PongMessage> PongReceived;
        public event Action<SnapshotV1> SnapshotReceived;

        public MirrorNetworkClient(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public void RegisterHandlers()
        {
            NetworkClient.RegisterHandler<WelcomeNetworkMessage>(OnWelcomeMessage, false);
            NetworkClient.RegisterHandler<ServerErrorNetworkMessage>(OnServerErrorMessage, false);
            NetworkClient.RegisterHandler<PongNetworkMessage>(OnPongMessage, false);
            NetworkClient.RegisterHandler<SnapshotNetworkMessage>(OnSnapshotMessage, false);
            NetworkClient.OnConnectedEvent += OnConnected;
            NetworkClient.OnDisconnectedEvent += OnDisconnected;
        }

        public void UnregisterHandlers()
        {
            NetworkClient.UnregisterHandler<WelcomeNetworkMessage>();
            NetworkClient.UnregisterHandler<ServerErrorNetworkMessage>();
            NetworkClient.UnregisterHandler<PongNetworkMessage>();
            NetworkClient.UnregisterHandler<SnapshotNetworkMessage>();
            NetworkClient.OnConnectedEvent -= OnConnected;
            NetworkClient.OnDisconnectedEvent -= OnDisconnected;
        }

        public void StartClient(NetworkEndpoint endpoint)
        {
            _networkManager.networkAddress = endpoint.Address;
            TrySetPort(endpoint.Port);
            _networkManager.StartClient();
        }

        public void StopClient()
        {
            _networkManager.StopClient();
        }

        public void SendHello(HelloMessage message)
        {
#if UNITY_EDITOR
            Debug.Log("MirrorNetworkClient: sending hello");
#endif
            NetworkClient.Send(new HelloNetworkMessage(message));
        }

        public void SendPing(PingMessage message)
        {
            NetworkClient.Send(new PingNetworkMessage(message));
        }

        public void SendMoveCommand(MoveCommand command)
        {
            NetworkClient.Send(new MoveCommandNetworkMessage(command));
        }

        private void OnWelcomeMessage(WelcomeNetworkMessage message)
        {
#if UNITY_EDITOR
            Debug.Log("MirrorNetworkClient: welcome received");
#endif
            WelcomeReceived?.Invoke(message.ToDto());
        }

        private void OnServerErrorMessage(ServerErrorNetworkMessage message)
        {
            ErrorReceived?.Invoke(message.ToDto());
        }

        private void OnPongMessage(PongNetworkMessage message)
        {
            PongReceived?.Invoke(message.ToDto());
        }

        private void OnSnapshotMessage(SnapshotNetworkMessage message)
        {
            SnapshotReceived?.Invoke(message.ToDto());
        }

        private void OnConnected()
        {
#if UNITY_EDITOR
            Debug.Log("MirrorNetworkClient: connected");
#endif
            Connected?.Invoke();
        }

        private void OnDisconnected()
        {
            Disconnected?.Invoke();
        }

        private static void TrySetPort(ushort port)
        {
            var transport = MirrorTransport.active;
            if (transport == null)
            {
                return;
            }

            var type = transport.GetType();
            var portField = type.GetField("port", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (portField != null)
            {
                if (portField.FieldType == typeof(ushort))
                {
                    portField.SetValue(transport, port);
                }
                else if (portField.FieldType == typeof(int))
                {
                    portField.SetValue(transport, (int)port);
                }
            }

            var portProperty = type.GetProperty("Port", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (portProperty != null && portProperty.CanWrite)
            {
                if (portProperty.PropertyType == typeof(ushort))
                {
                    portProperty.SetValue(transport, port);
                }
                else if (portProperty.PropertyType == typeof(int))
                {
                    portProperty.SetValue(transport, (int)port);
                }
            }
        }
    }

    internal struct HelloNetworkMessage : NetworkMessage
    {
        public int v;
        public string session_id;
        public string client_version;
        public string join_token;

        public HelloNetworkMessage(HelloMessage message)
        {
            v = message.v;
            session_id = message.session_id;
            client_version = message.client_version;
            join_token = message.join_token;
        }

        public HelloMessage ToDto() => new HelloMessage(session_id, client_version, join_token, v);
    }

    internal struct WelcomeNetworkMessage : NetworkMessage
    {
        public int v;
        public string match_id;
        public string player_id;
        public string server_time;

        public WelcomeNetworkMessage(WelcomeMessage message)
        {
            v = message.v;
            match_id = message.match_id;
            player_id = message.player_id;
            server_time = message.server_time;
        }

        public WelcomeMessage ToDto() => new WelcomeMessage(match_id, player_id, server_time, v);
    }

    internal struct ServerErrorNetworkMessage : NetworkMessage
    {
        public int v;
        public string code;
        public string detail;
        public string server_build_version;
        public string client_build_version;
        public int server_protocol_version;
        public int client_protocol_version;

        public ServerErrorNetworkMessage(ServerErrorMessage message)
        {
            v = message.v;
            code = message.code;
            detail = message.detail;
            server_build_version = message.server_build_version;
            client_build_version = message.client_build_version;
            server_protocol_version = message.server_protocol_version;
            client_protocol_version = message.client_protocol_version;
        }

        public ServerErrorMessage ToDto()
            => new ServerErrorMessage(code, detail, server_build_version, client_build_version, server_protocol_version, client_protocol_version, v);
    }

    internal sealed class MirrorTransportAdapter : INetworkTransport
    {
        private readonly MirrorTransport _transport;

        public MirrorTransportAdapter(MirrorTransport transport)
        {
            _transport = transport;
        }

        public string Name => _transport != null ? _transport.GetType().Name : "Mirror";
        public bool IsAvailable => _transport != null;
    }

    internal struct PlayerJoinedNetworkMessage : NetworkMessage
    {
        public int v;
        public string match_id;
        public string player_id;
        public string session_id;

        public PlayerJoinedNetworkMessage(PlayerJoinedMessage message)
        {
            v = message.v;
            match_id = message.match_id;
            player_id = message.player_id;
            session_id = message.session_id;
        }

        public PlayerJoinedMessage ToDto() => new PlayerJoinedMessage(match_id, player_id, session_id, v);
    }

    internal struct PlayerLeftNetworkMessage : NetworkMessage
    {
        public int v;
        public string match_id;
        public string player_id;
        public string session_id;
        public string reason;

        public PlayerLeftNetworkMessage(PlayerLeftMessage message)
        {
            v = message.v;
            match_id = message.match_id;
            player_id = message.player_id;
            session_id = message.session_id;
            reason = message.reason;
        }

        public PlayerLeftMessage ToDto() => new PlayerLeftMessage(match_id, player_id, session_id, reason, v);
    }

    internal struct PingNetworkMessage : NetworkMessage
    {
        public int v;
        public string client_time;

        public PingNetworkMessage(PingMessage message)
        {
            v = message.v;
            client_time = message.client_time;
        }
    }

    internal struct PongNetworkMessage : NetworkMessage
    {
        public int v;
        public string client_time;
        public string server_time;

        public PongNetworkMessage(PongMessage message)
        {
            v = message.v;
            client_time = message.client_time;
            server_time = message.server_time;
        }

        public PongMessage ToDto() => new PongMessage(client_time, server_time, v);
    }

    internal struct MoveCommandNetworkMessage : NetworkMessage
    {
        public int v;
        public float input_x;
        public float input_y;
        public string client_time;

        public MoveCommandNetworkMessage(MoveCommand command)
        {
            v = command.v;
            input_x = command.input_x;
            input_y = command.input_y;
            client_time = command.client_time;
        }

        public MoveCommand ToDto() => new MoveCommand(input_x, input_y, client_time, v);
    }

    internal struct SnapshotNetworkMessage : NetworkMessage
    {
        public int v;
        public long server_time_ms;
        public SnapshotEntityV1[] entities;

        public SnapshotNetworkMessage(SnapshotV1 snapshot)
        {
            v = snapshot.v;
            server_time_ms = snapshot.server_time_ms;
            entities = snapshot.entities;
        }

        public SnapshotV1 ToDto() => new SnapshotV1(server_time_ms, entities, v);
    }
}
