using System;
using Game.Core;
using Game.Network;
using Game.Runtime;
using UnityEngine;

namespace Game.Client
{
    public sealed class ClientNetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour facadeBehaviour;
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private ushort serverPort = 7770;
        [SerializeField] private string sessionId = "";
        [SerializeField] private string joinToken = "";
        [SerializeField] private int protocolVersion = NetworkProtocol.Version;
        [SerializeField] private string contentVersion = "";
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool autoSendMoveCommands = true;
        [SerializeField] private float moveCommandRate = 10f;

        private INetworkClient _client;
        private JsonRuntimeLogger _logger;
        private ClientSnapshotInterpolator _snapshotInterpolator;
        private float _nextMoveCommandTime;
        private bool _isConnected;
        private float _lastSnapshotTime;
        private float _lastPongTime;
        private string _disconnectReason = "unknown";

        public bool IsConnected => _isConnected;
        public event Action Connected;
        public event Action Disconnected;
        public event Action<WelcomeMessage> WelcomeReceived;
        public event Action<ServerErrorMessage> ErrorReceived;

        private void Awake()
        {
            _logger = new JsonRuntimeLogger();
            _snapshotInterpolator = new ClientSnapshotInterpolator();
            EnsureInitialized(false);
        }

        private void Start()
        {
            if (!EnsureInitialized(true))
            {
                enabled = false;
                return;
            }

            if (autoStart)
            {
                StartClient();
            }
        }

        private void Update()
        {
            _snapshotInterpolator?.Update(Time.realtimeSinceStartup);
            if (autoSendMoveCommands)
            {
                TrySendMoveCommand();
            }
        }

        public void StartClient()
        {
            if (!EnsureInitialized(true))
            {
                return;
            }

            _client.StartClient(new NetworkEndpoint(serverAddress, serverPort));
        }

        public void StopClient()
        {
            _disconnectReason = "client_stop";
            _client?.StopClient();
        }

        public void SetJoinToken(string value)
        {
            joinToken = value ?? string.Empty;
        }

        public void SetVersionInfo(int protocol, string content, int schema)
        {
            protocolVersion = protocol;
            contentVersion = content ?? string.Empty;
            schemaVersion = schema;
        }

        public void ConfigureEndpoint(string address, ushort port)
        {
            serverAddress = address;
            serverPort = port;
        }

        public void SetAutoStart(bool value)
        {
            autoStart = value;
        }

        private bool EnsureInitialized(bool logErrors)
        {
            if (_client != null)
            {
                return true;
            }

            var facade = ResolveFacade(logErrors);
            if (facade == null)
            {
                return false;
            }

            var resolvedClient = facade.Client;
            if (resolvedClient == null)
            {
                return false;
            }

            _client = resolvedClient;
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            _client.WelcomeReceived += OnWelcomeReceived;
            _client.ErrorReceived += OnErrorReceived;
            _client.SnapshotReceived += OnSnapshotReceived;
            _client.PongReceived += OnPongReceived;
            return true;
        }

        private INetworkFacade ResolveFacade(bool logErrors)
        {
            if (facadeBehaviour == null)
            {
                var componentFacade = GetComponent<INetworkFacade>();
                if (componentFacade != null)
                {
                    return componentFacade;
                }

                if (logErrors)
                {
                    Debug.LogError("ClientNetworkBootstrap: facadeBehaviour not set.");
                }

                return null;
            }

            if (facadeBehaviour is INetworkFacade facade)
            {
                return facade;
            }

            if (logErrors)
            {
                Debug.LogError("ClientNetworkBootstrap: facadeBehaviour does not implement INetworkFacade.");
            }

            return null;
        }

        private void OnConnected()
        {
            _isConnected = true;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
            }

            var hello = new HelloMessage(
                sessionId,
                BuildInfo.BuildVersion,
                protocolVersion,
                contentVersion,
                schemaVersion,
                joinToken);
            _client.SendHello(hello);

            var telemetry = new TelemetryContext(
                new MatchId(""),
                new MinigameId(""),
                new PlayerId(""),
                new SessionId(sessionId),
                BuildInfo.BuildVersion,
                "client_local");

            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["client_protocol_version"] = protocolVersion,
                ["client_content_version"] = contentVersion,
                ["client_schema_version"] = schemaVersion
            };
            _logger.Log(LogLevel.Info, "client_connected", "Client connected", fields, telemetry);
            _disconnectReason = "transport_disconnect";
            _nextMoveCommandTime = Time.realtimeSinceStartup;
            Connected?.Invoke();
        }

        private void OnDisconnected()
        {
            _isConnected = false;
            var telemetry = new TelemetryContext(
                new MatchId(""),
                new MinigameId(""),
                new PlayerId(""),
                new SessionId(sessionId ?? string.Empty),
                BuildInfo.BuildVersion,
                "client_local");

            var now = Time.realtimeSinceStartup;
            var snapshotAge = _lastSnapshotTime > 0f ? Mathf.Max(0f, now - _lastSnapshotTime) : -1f;
            var pongAge = _lastPongTime > 0f ? Mathf.Max(0f, now - _lastPongTime) : -1f;
            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["reason"] = _disconnectReason ?? "transport_disconnect",
                ["last_snapshot_age_s"] = snapshotAge < 0f ? null : Math.Round(snapshotAge, 2),
                ["last_pong_age_s"] = pongAge < 0f ? null : Math.Round(pongAge, 2)
            };
            _logger.Log(LogLevel.Info, "client_disconnected", "Client disconnected", fields, telemetry);
            Disconnected?.Invoke();
        }

        private void OnWelcomeReceived(WelcomeMessage message)
        {
            var telemetry = new TelemetryContext(
                new MatchId(message.match_id),
                new MinigameId(""),
                new PlayerId(message.player_id),
                new SessionId(sessionId ?? string.Empty),
                BuildInfo.BuildVersion,
                "client_local");

            _logger.Log(LogLevel.Info, "welcome_received", "Welcome received", null, telemetry);
            WelcomeReceived?.Invoke(message);
        }

        private void OnErrorReceived(ServerErrorMessage message)
        {
            _disconnectReason = string.IsNullOrWhiteSpace(message.code) ? "server_error" : message.code;
            var telemetry = new TelemetryContext(
                new MatchId(""),
                new MinigameId(""),
                new PlayerId(""),
                new SessionId(sessionId ?? string.Empty),
                BuildInfo.BuildVersion,
                "client_local");

            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["code"] = message.code,
                ["detail"] = message.detail,
                ["server_build_version"] = message.server_build_version,
                ["client_build_version"] = message.client_build_version,
                ["server_protocol_version"] = message.server_protocol_version,
                ["client_protocol_version"] = message.client_protocol_version,
                ["client_content_version"] = message.client_content_version,
                ["client_schema_version"] = message.client_schema_version,
                ["accepted_build_versions"] = message.accepted_build_versions,
                ["accepted_content_versions"] = message.accepted_content_versions,
                ["accepted_schema_versions"] = message.accepted_schema_versions,
                ["accepted_protocol_versions"] = message.accepted_protocol_versions
            };
            _logger.Log(LogLevel.Warn, "server_error", "Server error", fields, telemetry);
            ErrorReceived?.Invoke(message);
        }

        private void OnSnapshotReceived(SnapshotV1 snapshot)
        {
            _lastSnapshotTime = Time.realtimeSinceStartup;
            _snapshotInterpolator?.AddSnapshot(snapshot);
        }

        private void OnPongReceived(PongMessage message)
        {
            _lastPongTime = Time.realtimeSinceStartup;
        }

        private void TrySendMoveCommand()
        {
            if (_client == null)
            {
                return;
            }

            if (!_isConnected)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (moveCommandRate <= 0f)
            {
                return;
            }

            if (now < _nextMoveCommandTime)
            {
                return;
            }

            _nextMoveCommandTime = now + (1f / moveCommandRate);
            var input = new Vector2(Mathf.Sin(now), Mathf.Cos(now));
            var command = new MoveCommand(input.x, input.y, DateTime.UtcNow.ToString("o"));
            _client.SendMoveCommand(command);
        }
    }
}
