using System;
using System.Collections;
using System.Reflection;
using Game.Core;
using Game.Runtime;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkSmokeSequencer : MonoBehaviour
    {
        [SerializeField] private float clientDelaySeconds = 0.25f;

        private IEnumerator Start()
        {
            if (TryStartHost())
            {
                yield return new WaitForSeconds(0.5f);
                TrySendHello();
                yield break;
            }

            var serverType = Type.GetType("Game.Server.ServerNetworkBootstrap, Game.Server");
            var clientType = Type.GetType("Game.Client.ClientNetworkBootstrap, Game.Client");

            if (serverType == null || clientType == null)
            {
                Debug.LogError("NetworkSmokeSequencer: missing bootstrap types.");
                yield break;
            }

            var server = FindObjectOfType(serverType) as MonoBehaviour;
            var client = FindObjectOfType(clientType) as MonoBehaviour;

            Debug.Log("NetworkSmokeSequencer: starting server");
            InvokeIfPresent(serverType, server, "StartServer");
            if (clientDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(clientDelaySeconds);
            }

            Debug.Log("NetworkSmokeSequencer: starting client");
            InvokeIfPresent(clientType, client, "StartClient");
        }

        private static bool TryStartHost()
        {
            var facadeType = Type.GetType("Game.Network.Transport.Mirror.MirrorNetworkFacade, Game.Network.Transport.Mirror");
            if (facadeType == null)
            {
                return false;
            }

            var facade = FindObjectOfType(facadeType) as MonoBehaviour;
            if (facade == null)
            {
                return false;
            }

            var field = facadeType.GetField("networkManager", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            var networkManager = field.GetValue(facade);
            if (networkManager == null)
            {
                return false;
            }

            var startHost = networkManager.GetType().GetMethod("StartHost", BindingFlags.Instance | BindingFlags.Public);
            if (startHost == null)
            {
                return false;
            }

            Debug.Log("NetworkSmokeSequencer: starting host");
            startHost.Invoke(networkManager, null);
            return true;
        }

        private static void TrySendHello()
        {
            var facadeType = Type.GetType("Game.Network.Transport.Mirror.MirrorNetworkFacade, Game.Network.Transport.Mirror");
            var helloType = Type.GetType("Game.Network.HelloMessage, Game.Network");
            if (facadeType == null || helloType == null)
            {
                return;
            }

            var facade = FindObjectOfType(facadeType);
            if (facade == null)
            {
                return;
            }

            var clientProperty = facadeType.GetProperty("Client", BindingFlags.Instance | BindingFlags.Public);
            var client = clientProperty?.GetValue(facade, null);
            if (client == null)
            {
                return;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var (contentVersion, schemaVersion) = ResolveStubContentVersion();
            var helloMessage = Activator.CreateInstance(
                helloType,
                sessionId,
                BuildInfo.BuildVersion,
                NetworkProtocol.Version,
                contentVersion,
                schemaVersion,
                string.Empty,
                HelloMessage.Version);
            var sendHello = client.GetType().GetMethod("SendHello", BindingFlags.Instance | BindingFlags.Public);
            if (sendHello != null)
            {
                Debug.Log("NetworkSmokeSequencer: sending hello");
                sendHello.Invoke(client, new[] { helloMessage });
            }
        }

        private static (string contentVersion, int schemaVersion) ResolveStubContentVersion()
        {
            var rootPath = System.IO.Path.Combine(Application.dataPath, "Game", "Minigames");
            var catalog = MinigameCatalog.LoadFromDirectory(rootPath);
            var manifest = catalog?.GetById("stub_v1");
            if (manifest == null)
            {
                return (string.Empty, 1);
            }

            return (manifest.content_version ?? string.Empty, manifest.schema_version);
        }

        private static void InvokeIfPresent(Type type, object target, string methodName)
        {
            if (target == null)
            {
                return;
            }

            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }
    }
}
