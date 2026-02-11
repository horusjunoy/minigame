using UnityEngine;

namespace Game.Client
{
    public static class ClientFlowBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindObjectOfType<ClientFlowController>() != null)
            {
                return;
            }

            var flowObject = new GameObject("ClientFlow");
            Object.DontDestroyOnLoad(flowObject);

            var matchmaker = flowObject.AddComponent<MatchmakerClient>();
            var controller = flowObject.AddComponent<ClientFlowController>();
            var network = Object.FindObjectOfType<ClientNetworkBootstrap>();
            if (network != null)
            {
                network.SetAutoStart(false);
                network.StopClient();
            }

            controller.SetReferences(network, matchmaker);
        }
    }
}
