using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace Game.Server
{
    public sealed class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private string minigameId = "stub_v1";
        [SerializeField] private string catalogRelativePath = "Game/Minigames";
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
            var telemetry = new TelemetryContext(
                new MatchId("m_bootstrap"),
                new MinigameId(minigameId),
                new PlayerId("p_bootstrap"),
                new SessionId("s_bootstrap"),
                BuildInfo.BuildVersion,
                "server_local");

            var logger = new JsonRuntimeLogger();
            var minigame = MinigameRuntimeLoader.LoadById(minigameId, catalogRelativePath, telemetry, logger, out var manifest);
            if (minigame == null)
            {
                return;
            }

            var contentLoader = new MinigameContentLoader(logger, telemetry);
            contentLoader.LoadAllBlocking(manifest);

            var context = new StubMinigameContext(telemetry, logger, manifest.settings, manifest.permissions);
            context.AddPlayer(new PlayerRef(new PlayerId("p1")));
            context.AddPlayer(new PlayerRef(new PlayerId("p2")));

            var runner = new MinigameRunner(minigame, context);
            var tickRunner = new ServerMatchRunner(logger, telemetry);

            runner.Load();
            runner.Start();

            for (var i = 0; i < warmupTicks; i++)
            {
                tickRunner.RunTick(minigame, context, tickDelta);
            }

            runner.End(new GameResult(EndGameReason.Completed));
            contentLoader.UnloadAll();
        }
    }
}
