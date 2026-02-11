using System;
using System.Collections;
using System.Reflection;
using Game.Client;
using Game.Network;
using Game.Network.Transport.Mirror;
using Game.Server;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class NetworkSmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator NetworkFacade_Handshake_Completes()
        {
            NetworkSmokeProbe.ResetResult();

            var facadeObject = new GameObject("NetworkFacade");
            var facade = facadeObject.AddComponent<MirrorNetworkFacade>();

            var serverObject = new GameObject("ServerNetwork");
            var server = serverObject.AddComponent<ServerNetworkBootstrap>();

            var clientObject = new GameObject("ClientNetwork");
            var client = clientObject.AddComponent<ClientNetworkBootstrap>();

            var probeObject = new GameObject("NetworkSmokeProbe");
            var probe = probeObject.AddComponent<NetworkSmokeProbe>();

            SetPrivateField(server, "facadeBehaviour", facade);
            SetPrivateField(client, "facadeBehaviour", facade);
            SetPrivateField(probe, "facadeBehaviour", facade);

            const float timeoutSeconds = 10f;
            var start = Time.realtimeSinceStartup;

            while (!NetworkSmokeProbe.LastSuccess && Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                yield return null;
            }

            Assert.IsTrue(NetworkSmokeProbe.LastSuccess, $"Handshake did not complete. LastMessage={NetworkSmokeProbe.LastMessage}");

            UnityEngine.Object.Destroy(probeObject);
            UnityEngine.Object.Destroy(clientObject);
            UnityEngine.Object.Destroy(serverObject);
            UnityEngine.Object.Destroy(facadeObject);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}.");
            }

            field.SetValue(target, value);
        }
    }
}
