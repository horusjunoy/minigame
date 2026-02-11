using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class BuildScripts
    {
        public static void BuildClient()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = "artifacts/builds/client/MinigameClient.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            ExitWithReport(report);
        }

        public static void BuildServer()
        {
            Directory.CreateDirectory("artifacts/builds/server");
            var headless = !string.Equals(Environment.GetEnvironmentVariable("BUILD_SERVER_HEADLESS"), "0", StringComparison.OrdinalIgnoreCase);
            if (headless && !HasServerPlayer())
            {
                Debug.LogWarning("Server player module nao encontrado. Fazendo build nao-headless.");
                headless = false;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = "artifacts/builds/server/MinigameServer.exe",
                target = BuildTarget.StandaloneWindows64,
                options = headless ? BuildOptions.EnableHeadlessMode : BuildOptions.None
            });

            ExitWithReport(report);
        }

        private static bool HasServerPlayer()
        {
            var editorPath = EditorApplication.applicationPath;
            if (string.IsNullOrWhiteSpace(editorPath))
            {
                return false;
            }

            var editorDir = Path.GetDirectoryName(editorPath);
            if (string.IsNullOrWhiteSpace(editorDir))
            {
                return false;
            }

            var serverPlayer = Path.Combine(editorDir, "Data", "PlaybackEngines", "WindowsStandaloneSupport", "Variations", "win64_server_nondevelopment_mono", "WindowsPlayer.exe");
            return File.Exists(serverPlayer);
        }

        private static void ExitWithReport(BuildReport report)
        {
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build failed: {report.summary.result}");
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }
    }
}
