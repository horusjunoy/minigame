using System;
using System.Reflection;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkSmokeRuntimeRunner : MonoBehaviour
    {
        [SerializeField] private float timeoutSeconds = 10f;

        private void Start()
        {
            NetworkSmokeProbe.ResetResult();

            var facadeType = Type.GetType("Game.Network.Transport.Mirror.MirrorNetworkFacade, Game.Network.Transport.Mirror");
            var serverType = Type.GetType("Game.Server.ServerNetworkBootstrap, Game.Server");
            var clientType = Type.GetType("Game.Client.ClientNetworkBootstrap, Game.Client");

            if (facadeType == null || serverType == null || clientType == null)
            {
                Debug.LogError("NetworkSmokeRuntimeRunner: missing network types. Check asmdef references.");
                return;
            }

            var facadeObject = new GameObject("NetworkFacade");
            var facade = facadeObject.AddComponent(facadeType);

            var serverObject = new GameObject("ServerNetwork");
            var server = serverObject.AddComponent(serverType);

            var clientObject = new GameObject("ClientNetwork");
            var client = clientObject.AddComponent(clientType);

            var probeObject = new GameObject("NetworkSmokeProbe");
            var probe = probeObject.AddComponent<NetworkSmokeProbe>();

            SetPrivateField(server, "facadeBehaviour", facade);
            SetPrivateField(client, "facadeBehaviour", facade);
            SetPrivateField(probe, "facadeBehaviour", facade);
            SetPrivateField(probe, "timeoutSeconds", timeoutSeconds);
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
