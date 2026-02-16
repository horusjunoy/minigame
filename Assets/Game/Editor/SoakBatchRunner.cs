using System;
using System.IO;
using System.Threading;
using Game.Core;
using Game.Runtime;
using Game.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Editor
{
    public static class SoakBatchRunner
    {
        public static void Run()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var minigameId = GetArg(args, "-minigame", "arena_v1");
                var botCount = GetArgInt(args, "-bots", 8);
                var durationSeconds = GetArgInt(args, "-duration", 3600);
                var scoreToWin = GetArgInt(args, "-scoreToWin", -1);
                var timeScale = GetArgFloat(args, "-timeScale", 1f);
                var summaryPath = GetArg(args, "-summaryPath", Path.Combine("logs", "soak_summary.log"));
                var tickRate = GetArgInt(args, "-tickRate", 30);

                var telemetry = new TelemetryContext(
                    new MatchId("m_soak"),
                    new MinigameId(minigameId),
                    new PlayerId("p_soak"),
                    new SessionId("s_soak"),
                    BuildInfo.BuildVersion,
                    "server_soak");

                var logger = new SoakRuntimeLogger();
                var minigame = MinigameRuntimeLoader.LoadById(minigameId, "Game/Minigames", telemetry, logger, out var manifest);
                if (minigame == null || manifest == null)
                {
                    Debug.LogError("Soak: failed to load minigame.");
                    EditorApplication.Exit(1);
                    return;
                }

                var settings = manifest.settings ?? new Settings();
                settings.match_duration_s = -Mathf.Max(1, durationSeconds);
                settings.score_to_win = scoreToWin;
                var context = new StubMinigameContext(telemetry, logger, settings, manifest.permissions);
                var runner = new MinigameRunner(minigame, context);
                var tickRunner = new ServerMatchRunner(logger, telemetry);

                runner.Load();
                runner.Start();

                for (var i = 0; i < botCount; i++)
                {
                    var playerId = new PlayerId($"bot_{i + 1}");
                    var player = new PlayerRef(playerId);
                    context.AddPlayer(player);
                    minigame.OnPlayerJoin(player);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var lastTick = stopwatch.Elapsed;
                var tickDelta = 1f / Mathf.Max(1f, tickRate);
                var simulatedElapsed = 0f;
                var metricsInterval = TimeSpan.FromSeconds(10);
                var lastMetrics = stopwatch.Elapsed;
                var intervalTicks = 0;
                var intervalTickMs = 0.0;
                var totalTicks = 0;
                var totalTickMs = 0.0;
                var lastGc0 = GC.CollectionCount(0);
                var lastGc1 = GC.CollectionCount(1);
                var lastGc2 = GC.CollectionCount(2);
                var latencyMsAvg = 0.0;

                while (simulatedElapsed < durationSeconds)
                {
                    if (context.HasEnded)
                    {
                        break;
                    }

                    var now = stopwatch.Elapsed;
                    var elapsed = (float)(now - lastTick).TotalSeconds;
                    if (elapsed >= tickDelta)
                    {
                        lastTick = now;
                        var scaled = elapsed * Mathf.Max(0.01f, timeScale);
                        simulatedElapsed += scaled;
                        var tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        tickRunner.RunTick(minigame, context, scaled);
                        var tickMs = (System.Diagnostics.Stopwatch.GetTimestamp() - tickStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                        intervalTicks += 1;
                        intervalTickMs += tickMs;
                        totalTicks += 1;
                        totalTickMs += tickMs;
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }

                    if (stopwatch.Elapsed - lastMetrics >= metricsInterval)
                    {
                        var intervalSeconds = (stopwatch.Elapsed - lastMetrics).TotalSeconds;
                        var tickRateHz = intervalSeconds > 0 ? intervalTicks / intervalSeconds : 0;
                        var tickMsAvg = intervalTicks > 0 ? intervalTickMs / intervalTicks : 0;
                        var memTotal = GC.GetTotalMemory(false);
                        var monoUsed = Profiler.GetMonoUsedSizeLong();
                        var gc0 = GC.CollectionCount(0);
                        var gc1 = GC.CollectionCount(1);
                        var gc2 = GC.CollectionCount(2);
                        var fields = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["interval_s"] = Math.Round(intervalSeconds, 1),
                            ["ticks"] = intervalTicks,
                            ["tick_rate_hz"] = Math.Round(tickRateHz, 1),
                            ["tick_ms_avg"] = Math.Round(tickMsAvg, 2),
                            ["mem_total_bytes"] = memTotal,
                            ["mono_used_bytes"] = monoUsed,
                            ["gc0"] = gc0 - lastGc0,
                            ["gc1"] = gc1 - lastGc1,
                            ["gc2"] = gc2 - lastGc2,
                            ["latency_ms_avg"] = Math.Round(latencyMsAvg, 1)
                        };
                        logger.Log(LogLevel.Info, "soak_metrics", "Soak metrics snapshot", fields, telemetry);

                        lastMetrics = stopwatch.Elapsed;
                        intervalTicks = 0;
                        intervalTickMs = 0.0;
                        lastGc0 = gc0;
                        lastGc1 = gc1;
                        lastGc2 = gc2;
                    }
                }

                var result = context.HasEnded ? context.LastResult : new GameResult(EndGameReason.Timeout);
                runner.End(result);

                var endedEarly = simulatedElapsed + 0.5f < durationSeconds;
                var summary = logger.BuildSummary(minigameId, botCount, simulatedElapsed, endedEarly, result.Reason.ToString());
                var totalSeconds = Math.Max(1.0, stopwatch.Elapsed.TotalSeconds);
                var avgTickMs = totalTicks > 0 ? totalTickMs / totalTicks : 0.0;
                var avgTickRate = totalTicks / totalSeconds;
                var totalMem = GC.GetTotalMemory(false);
                var monoMem = Profiler.GetMonoUsedSizeLong();
                var gcTotal0 = GC.CollectionCount(0);
                var gcTotal1 = GC.CollectionCount(1);
                var gcTotal2 = GC.CollectionCount(2);
                var activeEntities = context.ActiveEntityCount;
                var playerSet = new System.Collections.Generic.HashSet<PlayerId>();
                var players = context.GetPlayers();
                for (var i = 0; i < players.Count; i++)
                {
                    playerSet.Add(players[i].Id);
                }
                var orphanScores = 0;
                var scoreboard = context.GetScoreboard().Snapshot();
                foreach (var entry in scoreboard)
                {
                    if (!playerSet.Contains(entry.Key))
                    {
                        orphanScores += 1;
                    }
                }

                if (activeEntities > 0)
                {
                    var fields = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["active_entities"] = activeEntities
                    };
                    logger.Log(LogLevel.Warn, "entity_leak", $"Active entities after end: {activeEntities}", fields, telemetry);
                }

                if (orphanScores > 0)
                {
                    var fields = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["orphan_scores"] = orphanScores
                    };
                    logger.Log(LogLevel.Warn, "scoreboard_orphan", $"Scores without player: {orphanScores}", fields, telemetry);
                }

                summary += $"tick_rate_hz_avg={avgTickRate:0.0}\n";
                summary += $"tick_ms_avg={avgTickMs:0.00}\n";
                summary += $"mem_total_bytes={totalMem}\n";
                summary += $"mono_used_bytes={monoMem}\n";
                summary += $"gc0_total={gcTotal0}\n";
                summary += $"gc1_total={gcTotal1}\n";
                summary += $"gc2_total={gcTotal2}\n";
                summary += $"latency_ms_avg={latencyMsAvg:0.0}\n";
                summary += $"active_entities={activeEntities}\n";
                summary += $"orphan_scores={orphanScores}\n";
                if (Mathf.Abs(timeScale - 1f) > 0.01f)
                {
                    summary += $"time_scale={timeScale:0.##}\n";
                }
                var fullPath = Path.GetFullPath(summaryPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, summary);

                Debug.Log(summary);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static string GetArg(string[] args, string key, string defaultValue)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return defaultValue;
        }

        private static int GetArgInt(string[] args, string key, int defaultValue)
        {
            var value = GetArg(args, key, defaultValue.ToString());
            return int.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        private static float GetArgFloat(string[] args, string key, float defaultValue)
        {
            var value = GetArg(args, key, defaultValue.ToString("0.0"));
            return float.TryParse(value, out var parsed) ? parsed : defaultValue;
        }
    }
}
