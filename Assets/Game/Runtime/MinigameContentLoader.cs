using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.Runtime
{
    public sealed class MinigameContentLoader
    {
        private readonly List<AsyncOperationHandle> _assetHandles = new List<AsyncOperationHandle>();
        private readonly List<AsyncOperationHandle<SceneInstance>> _sceneHandles = new List<AsyncOperationHandle<SceneInstance>>();
        private readonly IRuntimeLogger _logger;
        private readonly TelemetryContext _telemetry;

        public MinigameContentLoader(IRuntimeLogger logger, TelemetryContext telemetry)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        public void LoadAllBlocking(MinigameManifest manifest)
        {
            MinigameMemoryProfiler.LogSnapshot(_logger, _telemetry, "before_load");
            if (manifest?.addressables == null)
            {
                LogLoaded(0, 0, manifest != null ? manifest.content_version : string.Empty);
                return;
            }

            var sceneCount = 0;
            var prefabCount = 0;

            if (manifest.addressables.scenes != null)
            {
                foreach (var scene in manifest.addressables.scenes)
                {
                    if (string.IsNullOrWhiteSpace(scene))
                    {
                        continue;
                    }

                    var handle = UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(scene);
                    handle.WaitForCompletion();
                    _sceneHandles.Add(handle);
                    sceneCount += 1;
                }
            }

            if (manifest.addressables.prefabs != null)
            {
                foreach (var prefab in manifest.addressables.prefabs)
                {
                    if (string.IsNullOrWhiteSpace(prefab))
                    {
                        continue;
                    }

                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(prefab);
                    handle.WaitForCompletion();
                    _assetHandles.Add(handle);
                    prefabCount += 1;
                }
            }

            LogLoaded(sceneCount, prefabCount, manifest.content_version);
            MinigameMemoryProfiler.LogSnapshot(_logger, _telemetry, "after_load");
        }

        public void UnloadAll()
        {
            MinigameMemoryProfiler.LogSnapshot(_logger, _telemetry, "before_unload");
            for (var i = 0; i < _sceneHandles.Count; i++)
            {
                if (_sceneHandles[i].IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(_sceneHandles[i], true);
                }
            }

            for (var i = 0; i < _assetHandles.Count; i++)
            {
                if (_assetHandles[i].IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(_assetHandles[i]);
                }
            }

            _sceneHandles.Clear();
            _assetHandles.Clear();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            MinigameMemoryProfiler.LogSnapshot(_logger, _telemetry, "after_unload");
            _logger.Log(LogLevel.Info, "minigame_content_unloaded", "Minigame content unloaded", null, _telemetry);
        }

        private void LogLoaded(int scenes, int prefabs, string contentVersion)
        {
            var fields = new Dictionary<string, object>
            {
                ["scenes"] = scenes,
                ["prefabs"] = prefabs,
                ["content_version"] = contentVersion ?? string.Empty
            };
            _logger.Log(LogLevel.Info, "minigame_content_loaded", "Minigame content loaded", fields, _telemetry);
        }
    }
}
