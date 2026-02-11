using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class NetworkSmokeBuild
    {
        public static void BuildSmokePlayer()
        {
            var scenePath = "Assets/Scenes/NetworkSmokeRuntime.unity";
            EnsureScene(scenePath);

            var repoRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            var outputDir = Path.Combine(repoRoot, "artifacts", "playmode-smoke");
            Directory.CreateDirectory(outputDir);
            var exePath = Path.Combine(outputDir, "NetworkSmoke.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"NetworkSmokeBuild failed: {report.summary.result}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"NetworkSmokeBuild succeeded: {exePath}");
            EditorApplication.Exit(0);
        }

        private static void EnsureScene(string scenePath)
        {
            if (File.Exists(scenePath))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("NetworkSmokeRuntime");
            root.AddComponent<Game.Network.NetworkSmokeRuntimeRunner>();
            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }
}
