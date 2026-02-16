using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Game.Core;
using UnityEngine;

namespace Game.Runtime
{
    public static class ContentDistribution
    {
        public static bool TryResolveContent(
            string minigameId,
            string indexPath,
            IRuntimeLogger logger,
            TelemetryContext telemetry,
            out ContentEntry entry,
            out string cachedPath)
        {
            entry = null;
            cachedPath = null;

            if (string.IsNullOrWhiteSpace(minigameId) || string.IsNullOrWhiteSpace(indexPath))
            {
                return false;
            }

            if (!TryLoadIndex(indexPath, out var index))
            {
                logger?.Log(LogLevel.Warn, "content_index_missing", $"Content index not found: {indexPath}", null, telemetry);
                return false;
            }

            if (index.items == null)
            {
                return false;
            }

            for (var i = 0; i < index.items.Length; i++)
            {
                var item = index.items[i];
                if (item == null || !string.Equals(item.minigame_id, minigameId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry = item;
                cachedPath = TryCacheContent(item, logger, telemetry);
                return true;
            }

            return false;
        }

        private static bool TryLoadIndex(string path, out ContentIndex index)
        {
            index = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var json = File.ReadAllText(path);
                index = JsonUtility.FromJson<ContentIndex>(json);
                return index != null;
            }
            catch
            {
                return false;
            }
        }

        private static string TryCacheContent(ContentEntry entry, IRuntimeLogger logger, TelemetryContext telemetry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.url))
            {
                return null;
            }

            var start = DateTime.UtcNow;
            var cacheDir = Path.Combine(Application.persistentDataPath, "content_cache", entry.minigame_id ?? "unknown", entry.content_version ?? "unknown");
            Directory.CreateDirectory(cacheDir);
            var targetPath = Path.Combine(cacheDir, "content_catalog.json");

            try
            {
                var sourcePath = NormalizePath(entry.url);
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    logger?.Log(LogLevel.Warn, "content_download_failed", $"Content source missing: {entry.url}", null, telemetry);
                    return null;
                }

                File.Copy(sourcePath, targetPath, true);
                if (!ValidateHash(targetPath, entry.sha256))
                {
                    logger?.Log(LogLevel.Warn, "content_hash_mismatch", "Content hash mismatch", new { entry.minigame_id, entry.content_version }, telemetry);
                    return null;
                }

                var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;
                logger?.Log(LogLevel.Info, "content_download_ok", "Content cached", new { entry.minigame_id, entry.content_version, duration_ms = durationMs }, telemetry);
                return targetPath;
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Warn, "content_download_failed", ex.Message, null, telemetry);
                return null;
            }
        }

        private static string NormalizePath(string url)
        {
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring("file://".Length);
            }

            return url;
        }

        private static bool ValidateHash(string path, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return true;
            }

            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(path);
                var hashBytes = sha.ComputeHash(stream);
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                return string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        [Serializable]
        public sealed class ContentIndex
        {
            public string generated_at;
            public ContentEntry[] items;
        }

        [Serializable]
        public sealed class ContentEntry
        {
            public string minigame_id;
            public string content_version;
            public string url;
            public string sha256;
        }
    }
}
