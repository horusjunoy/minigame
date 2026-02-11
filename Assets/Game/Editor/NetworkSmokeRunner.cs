using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Network;
using Game.Network.Transport.Mirror;
using Game.Server;
using Game.Client;

namespace Game.Editor
{
    public static class NetworkSmokeRunner
    {
        public static void Run()
        {
            NetworkSmokeProbe.ResetResult();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var facadeObject = new GameObject("NetworkFacade");
            var facade = facadeObject.AddComponent<MirrorNetworkFacade>();

            var serverObject = new GameObject("ServerNetwork");
            var server = serverObject.AddComponent<ServerNetworkBootstrap>();

            var clientObject = new GameObject("ClientNetwork");
            var client = clientObject.AddComponent<ClientNetworkBootstrap>();

            var probeObject = new GameObject("NetworkSmokeProbe");
            var probe = probeObject.AddComponent<NetworkSmokeProbe>();

            SetObjectReference(server, "facadeBehaviour", facade);
            SetObjectReference(client, "facadeBehaviour", facade);
            SetObjectReference(probe, "facadeBehaviour", facade);
            SetBool(server, "allowEmptyJoinToken", true);

            EditorApplication.isPlaying = true;
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"NetworkSmokeRunner: missing field {fieldName} on {target.GetType().Name}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"NetworkSmokeRunner: missing field {fieldName} on {target.GetType().Name}");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
