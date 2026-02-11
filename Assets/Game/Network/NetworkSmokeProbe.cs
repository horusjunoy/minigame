using System;
using System.IO;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkSmokeProbe : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour facadeBehaviour;
        [SerializeField] private float timeoutSeconds = 5f;
        [SerializeField] private bool autoStart = true;

        private float _startTime;
        private bool _done;
        private bool _welcomeReceived;
        private bool _snapshotReceived;
        private bool _requestedDisconnect;
        private INetworkClient _client;

        public static bool LastSuccess { get; private set; }
        public static string LastMessage { get; private set; }

        public static void ResetResult()
        {
            LastSuccess = false;
            LastMessage = string.Empty;
        }

        private void Start()
        {
            if (!autoStart)
            {
                return;
            }

            _startTime = Time.realtimeSinceStartup;
            Debug.Log("network_smoke_start");
            AppendResult("network_smoke_start");
            if (facadeBehaviour == null)
            {
                Fail("facade_missing");
                return;
            }

            if (!(facadeBehaviour is INetworkFacade facade))
            {
                Fail("facade_invalid");
                return;
            }

            _client = facade.Client;
            _client.WelcomeReceived += OnWelcomeReceived;
            _client.SnapshotReceived += OnSnapshotReceived;
            _client.Disconnected += OnDisconnected;
        }

        private void Update()
        {
            if (_done)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _startTime > timeoutSeconds)
            {
                Fail("timeout");
            }
        }

        private void OnWelcomeReceived(WelcomeMessage message)
        {
            if (_done)
            {
                return;
            }

            _welcomeReceived = true;
            LastMessage = "welcome_received";
            AppendResult("network_smoke_welcome");
        }

        private void OnSnapshotReceived(SnapshotV1 snapshot)
        {
            if (_done)
            {
                return;
            }

            if (!_welcomeReceived)
            {
                Fail("snapshot_before_welcome");
                return;
            }

            _snapshotReceived = true;
            LastMessage = "snapshot_received";
            AppendResult("network_smoke_snapshot");

            if (!_requestedDisconnect && _client != null)
            {
                _requestedDisconnect = true;
                _client.StopClient();
            }
        }

        private void OnDisconnected()
        {
            if (_done)
            {
                return;
            }

            if (!_welcomeReceived)
            {
                Fail("disconnect_before_welcome");
                return;
            }

            if (!_snapshotReceived)
            {
                Fail("disconnect_before_snapshot");
                return;
            }

            LastSuccess = true;
            LastMessage = "replication_ok";
            _done = true;
            Debug.Log("network_smoke_ok");
            AppendResult("network_smoke_ok");
            RequestQuit(0);
        }

        private void Fail(string reason)
        {
            LastSuccess = false;
            LastMessage = reason;
            _done = true;
            Debug.LogError($"network_smoke_fail:{reason}");
            AppendResult($"network_smoke_fail:{reason}");
            RequestQuit(1);
        }

        private static void AppendResult(string line)
        {
            try
            {
                var overrideDir = Environment.GetEnvironmentVariable("SMOKE_LOG_DIR");
                var logRoot = string.IsNullOrWhiteSpace(overrideDir)
                    ? Directory.GetParent(Application.dataPath)?.FullName
                    : overrideDir;

                if (string.IsNullOrEmpty(logRoot))
                {
                    return;
                }

                var logDir = Path.Combine(logRoot, "logs");
                Directory.CreateDirectory(logDir);
                var path = Path.Combine(logDir, "network-smoke-result.log");
                File.AppendAllText(path, $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
            }
            catch
            {
                // Best effort; avoid crashing the smoke run.
            }
        }

        private static void RequestQuit(int exitCode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(exitCode);
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
