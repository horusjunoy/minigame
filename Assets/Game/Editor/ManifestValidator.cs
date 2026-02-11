using System;
using System.IO;
using Game.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ManifestValidator
    {
        public static void Run()
        {
            try
            {
                var root = Path.Combine(Application.dataPath, "Game", "Minigames");
                if (!Directory.Exists(root))
                {
                    Debug.LogError("ManifestValidator: Minigames folder not found.");
                    EditorApplication.Exit(1);
                    return;
                }

                var files = Directory.GetFiles(root, "*.manifest.json", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    Debug.LogError("ManifestValidator: no manifest files found.");
                    EditorApplication.Exit(1);
                    return;
                }

                var failures = 0;
                foreach (var file in files)
                {
                    if (!Validate(file, out var error))
                    {
                        failures += 1;
                        Debug.LogError($"manifest {Path.GetFileName(file)} Failed {error}");
                    }
                    else
                    {
                        Debug.Log($"manifest {Path.GetFileName(file)} Passed");
                    }
                }

                EditorApplication.Exit(failures > 0 ? 1 : 0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static bool Validate(string path, out string error)
        {
            error = null;
            var manifest = MinigameManifestLoader.LoadFromFile(path);
            if (manifest == null)
            {
                error = "manifest_parse_failed";
                return false;
            }

            if (manifest.schema_version <= 0)
            {
                error = "schema_version_invalid";
                return false;
            }
            if (manifest.schema_version != 1)
            {
                error = "schema_version_unsupported";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.id))
            {
                error = "id_missing";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.version))
            {
                error = "version_missing";
                return false;
            }
            if (!TryParseVersion(manifest.version))
            {
                error = "version_invalid";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.content_version))
            {
                error = "content_version_missing";
                return false;
            }
            if (!TryParseVersion(manifest.content_version))
            {
                error = "content_version_invalid";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.server_entry))
            {
                error = "server_entry_missing";
                return false;
            }

            var serverType = Type.GetType(manifest.server_entry);
            if (serverType == null)
            {
                error = "server_entry_invalid";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(manifest.client_entry))
            {
                var clientType = Type.GetType(manifest.client_entry);
                if (clientType == null)
                {
                    error = "client_entry_invalid";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            var core = version.Split('+')[0].Split('-')[0];
            var parts = core.Split('.');
            if (parts.Length < 3)
            {
                return false;
            }

            for (var i = 0; i < 3; i++)
            {
                if (!int.TryParse(parts[i], out _))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
