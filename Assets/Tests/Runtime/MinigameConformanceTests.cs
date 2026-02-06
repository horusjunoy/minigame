using Game.Core;
using Game.Minigames.Stub;
using Game.Runtime;
using NUnit.Framework;
using System.IO;

namespace Game.Tests.Runtime
{
    public sealed class MinigameConformanceTests
    {
        [Test]
        public void StubMinigame_Lifecycle_DoesNotThrow()
        {
            var telemetry = new TelemetryContext(
                new MatchId("m_test"),
                new MinigameId("stub_v1"),
                new PlayerId("p_test"),
                new SessionId("s_test"),
                BuildInfo.BuildVersion,
                "server_01");

            var logger = new JsonRuntimeLogger();
            var context = new StubMinigameContext(telemetry, logger);
            var minigame = new StubMinigame();

            minigame.OnLoad(context);
            minigame.OnGameStart();
            minigame.OnPlayerJoin(new PlayerRef(new PlayerId("p1")));
            minigame.OnTick(0.016f);
            minigame.OnGameEnd(new GameResult(EndGameReason.Completed));

            Assert.Pass("Lifecycle executed without exceptions.");
        }

        [Test]
        public void StubManifest_Loads_WithExpectedId()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "Game", "Minigames", "Stub", "StubMinigame.manifest.json");
            var manifest = MinigameManifestLoader.LoadFromFile(Path.GetFullPath(path));

            Assert.IsNotNull(manifest);
            Assert.AreEqual("stub_v1", manifest.id);
        }
    }
}
