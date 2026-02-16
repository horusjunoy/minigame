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
        public IEnumerator NetworkFacade_Handshake_Rejects_ProtocolMismatch()
        {
            yield return RunHandshakeRejectionCase(
                NetworkProtocol.Version + 1,
                "0.1.0",
                1,
                "protocol_mismatch");
        }

        [UnityTest]
        public IEnumerator NetworkFacade_Handshake_Rejects_SchemaMismatch()
        {
            yield return RunHandshakeRejectionCase(
                NetworkProtocol.Version,
                "0.1.0",
                2,
                "schema_mismatch");
        }

        [UnityTest]
        public IEnumerator NetworkFacade_Handshake_Rejects_ContentMismatch()
        {
            yield return RunHandshakeRejectionCase(
                NetworkProtocol.Version,
                "__invalid_content__",
                1,
                "content_mismatch");
        }

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
            SetPrivateField(server, "allowEmptyJoinToken", true);

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

        private static IEnumerator RunHandshakeRejectionCase(int protocolVersion, string contentVersion, int schemaVersion, string expectedCode)
        {
            var facadeObject = new GameObject("NetworkFacade");
            var serverObject = new GameObject("ServerNetwork");
            var clientObject = new GameObject("ClientNetwork");

            var facade = facadeObject.AddComponent<MirrorNetworkFacade>();
            var server = serverObject.AddComponent<ServerNetworkBootstrap>();
            var client = clientObject.AddComponent<ClientNetworkBootstrap>();

            try
            {
                SetPrivateField(server, "facadeBehaviour", facade);
                SetPrivateField(client, "facadeBehaviour", facade);
                SetPrivateField(server, "allowEmptyJoinToken", true);
                client.SetVersionInfo(protocolVersion, contentVersion, schemaVersion);

                ServerErrorMessage serverError = null;
                var welcomeReceived = false;
                client.ErrorReceived += message => serverError = message;
                client.WelcomeReceived += _ => welcomeReceived = true;

                const float timeoutSeconds = 10f;
                var start = Time.realtimeSinceStartup;
                while (serverError == null && Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    yield return null;
                }

                Assert.NotNull(serverError, $"Expected server error '{expectedCode}' but no error was received.");
                Assert.AreEqual(expectedCode, serverError.code);
                Assert.IsFalse(welcomeReceived, "Welcome should not be received for rejected handshake.");
            }
            finally
            {
                UnityEngine.Object.Destroy(clientObject);
                UnityEngine.Object.Destroy(serverObject);
                UnityEngine.Object.Destroy(facadeObject);
            }
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
