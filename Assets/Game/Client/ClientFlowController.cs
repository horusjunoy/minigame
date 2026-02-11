using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Network;
using Game.Runtime;
using UnityEngine;

namespace Game.Client
{
    public sealed class ClientFlowController : MonoBehaviour
    {
        private enum FlowState
        {
            Hub,
            CreatingMatch,
            Room,
            Connecting,
            Loading,
            InGame,
            Results,
            Error
        }

        [Header("References")]
        [SerializeField] private ClientNetworkBootstrap networkBootstrap;
        [SerializeField] private MatchmakerClient matchmakerClient;

        [Header("Config")]
        [SerializeField] private string minigameId = "arena_v1";
        [SerializeField] private int maxPlayers = 8;
        [SerializeField] private float loadingSeconds = 1.5f;
        [SerializeField] private int matchDurationSeconds = 600;
        [SerializeField] private int scoreToWin = 10;
        [SerializeField] private float matchmakingTimeoutSeconds = 15f;
        [SerializeField] private int matchmakingRetryCount = 2;
        [SerializeField] private float matchmakingRetryDelaySeconds = 1.5f;
        [SerializeField] private float reconnectWindowSeconds = 6f;
        [SerializeField] private float reconnectDelaySeconds = 1f;
        [SerializeField] private int reconnectMaxAttempts = 1;
        [SerializeField] private float configRefreshSeconds = 30f;

        private FlowState _state = FlowState.Hub;
        private string _errorMessage;
        private string _matchId;
        private string _endpoint;
        private string _joinToken;
        private string _playerId;
        private int _players;
        private int _maxPlayers;
        private float _stateStartedAt;
        private float _gameStartedAt;
        private int _localScore;
        private float _nextScoreTime;
        private readonly List<ScoreEntry> _results = new List<ScoreEntry>();
        private bool _ignoreDisconnect;
        private bool _reconnecting;
        private float _matchmakingDeadline;
        private float _reconnectDeadline;
        private int _reconnectAttempts;
        private bool _pendingReconnect;
        private JsonRuntimeLogger _logger;
        private string[] _remoteMinigamePool;
        private string[] _remoteBlockedMinigames;
        private string _remoteFallbackMinigame;
        private int _remoteMaxPlayers;
        private float _lastConfigFetchAt;

        private void Awake()
        {
            _logger = new JsonRuntimeLogger();
            if (networkBootstrap != null)
            {
                networkBootstrap.Connected += HandleConnected;
                networkBootstrap.Disconnected += HandleDisconnected;
                networkBootstrap.WelcomeReceived += HandleWelcome;
                networkBootstrap.ErrorReceived += HandleNetworkError;
            }
        }

        private void OnDestroy()
        {
            if (networkBootstrap != null)
            {
                networkBootstrap.Connected -= HandleConnected;
                networkBootstrap.Disconnected -= HandleDisconnected;
                networkBootstrap.WelcomeReceived -= HandleWelcome;
                networkBootstrap.ErrorReceived -= HandleNetworkError;
            }
        }

        public void SetReferences(ClientNetworkBootstrap bootstrap, MatchmakerClient client)
        {
            if (networkBootstrap != null)
            {
                networkBootstrap.Connected -= HandleConnected;
                networkBootstrap.Disconnected -= HandleDisconnected;
                networkBootstrap.WelcomeReceived -= HandleWelcome;
                networkBootstrap.ErrorReceived -= HandleNetworkError;
            }

            networkBootstrap = bootstrap;
            matchmakerClient = client;

            if (networkBootstrap != null)
            {
                networkBootstrap.Connected += HandleConnected;
                networkBootstrap.Disconnected += HandleDisconnected;
                networkBootstrap.WelcomeReceived += HandleWelcome;
                networkBootstrap.ErrorReceived += HandleNetworkError;
            }
        }

        private void Update()
        {
            if (_state == FlowState.Room && matchmakerClient != null && !string.IsNullOrWhiteSpace(_matchId))
            {
                if (Time.realtimeSinceStartup - _stateStartedAt > 2f)
                {
                    _stateStartedAt = Time.realtimeSinceStartup;
                    StartCoroutine(matchmakerClient.ListMatches(minigameId, OnMatchesListed));
                }
            }

            if (_state == FlowState.Loading && Time.realtimeSinceStartup - _stateStartedAt >= loadingSeconds)
            {
                EnterInGame();
            }

            if (_state == FlowState.InGame)
            {
                UpdateGameState();
            }
        }

        private void UpdateGameState()
        {
            var elapsed = Time.realtimeSinceStartup - _gameStartedAt;
            if (elapsed >= matchDurationSeconds)
            {
                EndMatch("timeout");
                return;
            }

            if (Time.realtimeSinceStartup >= _nextScoreTime)
            {
                _nextScoreTime = Time.realtimeSinceStartup + 1.75f;
                _localScore += 1;
                if (_localScore >= scoreToWin)
                {
                    EndMatch("score_to_win");
                }
            }
        }

        private void OnGUI()
        {
            var width = 420f;
            var height = 380f;
            var rect = new Rect(16f, 16f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("Minigame Client");
            GUILayout.Space(8f);

            switch (_state)
            {
                case FlowState.Hub:
                    DrawHub();
                    break;
                case FlowState.CreatingMatch:
                    GUILayout.Label("Aguarde...");
                    GUILayout.Label("Criando partida");
                    break;
                case FlowState.Room:
                    DrawRoom();
                    break;
                case FlowState.Connecting:
                    GUILayout.Label("Aguarde...");
                    GUILayout.Label(GetConnectingLabel());
                    break;
                case FlowState.Loading:
                    GUILayout.Label("Aguarde...");
                    GUILayout.Label("Carregando partida");
                    break;
                case FlowState.InGame:
                    DrawHud();
                    break;
                case FlowState.Results:
                    DrawResults();
                    break;
                case FlowState.Error:
                    DrawError();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawHub()
        {
            GUILayout.Label("Hub");
            GUILayout.Space(6f);
            if (GUILayout.Button("Quick Play"))
            {
                StartQuickPlay();
            }
        }

        private void DrawRoom()
        {
            GUILayout.Label("Sala");
            GUILayout.Label($"Match: {_matchId}");
            GUILayout.Label($"Endpoint: {_endpoint}");
            GUILayout.Label($"Players: {_players}/{_maxPlayers}");
            GUILayout.Space(8f);
            if (GUILayout.Button("Iniciar"))
            {
                StartConnecting();
            }
            if (GUILayout.Button("Sair"))
            {
                StartCoroutine(EndMatchRequest("client_left"));
                GoToHub();
            }
        }

        private void DrawHud()
        {
            var elapsed = Time.realtimeSinceStartup - _gameStartedAt;
            var remaining = Mathf.Max(0f, matchDurationSeconds - elapsed);
            GUILayout.Label("HUD");
            GUILayout.Label($"Tempo: {Mathf.CeilToInt(remaining)}s");
            GUILayout.Label($"Score: {_localScore}/{scoreToWin}");
            GUILayout.Space(8f);
            if (GUILayout.Button("Finalizar"))
            {
                EndMatch("manual");
            }
        }

        private void DrawResults()
        {
            GUILayout.Label("Resultados");
            foreach (var entry in _results)
            {
                GUILayout.Label($"{entry.PlayerId}: {entry.Score}");
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("Jogar de novo"))
            {
                StartQuickPlay();
            }
            if (EconomyFeatureFlags.IsEnabled())
            {
                if (GUILayout.Button("Comprar item (stub)"))
                {
                    LogPurchaseIntent("starter_pack", "results");
                }
            }
            if (GUILayout.Button("Voltar ao Hub"))
            {
                GoToHub();
            }
        }

        private void DrawError()
        {
            GUILayout.Label("Erro");
            GUILayout.Label(GetFriendlyError(_errorMessage));
            if (!string.IsNullOrWhiteSpace(_errorMessage))
            {
                GUILayout.Label($"Detalhes: {_errorMessage}");
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("Voltar ao Hub"))
            {
                GoToHub();
            }
        }

        private void StartQuickPlay()
        {
            if (matchmakerClient == null)
            {
                SetError("matchmaker_client_missing");
                return;
            }

            _state = FlowState.CreatingMatch;
            _stateStartedAt = Time.realtimeSinceStartup;
            _matchmakingDeadline = Time.realtimeSinceStartup + matchmakingTimeoutSeconds;
            StartCoroutine(CreateMatchWithRetry());
        }

        private void OnMatchCreated(MatchmakerClient.Result<MatchmakerClient.CreateMatchResponse> result)
        {
            if (!result.Success || result.Payload == null)
            {
                SetError($"match_create_failed: {result.Error}");
                return;
            }

            ApplyMatchCreated(result.Payload);
            StartCoroutine(JoinMatchWithRetry());
        }

        private void OnMatchJoined(MatchmakerClient.Result<MatchmakerClient.JoinMatchResponse> result)
        {
            if (!result.Success || result.Payload == null)
            {
                SetError($"match_join_failed: {result.Error}");
                return;
            }

            _endpoint = string.IsNullOrWhiteSpace(result.Payload.endpoint) ? _endpoint : result.Payload.endpoint;
            _joinToken = string.IsNullOrWhiteSpace(result.Payload.join_token) ? _joinToken : result.Payload.join_token;

            ApplyMatchJoined(result.Payload);
            StartCoroutine(VerifyTokenWithRetry());
        }

        private void OnTokenVerified(MatchmakerClient.Result<MatchmakerClient.TokenVerifyResponse> result)
        {
            if (!result.Success || result.Payload == null)
            {
                SetError($"token_invalid: {result.Error}");
                return;
            }

            ApplyTokenVerified(result.Payload);
        }

        private void OnMatchesListed(MatchmakerClient.Result<MatchmakerClient.MatchListResponse> result)
        {
            if (!result.Success || result.Payload == null || result.Payload.items == null)
            {
                return;
            }

            for (var i = 0; i < result.Payload.items.Length; i++)
            {
                var entry = result.Payload.items[i];
                if (entry.match_id == _matchId)
                {
                    _players = entry.players;
                    _maxPlayers = entry.max_players;
                    return;
                }
            }
        }

        private void StartConnecting()
        {
            if (networkBootstrap == null)
            {
                SetError("network_bootstrap_missing");
                return;
            }

            if (!TryParseEndpoint(_endpoint, out var address, out var port))
            {
                SetError("endpoint_invalid");
                return;
            }

            _state = FlowState.Connecting;
            _stateStartedAt = Time.realtimeSinceStartup;
            networkBootstrap.ConfigureEndpoint(address, port);
            networkBootstrap.SetJoinToken(_joinToken);
            networkBootstrap.StartClient();
        }

        private void HandleConnected()
        {
            _state = FlowState.Loading;
            _stateStartedAt = Time.realtimeSinceStartup;
            _reconnectAttempts = 0;
            _reconnectDeadline = 0f;
            _pendingReconnect = false;
            _reconnecting = false;
        }

        private void HandleDisconnected()
        {
            if (_ignoreDisconnect)
            {
                _ignoreDisconnect = false;
                return;
            }

            if (TryQueueReconnect())
            {
                return;
            }

            SetError("server_disconnected");
        }

        private void HandleWelcome(WelcomeMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.match_id))
            {
                _matchId = message.match_id;
            }
        }

        private void HandleNetworkError(ServerErrorMessage message)
        {
            _ignoreDisconnect = true;
            var detail = BuildNetworkErrorDetail(message);
            SetError(detail);
        }

        private void EnterInGame()
        {
            _state = FlowState.InGame;
            _gameStartedAt = Time.realtimeSinceStartup;
            _localScore = 0;
            _nextScoreTime = Time.realtimeSinceStartup + 1.5f;
        }

        private void EndMatch(string reason)
        {
            StartCoroutine(EndMatchRequest(reason));
            BuildResults();
            _state = FlowState.Results;
        }

        private IEnumerator EndMatchRequest(string reason)
        {
            if (matchmakerClient != null && !string.IsNullOrWhiteSpace(_matchId))
            {
                yield return matchmakerClient.EndMatch(_matchId, reason, _ => { });
            }

            if (networkBootstrap != null)
            {
                _ignoreDisconnect = true;
                networkBootstrap.StopClient();
            }
        }

        private void BuildResults()
        {
            _results.Clear();
            _results.Add(new ScoreEntry(_playerId ?? "player", _localScore));
            _results.Add(new ScoreEntry("bot_a", Mathf.Max(0, _localScore - 2)));
            _results.Add(new ScoreEntry("bot_b", Mathf.Max(0, _localScore - 4)));
            _results.Sort((a, b) => b.Score.CompareTo(a.Score));
        }

        private void GoToHub()
        {
            _matchId = null;
            _endpoint = null;
            _joinToken = null;
            _playerId = null;
            _players = 0;
            _maxPlayers = maxPlayers;
            _results.Clear();
            _reconnectAttempts = 0;
            _reconnectDeadline = 0f;
            _pendingReconnect = false;
            _reconnecting = false;
            if (networkBootstrap != null)
            {
                _ignoreDisconnect = true;
                networkBootstrap.StopClient();
            }
            _state = FlowState.Hub;
        }

        private void SetError(string message)
        {
            _errorMessage = message;
            _state = FlowState.Error;
        }

        private static bool TryParseEndpoint(string endpoint, out string address, out ushort port)
        {
            address = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }

            var parts = endpoint.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            address = parts[0];
            return ushort.TryParse(parts[1], out port);
        }

        private IEnumerator CreateMatchWithRetry()
        {
            yield return RefreshRemoteConfig();
            var attempt = 0;
            while (attempt <= matchmakingRetryCount)
            {
                MatchmakerClient.Result<MatchmakerClient.CreateMatchResponse> result = null;
                var selectedMinigame = ResolveMinigameId();
                var selectedMaxPlayers = ResolveMaxPlayers();
                yield return matchmakerClient.CreateMatch(selectedMinigame, selectedMaxPlayers, r => result = r);
                if (result != null && result.Success && result.Payload != null)
                {
                    minigameId = selectedMinigame;
                    ApplyMatchCreated(result.Payload);
                    yield return JoinMatchWithRetry();
                    yield break;
                }

                if (IsMatchmakingTimedOut())
                {
                    SetError("matchmaking_timeout");
                    yield break;
                }

                if (result != null && result.Error == "allocation_failed")
                {
                    SetError("allocation_failed");
                    yield break;
                }

                attempt += 1;
                if (attempt <= matchmakingRetryCount)
                {
                    yield return new WaitForSeconds(matchmakingRetryDelaySeconds);
                }
            }

            SetError("match_create_failed");
        }

        private IEnumerator JoinMatchWithRetry()
        {
            var attempt = 0;
            while (attempt <= matchmakingRetryCount)
            {
                MatchmakerClient.Result<MatchmakerClient.JoinMatchResponse> result = null;
                yield return matchmakerClient.JoinMatch(_matchId, r => result = r);
                if (result != null && result.Success && result.Payload != null)
                {
                    ApplyMatchJoined(result.Payload);
                    yield return VerifyTokenWithRetry();
                    yield break;
                }

                if (IsMatchmakingTimedOut())
                {
                    SetError("matchmaking_timeout");
                    yield break;
                }

                attempt += 1;
                if (attempt <= matchmakingRetryCount)
                {
                    yield return new WaitForSeconds(matchmakingRetryDelaySeconds);
                }
            }

            SetError("match_join_failed");
        }

        private IEnumerator VerifyTokenWithRetry()
        {
            var attempt = 0;
            while (attempt <= matchmakingRetryCount)
            {
                MatchmakerClient.Result<MatchmakerClient.TokenVerifyResponse> result = null;
                yield return matchmakerClient.VerifyToken(_joinToken, r => result = r);
                if (result != null && result.Success && result.Payload != null)
                {
                    ApplyTokenVerified(result.Payload);
                    yield break;
                }

                if (IsMatchmakingTimedOut())
                {
                    SetError("matchmaking_timeout");
                    yield break;
                }

                attempt += 1;
                if (attempt <= matchmakingRetryCount)
                {
                    yield return new WaitForSeconds(matchmakingRetryDelaySeconds);
                }
            }

            SetError("token_invalid");
        }

        private void ApplyMatchCreated(MatchmakerClient.CreateMatchResponse payload)
        {
            _matchId = payload.match_id;
            _endpoint = payload.endpoint;
            _joinToken = payload.join_token;
            _players = 0;
            _maxPlayers = maxPlayers;
            _state = FlowState.CreatingMatch;
            _stateStartedAt = Time.realtimeSinceStartup;
        }

        private void ApplyMatchJoined(MatchmakerClient.JoinMatchResponse payload)
        {
            _endpoint = string.IsNullOrWhiteSpace(payload.endpoint) ? _endpoint : payload.endpoint;
            _joinToken = string.IsNullOrWhiteSpace(payload.join_token) ? _joinToken : payload.join_token;
        }

        private void ApplyTokenVerified(MatchmakerClient.TokenVerifyResponse payload)
        {
            _playerId = payload.player_id;
            _state = FlowState.Room;
            _stateStartedAt = Time.realtimeSinceStartup;
        }

        private bool IsMatchmakingTimedOut()
        {
            return matchmakingTimeoutSeconds > 0f && Time.realtimeSinceStartup > _matchmakingDeadline;
        }

        private bool TryQueueReconnect()
        {
            if (networkBootstrap == null)
            {
                return false;
            }

            if (_state != FlowState.InGame && _state != FlowState.Loading && _state != FlowState.Connecting)
            {
                return false;
            }

            if (_reconnectAttempts >= reconnectMaxAttempts)
            {
                return false;
            }

            if (_reconnectDeadline <= 0f)
            {
                _reconnectDeadline = Time.realtimeSinceStartup + reconnectWindowSeconds;
            }

            if (Time.realtimeSinceStartup > _reconnectDeadline)
            {
                return false;
            }

            if (_pendingReconnect)
            {
                return true;
            }

            _pendingReconnect = true;
            _reconnecting = true;
            StartCoroutine(DoReconnect());
            return true;
        }

        private IEnumerator DoReconnect()
        {
            _reconnectAttempts += 1;
            _state = FlowState.Connecting;
            _stateStartedAt = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(reconnectDelaySeconds);
            _pendingReconnect = false;
            networkBootstrap.StartClient();
        }

        private string GetFriendlyError(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "Algo deu errado. Tente novamente.";
            }

            if (code.StartsWith("match_create_failed", StringComparison.OrdinalIgnoreCase))
            {
                return "Nao foi possivel criar a partida. Tente novamente.";
            }
            if (code.StartsWith("match_join_failed", StringComparison.OrdinalIgnoreCase))
            {
                return "Nao foi possivel entrar na partida. Tente novamente.";
            }
            if (code.StartsWith("token_invalid", StringComparison.OrdinalIgnoreCase))
            {
                return "Token invalido. Refaça o matchmaking.";
            }
            if (code.StartsWith("allocation_failed", StringComparison.OrdinalIgnoreCase))
            {
                return "Servidor cheio no momento. Tente novamente.";
            }
            if (code.StartsWith("matchmaking_timeout", StringComparison.OrdinalIgnoreCase))
            {
                return "Timeout de matchmaking. Verifique sua conexao.";
            }
            if (code.StartsWith("server_disconnected", StringComparison.OrdinalIgnoreCase))
            {
                return "Conexao perdida com o servidor.";
            }
            if (code.StartsWith("endpoint_invalid", StringComparison.OrdinalIgnoreCase))
            {
                return "Endpoint invalido. Tente novamente.";
            }
            if (code.StartsWith("network_bootstrap_missing", StringComparison.OrdinalIgnoreCase))
            {
                return "Erro interno de rede.";
            }
            if (code.StartsWith("protocol_mismatch", StringComparison.OrdinalIgnoreCase))
            {
                return "Versao do protocolo incompatível. Atualize o client.";
            }
            if (code.StartsWith("build_mismatch", StringComparison.OrdinalIgnoreCase))
            {
                return "Versao do client incompatível. Atualize o client.";
            }

            return code;
        }

        private string GetConnectingLabel()
        {
            if (_reconnecting)
            {
                return $"Reconectando... ({_reconnectAttempts + 1}/{Mathf.Max(1, reconnectMaxAttempts)})";
            }

            return "Conectando ao servidor";
        }

        private static string BuildNetworkErrorDetail(ServerErrorMessage message)
        {
            if (message.code == null)
            {
                return "server_error";
            }

            if (message.code.StartsWith("protocol_mismatch", StringComparison.OrdinalIgnoreCase))
            {
                return $"protocol_mismatch: server={message.server_protocol_version} client={message.client_protocol_version}";
            }

            if (message.code.StartsWith("build_mismatch", StringComparison.OrdinalIgnoreCase))
            {
                return $"build_mismatch: server={message.server_build_version} client={message.client_build_version}";
            }

            if (!string.IsNullOrWhiteSpace(message.detail))
            {
                return $"{message.code}: {message.detail}";
            }

            return message.code;
        }

        private void LogPurchaseIntent(string sku, string source)
        {
            var telemetry = new TelemetryContext(
                new MatchId(_matchId ?? string.Empty),
                new MinigameId(minigameId ?? string.Empty),
                new PlayerId(_playerId ?? string.Empty),
                new SessionId(string.Empty),
                BuildInfo.BuildVersion,
                "client_local");

            EconomyEventPublisher.LogPurchaseIntent(_logger, telemetry, sku, source);
        }

        private IEnumerator RefreshRemoteConfig()
        {
            if (matchmakerClient == null)
            {
                yield break;
            }

            if (configRefreshSeconds > 0f && Time.realtimeSinceStartup - _lastConfigFetchAt < configRefreshSeconds)
            {
                yield break;
            }

            _lastConfigFetchAt = Time.realtimeSinceStartup;
            MatchmakerClient.Result<MatchmakerClient.RemoteConfigResponse> result = null;
            yield return matchmakerClient.GetConfig(r => result = r);
            if (result == null || !result.Success || result.Payload == null)
            {
                yield break;
            }

            ApplyRemoteConfig(result.Payload);
        }

        private void ApplyRemoteConfig(MatchmakerClient.RemoteConfigResponse config)
        {
            _remoteMinigamePool = config.minigame_pool;
            _remoteBlockedMinigames = config.blocked_minigames;
            _remoteFallbackMinigame = config.fallback_minigame_id;
            _remoteMaxPlayers = config.max_players;
            if (config.match_duration_s > 0)
            {
                matchDurationSeconds = config.match_duration_s;
            }
        }

        private string ResolveMinigameId()
        {
            if (_remoteMinigamePool != null && _remoteMinigamePool.Length > 0)
            {
                var candidate = _remoteMinigamePool[UnityEngine.Random.Range(0, _remoteMinigamePool.Length)];
                if (_remoteBlockedMinigames != null)
                {
                    for (var i = 0; i < _remoteBlockedMinigames.Length; i++)
                    {
                        if (string.Equals(candidate, _remoteBlockedMinigames[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return string.IsNullOrWhiteSpace(_remoteFallbackMinigame) ? minigameId : _remoteFallbackMinigame;
                        }
                    }
                }

                return candidate;
            }

            return minigameId;
        }

        private int ResolveMaxPlayers()
        {
            if (_remoteMaxPlayers > 0)
            {
                return _remoteMaxPlayers;
            }

            return maxPlayers;
        }

        private readonly struct ScoreEntry
        {
            public readonly string PlayerId;
            public readonly int Score;

            public ScoreEntry(string playerId, int score)
            {
                PlayerId = playerId;
                Score = score;
            }
        }
    }
}
