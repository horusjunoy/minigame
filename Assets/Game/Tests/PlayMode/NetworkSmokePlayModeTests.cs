using System;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Client;
using Game.Core;
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
        public IEnumerator NetworkFacade_Handshake_Rejects_BuildMismatch()
        {
            yield return RunManualHelloRejectionCase(
                protocolVersion: NetworkProtocol.Version,
                contentVersion: "0.1.0",
                schemaVersion: 1,
                clientBuildVersion: "__invalid_build__",
                expectedCode: "build_mismatch");
        }

        [UnityTest]
        public IEnumerator NetworkFacade_Handshake_Rejects_ManifestMissing()
        {
            yield return RunManualHelloRejectionCase(
                protocolVersion: NetworkProtocol.Version,
                contentVersion: "0.1.0",
                schemaVersion: 1,
                clientBuildVersion: BuildInfo.BuildVersion,
                expectedCode: "manifest_missing",
                serverMinigameId: "__missing_manifest__");
        }

        [UnityTest]
        public IEnumerator NetworkFacade_Handshake_Rejects_TokenMissing()
        {
            yield return RunManualHelloDisconnectCase(
                NetworkProtocol.Version,
                "0.1.0",
                1,
                BuildInfo.BuildVersion,
                string.Empty);
        }

        [UnityTest]
        public IEnumerator NetworkFacade_Handshake_Rejects_TokenReplay()
        {
            const string keyMaterial = "__test_signing_key__";
            const string matchId = "m_local";
            var signedProof = CreateJoinToken(keyMaterial, matchId, "p_replay");

            // First handshake with fresh token should succeed.
            yield return RunManualHelloWithOutcome(
                NetworkProtocol.Version,
                "0.1.0",
                1,
                BuildInfo.BuildVersion,
                signedProof,
                false,
                keyMaterial,
                true);

            // Reusing the same token should be rejected.
            yield return RunManualHelloWithOutcome(
                NetworkProtocol.Version,
                "0.1.0",
                1,
                BuildInfo.BuildVersion,
                signedProof,
                false,
                keyMaterial,
                false);
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

                ServerErrorMessage? serverError = null;
                var welcomeReceived = false;
                client.ErrorReceived += message => serverError = message;
                client.WelcomeReceived += _ => welcomeReceived = true;

                const float timeoutSeconds = 10f;
                var start = Time.realtimeSinceStartup;
                while (!serverError.HasValue && Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    yield return null;
                }

                Assert.IsTrue(serverError.HasValue, $"Expected server error '{expectedCode}' but no error was received.");
                Assert.AreEqual(expectedCode, serverError.Value.code);
                Assert.IsFalse(welcomeReceived, "Welcome should not be received for rejected handshake.");
            }
            finally
            {
                UnityEngine.Object.Destroy(clientObject);
                UnityEngine.Object.Destroy(serverObject);
                UnityEngine.Object.Destroy(facadeObject);
            }
        }

        private static IEnumerator RunManualHelloRejectionCase(
            int protocolVersion,
            string contentVersion,
            int schemaVersion,
            string clientBuildVersion,
            string expectedCode,
            string serverMinigameId = null)
        {
            var facadeObject = new GameObject("NetworkFacade");
            var serverObject = new GameObject("ServerNetwork");

            var facade = facadeObject.AddComponent<MirrorNetworkFacade>();
            var server = serverObject.AddComponent<ServerNetworkBootstrap>();

            try
            {
                SetPrivateField(server, "facadeBehaviour", facade);
                SetPrivateField(server, "allowEmptyJoinToken", true);
                if (!string.IsNullOrWhiteSpace(serverMinigameId))
                {
                    SetPrivateField(server, "minigameId", serverMinigameId);
                }

                ServerErrorMessage? serverError = null;
                var welcomeReceived = false;
                var connected = false;
                var sentHello = false;

                facade.Client.ErrorReceived += message => serverError = message;
                facade.Client.WelcomeReceived += _ => welcomeReceived = true;
                facade.Client.Connected += () => connected = true;

                facade.Client.StartClient(new NetworkEndpoint("127.0.0.1", 7770));

                const float timeoutSeconds = 10f;
                var start = Time.realtimeSinceStartup;
                while (!connected && Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    yield return null;
                }

                Assert.IsTrue(connected, "Client did not connect.");

                var hello = new HelloMessage(
                    Guid.NewGuid().ToString("N"),
                    clientBuildVersion,
                    protocolVersion,
                    contentVersion,
                    schemaVersion,
                    string.Empty);
                facade.Client.SendHello(hello);
                sentHello = true;

                while (!serverError.HasValue && Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    yield return null;
                }

                Assert.IsTrue(sentHello, "Hello should be sent in manual handshake case.");
                Assert.IsTrue(serverError.HasValue, $"Expected server error '{expectedCode}' but no error was received.");
                Assert.AreEqual(expectedCode, serverError.Value.code);
                Assert.IsFalse(welcomeReceived, "Welcome should not be received for rejected handshake.");
            }
            finally
            {
                facade?.Client?.StopClient();
                server?.StopServer();
                UnityEngine.Object.Destroy(serverObject);
                UnityEngine.Object.Destroy(facadeObject);
            }
        }

        private static IEnumerator RunManualHelloDisconnectCase(
            int protocolVersion,
            string contentVersion,
            int schemaVersion,
            string clientBuildVersion,
            string joinPayload)
        {
            yield return RunManualHelloWithOutcome(
                protocolVersion,
                contentVersion,
                schemaVersion,
                clientBuildVersion,
                joinPayload,
                false,
                "__token_missing_key__",
                false);
        }

        private static IEnumerator RunManualHelloWithOutcome(
            int protocolVersion,
            string contentVersion,
            int schemaVersion,
            string clientBuildVersion,
            string joinToken,
            bool allowEmptyJoinToken,
            string matchmakerSecret,
            bool expectWelcome)
        {
            var facadeObject = new GameObject("NetworkFacade");
            var serverObject = new GameObject("ServerNetwork");

            var facade = facadeObject.AddComponent<MirrorNetworkFacade>();
            var server = serverObject.AddComponent<ServerNetworkBootstrap>();

            var priorKey = Environment.GetEnvironmentVariable("MATCHMAKER_SECRET");
            Environment.SetEnvironmentVariable("MATCHMAKER_SECRET", null);

            try
            {
                SetPrivateField(server, "facadeBehaviour", facade);
                SetPrivateField(server, "allowEmptyJoinToken", allowEmptyJoinToken);
                SetPrivateField(server, "matchmakerSecret", matchmakerSecret);

                var connected = false;
                var disconnected = false;
                var welcomeReceived = false;
                facade.Client.Connected += () => connected = true;
                facade.Client.Disconnected += () => disconnected = true;
                facade.Client.WelcomeReceived += _ => welcomeReceived = true;

                facade.Client.StartClient(new NetworkEndpoint("127.0.0.1", 7770));

                const float timeoutSeconds = 10f;
                var start = Time.realtimeSinceStartup;
                while (!connected && Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    yield return null;
                }

                Assert.IsTrue(connected, "Client did not connect.");

                var hello = new HelloMessage(
                    Guid.NewGuid().ToString("N"),
                    clientBuildVersion,
                    protocolVersion,
                    contentVersion,
                    schemaVersion,
                    joinToken ?? string.Empty);
                facade.Client.SendHello(hello);

                while (!disconnected && !welcomeReceived && Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    yield return null;
                }

                if (expectWelcome)
                {
                    Assert.IsTrue(welcomeReceived, "Welcome should be received.");
                }
                else
                {
                    Assert.IsFalse(welcomeReceived, "Welcome should not be received.");
                    Assert.IsTrue(disconnected, "Client should disconnect when token is rejected.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MATCHMAKER_SECRET", priorKey);
                facade?.Client?.StopClient();
                server?.StopServer();
                UnityEngine.Object.Destroy(serverObject);
                UnityEngine.Object.Destroy(facadeObject);
            }
        }

        private static string CreateJoinToken(string secret, string matchId, string playerId)
        {
            var payload = JsonUtility.ToJson(new MatchmakerTokenVerifier.TokenPayload
            {
                match_id = matchId,
                player_id = playerId,
                exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()
            });

            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var encodedPayload = Base64UrlEncode(payloadBytes);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty));
            var signatureBytes = hmac.ComputeHash(payloadBytes);
            var encodedSignature = Base64UrlEncode(signatureBytes);
            return encodedPayload + "." + encodedSignature;
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
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
