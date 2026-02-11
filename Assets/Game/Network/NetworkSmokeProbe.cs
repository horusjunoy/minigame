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

            facade.Client.WelcomeReceived += OnWelcomeReceived;
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

            LastSuccess = true;
            LastMessage = "welcome_received";
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
