using System.IO;
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
            try
            {
                ServerHealthEndpoint.StartForSmoke();

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
                var context = new StubMinigameContext(telemetry, logger, manifest.settings);
                context.AddPlayer(new PlayerRef(new PlayerId("p1")));
                context.AddPlayer(new PlayerRef(new PlayerId("p2")));

                var runner = new MinigameRunner(minigame, context);

                runner.Load();
                runner.Start();
                runner.Tick(0.016f);
                runner.Tick(0.016f);
                runner.End(new GameResult(EndGameReason.Completed));

                Debug.Log("Smoke: runtime completed.");
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
