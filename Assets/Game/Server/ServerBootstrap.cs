using System.IO;
using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace Game.Server
{
    public sealed class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private string manifestRelativePath = "Game/Minigames/Stub/StubMinigame.manifest.json";
        [SerializeField] private int warmupTicks = 5;
        [SerializeField] private float tickDelta = 0.016f;
        [SerializeField] private bool autoRunOnStart = true;

        private void Start()
        {
            if (!autoRunOnStart)
            {
                return;
            }

            RunOnce();
        }

        public void RunOnce()
        {
            var manifestPath = Path.Combine(Application.dataPath, manifestRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            var manifest = MinigameManifestLoader.LoadFromFile(manifestPath);
            if (manifest == null)
            {
                return;
            }

            var minigame = MinigameFactory.CreateFromEntry(manifest.server_entry);
            if (minigame == null)
            {
                return;
            }

            var telemetry = new TelemetryContext(
                new MatchId("m_bootstrap"),
                new MinigameId(manifest.id ?? "stub_v1"),
                new PlayerId("p_bootstrap"),
                new SessionId("s_bootstrap"),
                BuildInfo.BuildVersion,
                "server_local");

            var logger = new JsonRuntimeLogger();
            var context = new StubMinigameContext(telemetry, logger);
            context.AddPlayer(new PlayerRef(new PlayerId("p1")));
            context.AddPlayer(new PlayerRef(new PlayerId("p2")));

            var runner = new MinigameRunner(minigame, context);
            var tickRunner = new ServerMatchRunner(logger, telemetry);

            runner.Load();
            runner.Start();

            for (var i = 0; i < warmupTicks; i++)
            {
                tickRunner.RunTick(minigame, tickDelta);
            }

            runner.End(new GameResult(EndGameReason.Completed));
        }
    }
}
