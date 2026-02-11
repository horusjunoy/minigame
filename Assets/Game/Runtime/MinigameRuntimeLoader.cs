using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    public static class MinigameRuntimeLoader
    {
        public static IMinigame LoadById(string minigameId, string catalogRelativePath, TelemetryContext telemetry, IRuntimeLogger logger, out MinigameManifest manifest)
        {
            return LoadById(minigameId, null, catalogRelativePath, telemetry, logger, out manifest);
        }

        public static IMinigame LoadById(string minigameId, string minigameVersion, string catalogRelativePath, TelemetryContext telemetry, IRuntimeLogger logger, out MinigameManifest manifest)
        {
            manifest = null;
            var rootPath = Path.Combine(Application.dataPath, catalogRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            var catalog = MinigameCatalog.LoadFromDirectory(rootPath);

            var resolvedId = minigameId;
            var resolvedVersion = minigameVersion;
            if (!string.IsNullOrWhiteSpace(minigameId) && minigameId.Contains("@"))
            {
                var parts = minigameId.Split('@');
                if (parts.Length == 2)
                {
                    resolvedId = parts[0];
                    resolvedVersion = string.IsNullOrWhiteSpace(resolvedVersion) ? parts[1] : resolvedVersion;
                }
            }

            manifest = catalog.GetById(resolvedId, resolvedVersion);

            if (manifest == null)
            {
                logger.Log(LogLevel.Error, "minigame_error", $"Manifest not found: {minigameId}", null, telemetry);
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.server_entry))
            {
                logger.Log(LogLevel.Error, "minigame_error", $"Manifest missing server_entry: {minigameId}", null, telemetry);
                return null;
            }

            var minigame = MinigameFactory.CreateFromEntry(manifest.server_entry);
            if (minigame == null)
            {
                logger.Log(LogLevel.Error, "minigame_error", $"Minigame entry invalid: {manifest.server_entry}", null, telemetry);
                return null;
            }

            return minigame;
        }
    }
}
