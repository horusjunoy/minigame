using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Runtime
{
    public sealed class MinigameCatalog
    {
        private readonly Dictionary<string, List<MinigameManifest>> _manifests = new Dictionary<string, List<MinigameManifest>>();

        public static MinigameCatalog LoadFromDirectory(string rootPath)
        {
            var catalog = new MinigameCatalog();
            if (!Directory.Exists(rootPath))
            {
                Debug.LogError($"Minigame catalog directory not found: {rootPath}");
                return catalog;
            }

            var files = Directory.GetFiles(rootPath, "*.manifest.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var manifest = MinigameManifestLoader.LoadFromFile(file);
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.id))
                {
                    continue;
                }

                catalog.Register(manifest);
            }

            return catalog;
        }

        public void Register(MinigameManifest manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.id))
            {
                return;
            }

            if (!_manifests.TryGetValue(manifest.id, out var list))
            {
                list = new List<MinigameManifest>();
                _manifests[manifest.id] = list;
            }

            list.Add(manifest);
        }

        public MinigameManifest GetById(string minigameId, string version = null)
        {
            if (string.IsNullOrWhiteSpace(minigameId))
            {
                return null;
            }

            if (!_manifests.TryGetValue(minigameId, out var list) || list.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].version, version, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return list[i];
                    }
                }
                return null;
            }

            MinigameManifest best = null;
            for (var i = 0; i < list.Count; i++)
            {
                if (best == null)
                {
                    best = list[i];
                    continue;
                }

                if (CompareVersions(list[i].version, best.version) > 0)
                {
                    best = list[i];
                }
            }

            return best;
        }

        private static int CompareVersions(string left, string right)
        {
            var a = ParseVersion(left);
            var b = ParseVersion(right);
            for (var i = 0; i < 3; i++)
            {
                var diff = a[i].CompareTo(b[i]);
                if (diff != 0)
                {
                    return diff;
                }
            }

            return 0;
        }

        private static int[] ParseVersion(string version)
        {
            var result = new[] { 0, 0, 0 };
            if (string.IsNullOrWhiteSpace(version))
            {
                return result;
            }

            var core = version.Split('+')[0].Split('-')[0];
            var parts = core.Split('.');
            for (var i = 0; i < result.Length && i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out var value))
                {
                    result[i] = value;
                }
            }

            return result;
        }
    }
}
