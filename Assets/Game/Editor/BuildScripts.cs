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
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = "artifacts/builds/server/MinigameServer.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.EnableHeadlessMode
            });

            ExitWithReport(report);
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
