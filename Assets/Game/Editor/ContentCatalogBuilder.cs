using System;
using System.IO;
using Game.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ContentCatalogBuilder
    {
        public static void Build()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var minigameId = GetArg(args, "-minigame", Environment.GetEnvironmentVariable("CONTENT_MINIGAME_ID"));
                var outputRoot = GetArg(args, "-outputRoot", Environment.GetEnvironmentVariable("CONTENT_OUTPUT_ROOT"));

                if (string.IsNullOrWhiteSpace(outputRoot))
                {
                    outputRoot = Path.Combine(Path.GetDirectoryName(Application.dataPath), "artifacts", "content");
                }

                var root = Path.Combine(Application.dataPath, "Game", "Minigames");
                if (!Directory.Exists(root))
                {
                    Debug.LogError("ContentCatalogBuilder: Minigames folder not found.");
                    EditorApplication.Exit(1);
                    return;
                }

                var files = Directory.GetFiles(root, "*.manifest.json", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    Debug.LogError("ContentCatalogBuilder: no manifest files found.");
                    EditorApplication.Exit(1);
                    return;
                }

                var failures = 0;
                foreach (var file in files)
                {
                    var manifest = MinigameManifestLoader.LoadFromFile(file);
                    if (manifest == null)
                    {
                        Debug.LogError($"ContentCatalogBuilder: manifest parse failed {file}");
                        failures += 1;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(minigameId) &&
                        !string.Equals(manifest.id, minigameId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(manifest.content_version))
                    {
                        Debug.LogError($"ContentCatalogBuilder: content_version missing for {manifest.id}");
                        failures += 1;
                        continue;
                    }

                    var outputDir = Path.Combine(outputRoot, manifest.id, manifest.content_version);
                    Directory.CreateDirectory(outputDir);
                    var catalog = new MinigameContentCatalog
                    {
                        id = manifest.id,
                        version = manifest.version,
                        content_version = manifest.content_version,
                        addressables = manifest.addressables
                    };

                    var json = JsonUtility.ToJson(catalog, true);
                    var outputPath = Path.Combine(outputDir, "content_catalog.json");
                    File.WriteAllText(outputPath, json);
                    Debug.Log($"ContentCatalogBuilder: wrote {outputPath}");
                }

                EditorApplication.Exit(failures > 0 ? 1 : 0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static string GetArg(string[] args, string key, string fallback)
        {
            if (args != null)
            {
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    {
                        return args[i + 1];
                    }
                }
            }

            return fallback;
        }

        [Serializable]
        private sealed class MinigameContentCatalog
        {
            public string id;
            public string version;
            public string content_version;
            public AddressablesConfig addressables;
        }
    }
}
