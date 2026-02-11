using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Runtime
{
    public sealed class MinigameConformanceTests
    {
        private sealed class ConformanceLogger : IRuntimeLogger
        {
            public readonly List<string> Events = new List<string>();
            private readonly string _logPath;

            public ConformanceLogger(string logPath)
            {
                _logPath = logPath;
            }

            public void Log(LogLevel level, string eventName, string message, object fields = null, TelemetryContext? context = null)
            {
                Events.Add(eventName);
                try
                {
                    var ctx = context.HasValue ? context.Value : default;
                    var line = $"{DateTime.UtcNow:O} {level} {eventName} match_id={ctx.MatchId.Value} minigame_id={ctx.MinigameId.Value} build_version={ctx.BuildVersion}";
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
                catch
                {
                    // Best effort logging for conformance failures.
                }
            }
        }

        public static IEnumerable<TestCaseData> ManifestCases()
        {
            var root = Path.Combine(Application.dataPath, "Game", "Minigames");
            if (!Directory.Exists(root))
            {
                yield break;
            }

            var files = Directory.GetFiles(root, "*.manifest.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                yield return new TestCaseData(file).SetName($"Conformance_{name}");
            }
        }

        [TestCaseSource(nameof(ManifestCases))]
        public void Minigame_Lifecycle_Completes_WithoutExceptions(string manifestPath)
        {
            var manifest = MinigameManifestLoader.LoadFromFile(manifestPath);
            Assert.NotNull(manifest, $"Manifest parse failed: {manifestPath}");

            var telemetry = new TelemetryContext(
                new MatchId("m_test"),
                new MinigameId(manifest.id ?? "unknown"),
                new PlayerId("p_test"),
                new SessionId("s_test"),
                BuildInfo.BuildVersion,
                "server_test");

            var logPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? ".", "logs", "minigame_conformance.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            var logger = new ConformanceLogger(logPath);
            var minigame = MinigameRuntimeLoader.LoadById(manifest.id, manifest.version, "Game/Minigames", telemetry, logger, out var loadedManifest);
            Assert.NotNull(loadedManifest, "Manifest should load for conformance test.");
            Assert.NotNull(minigame, "Minigame runtime loader should create the minigame.");

            var context = new StubMinigameContext(telemetry, logger, loadedManifest.settings);
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
