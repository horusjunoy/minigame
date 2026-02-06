using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Minigames.Stub;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Runtime
{
    public sealed class MinigameConformanceTests
    {
        private sealed class TestLogger : IRuntimeLogger
        {
            public readonly List<string> Events = new List<string>();

            public void Log(LogLevel level, string eventName, string message, object fields = null, TelemetryContext? context = null)
            {
                Events.Add(eventName);
            }
        }

        [Test]
        public void StubMinigame_Lifecycle_Completes_WithoutExceptions()
        {
            var manifestPath = Path.Combine(Application.dataPath, "Game/Minigames/Stub/StubMinigame.manifest.json");
            var manifest = MinigameManifestLoader.LoadFromFile(manifestPath);
            Assert.NotNull(manifest, "Manifest should load for conformance test.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(manifest.server_entry), "Manifest must define server_entry.");

            var minigame = MinigameFactory.CreateFromEntry(manifest.server_entry);
            Assert.NotNull(minigame, "MinigameFactory should create the minigame.");

            var telemetry = new TelemetryContext(
                new MatchId("m_test"),
                new MinigameId(manifest.id ?? "stub_v1"),
                new PlayerId("p_test"),
                new SessionId("s_test"),
                BuildInfo.BuildVersion,
                "server_test");

            var logger = new TestLogger();
            var context = new StubMinigameContext(telemetry, logger);
            context.AddPlayer(new PlayerRef(new PlayerId("p1")));
            context.AddPlayer(new PlayerRef(new PlayerId("p2")));

            var runner = new MinigameRunner(minigame, context);

            Assert.DoesNotThrow(runner.Load);
            Assert.DoesNotThrow(runner.Start);

            foreach (var player in context.GetPlayers())
            {
                Assert.DoesNotThrow(() => minigame.OnPlayerJoin(player));
            }

            for (var i = 0; i < 3; i++)
            {
                Assert.DoesNotThrow(() => runner.Tick(0.016f));
            }

            Assert.DoesNotThrow(() => runner.End(new GameResult(EndGameReason.Completed)));

            Assert.Contains("minigame_loaded", logger.Events);
            Assert.Contains("match_started", logger.Events);
            Assert.Contains("match_ended", logger.Events);
        }
    }
}
