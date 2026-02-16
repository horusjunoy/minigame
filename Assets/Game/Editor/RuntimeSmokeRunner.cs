using System.IO;
using System.Threading;
using Game.Core;
using Game.Runtime;
using Game.Server;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class RuntimeSmokeRunner
    {
        public static void Run()
        {
            RunInternal(mobile: false);
        }

        public static void RunMobile()
        {
            RunInternal(mobile: true);
        }

        private static void RunInternal(bool mobile)
        {
            try
            {
                ServerHealthEndpoint.StartForSmoke();
                if (mobile)
                {
                    Debug.Log("mobile_smoke_start");
                }

                var manifestPath = Path.Combine(Application.dataPath, "Game/Minigames/Stub/StubMinigame.manifest.json");
                var manifest = MinigameManifestLoader.LoadFromFile(manifestPath);
                if (manifest == null)
                {
                    Debug.LogError("Smoke: manifest not found.");
                    EditorApplication.Exit(1);
                    return;
                }

                var minigame = MinigameFactory.CreateFromEntry(manifest.server_entry);
                if (minigame == null)
                {
                    Debug.LogError("Smoke: minigame entry invalid.");
                    EditorApplication.Exit(1);
                    return;
                }

                var telemetry = new TelemetryContext(
                    new MatchId("m_smoke"),
                    new MinigameId(manifest.id ?? "stub_v1"),
                    new PlayerId("p_smoke"),
                    new SessionId("s_smoke"),
                    BuildInfo.BuildVersion,
                    "server_smoke");

                var logger = new JsonRuntimeLogger();
                var context = new StubMinigameContext(telemetry, logger, manifest.settings, manifest.permissions);
                context.AddPlayer(new PlayerRef(new PlayerId("p1")));
                context.AddPlayer(new PlayerRef(new PlayerId("p2")));

                var contentLoader = new MinigameContentLoader(logger, telemetry);
                contentLoader.LoadAllBlocking(manifest);

                var runner = new MinigameRunner(minigame, context);

                runner.Load();
                if (mobile)
                {
                    Debug.Log("mobile_smoke_enter_match");
                }
                runner.Start();
                runner.Tick(0.016f);
                runner.Tick(0.016f);
                runner.End(new GameResult(EndGameReason.Completed));
                if (mobile)
                {
                    Debug.Log("mobile_smoke_results");
                }
                contentLoader.UnloadAll();
                if (mobile)
                {
                    Debug.Log("mobile_smoke_hub");
                    Debug.Log("mobile_smoke_ok");
                }

                Debug.Log("Smoke: runtime completed.");
                Thread.Sleep(1500);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
