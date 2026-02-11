using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Network;
using Game.Network.Transport.Mirror;
using Game.Server;
using Game.Client;

namespace Game.Editor
{
    public static class NetworkSmokeBatchRunner
    {
        private static double _startTime;

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
            var sequencer = probeObject.AddComponent<NetworkSmokeSequencer>();

            SetObjectReference(server, "facadeBehaviour", facade);
            SetObjectReference(client, "facadeBehaviour", facade);
            SetObjectReference(probe, "facadeBehaviour", facade);
            SetFloat(probe, "timeoutSeconds", 20f);
            SetFloat(sequencer, "clientDelaySeconds", 1f);
            SetBool(server, "autoStart", false);
            SetBool(client, "autoStart", false);
            SetBool(server, "allowEmptyJoinToken", true);

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnEditorUpdate()
        {
            const double timeoutSeconds = 20.0;
            if (EditorApplication.timeSinceStartup - _startTime > timeoutSeconds)
            {
                Debug.LogError("NetworkSmokeBatchRunner: timeout waiting for playmode.");
                EditorApplication.Exit(1);
            }
        }

        private static void SetObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"NetworkSmokeBatchRunner: missing field {fieldName} on {target.GetType().Name}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string fieldName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"NetworkSmokeBatchRunner: missing field {fieldName} on {target.GetType().Name}");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string fieldName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"NetworkSmokeBatchRunner: missing field {fieldName} on {target.GetType().Name}");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
