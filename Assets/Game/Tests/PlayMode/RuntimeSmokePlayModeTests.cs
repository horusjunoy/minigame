using System.Collections;
using System.IO;
using Game.Core;
using Game.Minigames.Stub;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class RuntimeSmokePlayModeTests
    {
        private sealed class TestLogger : IRuntimeLogger
        {
            public void Log(LogLevel level, string eventName, string message, object fields = null, TelemetryContext? context = null)
            {
            }
        }

        [UnityTest]
        public IEnumerator Runtime_Smoke_Runs_Minimal_Lifecycle()
        {
            var manifestPath = Path.Combine(Application.dataPath, "Game/Minigames/Stub/StubMinigame.manifest.json");
            var manifest = MinigameManifestLoader.LoadFromFile(manifestPath);
            Assert.NotNull(manifest);

            var minigame = MinigameFactory.CreateFromEntry(manifest.server_entry);
            Assert.NotNull(minigame);

            var telemetry = new TelemetryContext(
                new MatchId("m_smoke"),
                new MinigameId(manifest.id ?? "stub_v1"),
                new PlayerId("p_smoke"),
                new SessionId("s_smoke"),
                BuildInfo.BuildVersion,
                "server_smoke");

            var context = new StubMinigameContext(telemetry, new TestLogger(), manifest.settings);
            context.AddPlayer(new PlayerRef(new PlayerId("p1")));
            context.AddPlayer(new PlayerRef(new PlayerId("p2")));

            var runner = new MinigameRunner(minigame, context);
            runner.Load();
            runner.Start();

            yield return null;

            runner.Tick(0.016f);
            runner.Tick(0.016f);

            runner.End(new GameResult(EndGameReason.Completed));
        }
    }
}
