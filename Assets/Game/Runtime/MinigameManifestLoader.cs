using System.IO;
using UnityEngine;

namespace Game.Runtime
{
    public static class MinigameManifestLoader
    {
        public static MinigameManifest LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"Manifest file not found: {path}");
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<MinigameManifest>(json);
        }
    }
}
