using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Network;
using Game.Runtime;
using UnityEngine;

namespace Game.Server
{
    public sealed class ServerNetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour facadeBehaviour;
        [SerializeField] private string matchId = "m_local";
        [SerializeField] private string minigameId = "stub_v1";
        [SerializeField] private string serverInstanceId = "server_local";
        [SerializeField] private string listenAddress = "127.0.0.1";
        [SerializeField] private ushort listenPort = 7770;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private string matchmakerSecret = "dev_secret_change_me";
        [SerializeField] private int joinTokenMaxLength = 2048;
        [SerializeField] private bool allowEmptyJoinToken = false;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float maxBounds = 10f;
        [SerializeField] private float minCommandInterval = 0.05f;
        [SerializeField] private int maxCommandsPerSecond = 20;

        private readonly Dictionary<string, string> _sessionByPlayer = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector3> _positionsByPlayer = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, float> _lastCommandTime = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _commandWindowStart = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _commandCount = new Dictionary<string, int>();
        private readonly HashSet<string> _usedJoinTokens = new HashSet<string>();
        private readonly Dictionary<string, string> _playerIdByPeer = new Dictionary<string, string>();
        private INetworkServer _server;
        private JsonRuntimeLogger _logger;
        private float _nextSnapshotTime;
        private float _nextBytesLogTime;
        private int _bytesThisSecond;
        private bool _serverRunning;
        private readonly Dictionary<string, int> _playerIndex = new Dictionary<string, int>();
        private MatchmakerTokenVerifier _tokenVerifier;

        private void Awake()
        {
            _logger = new JsonRuntimeLogger();
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
                StartServer();
            }
        }

        public void StartServer()
        {
            if (!EnsureInitialized(true))
            {
                return;
            }

            _server.StartServer(new NetworkEndpoint(listenAddress, listenPort));
            _serverRunning = true;
            _nextSnapshotTime = Time.realtimeSinceStartup;
            _nextBytesLogTime = Time.realtimeSinceStartup + 1f;
            _bytesThisSecond = 0;
        }

        public void StopServer()
        {
            _server?.StopServer();
            _serverRunning = false;
        }

        private bool EnsureInitialized(bool logErrors)
        {
            if (_server != null)
            {
                return true;
            }

            var facade = ResolveFacade(logErrors);
            if (facade == null)
            {
                return false;
            }

            var resolvedServer = facade.Server;
            if (resolvedServer == null)
            {
                return false;
            }

            _server = resolvedServer;
            _server.HelloReceived += OnHelloReceived;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.MoveCommandReceived += OnMoveCommandReceived;
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
                    Debug.LogError("ServerNetworkBootstrap: facadeBehaviour not set.");
                }

                return null;
            }

            if (facadeBehaviour is INetworkFacade facade)
            {
                return facade;
            }

            if (logErrors)
            {
                Debug.LogError("ServerNetworkBootstrap: facadeBehaviour does not implement INetworkFacade.");
            }

            return null;
        }

        private void OnHelloReceived(NetworkPeerId peerId, HelloMessage message)
        {
            var sessionId = SanitizePayload(message.session_id, 64, peerId, "session_id");
            var clientVersion = SanitizePayload(message.client_version, 32, peerId, "client_version");
            var joinToken = SanitizePayload(message.join_token, joinTokenMaxLength, peerId, "join_token");

            if (!TryAcceptJoinToken(peerId, sessionId, joinToken, out var tokenPayload))
            {
                return;
            }

            _sessionByPlayer[peerId.Value] = sessionId;
            var playerId = string.IsNullOrWhiteSpace(tokenPayload.player_id) ? peerId.Value : tokenPayload.player_id;
            _playerIdByPeer[peerId.Value] = playerId;
            if (!_playerIndex.ContainsKey(peerId.Value))
            {
                _playerIndex[peerId.Value] = _playerIndex.Count;
            }

            if (!_positionsByPlayer.ContainsKey(peerId.Value))
            {
                _positionsByPlayer[peerId.Value] = Vector3.zero;
            }

            var telemetry = new TelemetryContext(
                new MatchId(matchId),
                new MinigameId(minigameId),
                new PlayerId(playerId),
                new SessionId(sessionId),
                BuildInfo.BuildVersion,
                serverInstanceId);

            _logger.Log(LogLevel.Info, "player_joined", "Player joined", null, telemetry);

            if (clientVersion != message.client_version)
            {
                var fields = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["player_id"] = playerId
                };
                _logger.Log(LogLevel.Warn, "payload_clamped", "Client version clamped", fields, telemetry);
            }

            var welcome = new WelcomeMessage(matchId, playerId, DateTime.UtcNow.ToString("o"));
            _server.SendWelcome(peerId, welcome);
        }

        private void OnClientDisconnected(NetworkPeerId peerId)
        {
            _sessionByPlayer.TryGetValue(peerId.Value, out var sessionId);
            var telemetry = new TelemetryContext(
                new MatchId(matchId),
                new MinigameId(minigameId),
                new PlayerId(ResolvePlayerId(peerId.Value)),
                new SessionId(sessionId ?? string.Empty),
                BuildInfo.BuildVersion,
                serverInstanceId);

            var now = Time.realtimeSinceStartup;
            var lastCommandAge = _lastCommandTime.TryGetValue(peerId.Value, out var last)
                ? Mathf.Max(0f, now - last)
                : -1f;
            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["reason"] = "transport_disconnect",
                ["last_command_age_s"] = lastCommandAge < 0f ? null : Math.Round(lastCommandAge, 2)
            };
            _logger.Log(LogLevel.Info, "player_left", "Player left", fields, telemetry);

            _playerIndex.Remove(peerId.Value);
            _positionsByPlayer.Remove(peerId.Value);
            _lastCommandTime.Remove(peerId.Value);
            _commandWindowStart.Remove(peerId.Value);
            _commandCount.Remove(peerId.Value);
            _playerIdByPeer.Remove(peerId.Value);
        }

        private void OnMoveCommandReceived(NetworkPeerId peerId, MoveCommand command)
        {
            _ = SanitizePayload(command.client_time, 64, peerId, "client_time");
            var now = Time.realtimeSinceStartup;
            if (_lastCommandTime.TryGetValue(peerId.Value, out var lastTime))
            {
                var interval = now - lastTime;
                if (interval < minCommandInterval)
                {
                    LogMovementInvalid(peerId, "rate_limited");
                    return;
                }
            }

            if (!ValidateCommandRate(peerId.Value, now))
            {
                LogMovementInvalid(peerId, "rate_limit_window");
                return;
            }

            if (!_positionsByPlayer.TryGetValue(peerId.Value, out var position))
            {
                position = Vector3.zero;
            }

            if (float.IsNaN(command.input_x) || float.IsNaN(command.input_y) ||
                float.IsInfinity(command.input_x) || float.IsInfinity(command.input_y))
            {
                LogMovementInvalid(peerId, "input_nan");
                return;
            }

            var clampedX = Mathf.Clamp(command.input_x, -1f, 1f);
            var clampedY = Mathf.Clamp(command.input_y, -1f, 1f);
            if (!Mathf.Approximately(clampedX, command.input_x) || !Mathf.Approximately(clampedY, command.input_y))
            {
                LogMovementInvalid(peerId, "input_clamp");
            }

            var input = new Vector2(clampedX, clampedY);
            var deltaTime = Mathf.Max(0.01f, now - (_lastCommandTime.TryGetValue(peerId.Value, out var last) ? last : now - 0.1f));
            var maxDistance = maxSpeed * deltaTime;
            var desired = new Vector3(input.x, 0f, input.y);
            if (desired.magnitude > maxDistance)
            {
                desired = desired.normalized * maxDistance;
                LogMovementInvalid(peerId, "speed_clamp");
            }

            position += desired;
            position.x = Mathf.Clamp(position.x, -maxBounds, maxBounds);
            position.z = Mathf.Clamp(position.z, -maxBounds, maxBounds);

            _positionsByPlayer[peerId.Value] = position;
            _lastCommandTime[peerId.Value] = now;
        }

        private void Update()
        {
            if (!_serverRunning || _server == null || _playerIndex.Count == 0)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            const float snapshotInterval = 1f / 15f;
            if (now >= _nextSnapshotTime)
            {
                _nextSnapshotTime = now + snapshotInterval;
                var snapshot = BuildSnapshot();
                _server.BroadcastSnapshot(snapshot);
                _bytesThisSecond += EstimateSnapshotBytes(snapshot);
            }

            if (now >= _nextBytesLogTime)
            {
                _nextBytesLogTime = now + 1f;
                LogNetworkBytesPerSecond(_bytesThisSecond);
                _bytesThisSecond = 0;
            }
        }

        private SnapshotV1 BuildSnapshot()
        {
            var serverTimeMs = (long)(Time.realtimeSinceStartup * 1000f);
            var entities = new SnapshotEntityV1[_playerIndex.Count];
            var i = 0;
            foreach (var entry in _playerIndex)
            {
                if (!_positionsByPlayer.TryGetValue(entry.Key, out var position))
                {
                    position = Vector3.zero;
                }

                var rot = Quaternion.identity;
                entities[i++] = new SnapshotEntityV1(entry.Key, position.x, position.y, position.z, rot.x, rot.y, rot.z, rot.w);
            }

            return new SnapshotV1(serverTimeMs, entities);
        }

        private void LogNetworkBytesPerSecond(int bytesPerSecond)
        {
            var telemetry = new TelemetryContext(
                new MatchId(matchId),
                new MinigameId(minigameId),
                new PlayerId(""),
                new SessionId(""),
                BuildInfo.BuildVersion,
                serverInstanceId);

            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["bytes_per_s"] = bytesPerSecond
            };
            _logger.Log(LogLevel.Debug, "network_bytes_per_s", "Network bytes per second", fields, telemetry);
        }

        private bool ValidateCommandRate(string peerId, float now)
        {
            if (maxCommandsPerSecond <= 0)
            {
                return true;
            }

            if (!_commandWindowStart.TryGetValue(peerId, out var windowStart))
            {
                _commandWindowStart[peerId] = now;
                _commandCount[peerId] = 1;
                return true;
            }

            if (now - windowStart >= 1f)
            {
                _commandWindowStart[peerId] = now;
                _commandCount[peerId] = 1;
                return true;
            }

            _commandCount[peerId] = _commandCount.TryGetValue(peerId, out var count) ? count + 1 : 1;
            return _commandCount[peerId] <= maxCommandsPerSecond;
        }

        private string SanitizePayload(string value, int maxLength, NetworkPeerId peerId, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            var telemetry = new TelemetryContext(
                new MatchId(matchId),
                new MinigameId(minigameId),
                new PlayerId(peerId.Value),
                new SessionId(_sessionByPlayer.TryGetValue(peerId.Value, out var sessionId) ? sessionId : string.Empty),
                BuildInfo.BuildVersion,
                serverInstanceId);

            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["field"] = fieldName,
                ["len"] = value.Length
            };
            _logger.Log(LogLevel.Warn, "payload_clamped", "Payload clamped", fields, telemetry);
            return value.Substring(0, maxLength);
        }

        private static int EstimateSnapshotBytes(SnapshotV1 snapshot)
        {
            if (snapshot.entities == null)
            {
                return 0;
            }

            var bytes = 12; // header estimate
            for (var i = 0; i < snapshot.entities.Length; i++)
            {
                var entity = snapshot.entities[i];
                bytes += 32; // floats + version
                if (!string.IsNullOrEmpty(entity.player_id))
                {
                    bytes += Encoding.UTF8.GetByteCount(entity.player_id);
                }
            }

            return bytes;
        }

        private void LogMovementInvalid(NetworkPeerId peerId, string reason)
        {
            _sessionByPlayer.TryGetValue(peerId.Value, out var sessionId);
            var telemetry = new TelemetryContext(
                new MatchId(matchId),
                new MinigameId(minigameId),
                new PlayerId(ResolvePlayerId(peerId.Value)),
                new SessionId(sessionId ?? string.Empty),
                BuildInfo.BuildVersion,
                serverInstanceId);

            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["reason"] = reason
            };
            _logger.Log(LogLevel.Warn, "movement_invalid", "Movement invalid", fields, telemetry);
        }

        private bool TryAcceptJoinToken(NetworkPeerId peerId, string sessionId, string joinToken, out MatchmakerTokenVerifier.TokenPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(joinToken))
            {
                if (allowEmptyJoinToken)
                {
                    payload = new MatchmakerTokenVerifier.TokenPayload
                    {
                        match_id = matchId,
                        player_id = peerId.Value,
                        exp = 0
                    };
                    return true;
                }

                RejectJoinToken(peerId, sessionId, null, "token_missing");
                return false;
            }

            var verifier = GetTokenVerifier();
            if (verifier == null)
            {
                RejectJoinToken(peerId, sessionId, null, "token_verifier_missing");
                return false;
            }

            if (!verifier.TryValidate(joinToken, out payload, out var reason))
            {
                RejectJoinToken(peerId, sessionId, payload, reason);
                return false;
            }

            if (_usedJoinTokens.Contains(joinToken))
            {
                RejectJoinToken(peerId, sessionId, payload, "token_replay");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(matchId) && payload != null &&
                !string.Equals(matchId, payload.match_id, StringComparison.Ordinal))
            {
                RejectJoinToken(peerId, sessionId, payload, "token_match_mismatch");
                return false;
            }

            _usedJoinTokens.Add(joinToken);
            return true;
        }

        private void RejectJoinToken(NetworkPeerId peerId, string sessionId, MatchmakerTokenVerifier.TokenPayload payload, string reason)
        {
            var matchValue = payload != null && !string.IsNullOrWhiteSpace(payload.match_id) ? payload.match_id : matchId;
            var playerValue = payload != null && !string.IsNullOrWhiteSpace(payload.player_id) ? payload.player_id : peerId.Value;
            var telemetry = new TelemetryContext(
                new MatchId(matchValue),
                new MinigameId(minigameId),
                new PlayerId(playerValue),
                new SessionId(sessionId ?? string.Empty),
                BuildInfo.BuildVersion,
                serverInstanceId);

            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["reason"] = reason
            };
            _logger.Log(LogLevel.Warn, "token_rejected", "Join token rejected", fields, telemetry);
            _server.Disconnect(peerId, reason);
        }

        private MatchmakerTokenVerifier GetTokenVerifier()
        {
            if (_tokenVerifier != null)
            {
                return _tokenVerifier;
            }

            var secret = Environment.GetEnvironmentVariable("MATCHMAKER_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = matchmakerSecret;
            }

            _tokenVerifier = new MatchmakerTokenVerifier(secret);
            return _tokenVerifier;
        }

        private string ResolvePlayerId(string peerId)
        {
            return _playerIdByPeer.TryGetValue(peerId, out var playerId) ? playerId : peerId;
        }
    }
}
