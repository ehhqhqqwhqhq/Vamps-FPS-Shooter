using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Vamp.Online.EditorTools
{
    /// <summary>
    /// Creates the two network prefabs the online mode needs:
    ///  • Resources/VampNetPlayer.prefab  - a VARIANT of Prefabs/Players/Player.prefab + NetworkObject + NetPlayer
    ///    (so every change to the normal player - movement, weapons, character - automatically applies online too)
    ///  • Resources/VampNetSession.prefab - NetworkObject + NetSession (lobby / match state)
    /// Runs automatically when they are missing; VAMP ▸ Build Online Prefabs rebuilds them.
    /// </summary>
    [InitializeOnLoad]
    public static class OnlinePrefabBuilder
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Players/Player.prefab";
        private const string NetPlayerPath = "Assets/Resources/VampNetPlayer.prefab";
        private const string NetSessionPath = "Assets/Resources/VampNetSession.prefab";
        private const string NetBotPath = "Assets/Resources/VampNetBot.prefab";

        static OnlinePrefabBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(NetSessionPath) == null
                    || AssetDatabase.LoadAssetAtPath<GameObject>(NetBotPath) == null)
                    Build();
            };
        }

        [MenuItem("VAMP/Build Online Prefabs", priority = 20)]
        public static void Build()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (basePrefab == null)
            {
                Debug.LogWarning("[VAMP] Online prefabs: " + PlayerPrefabPath + " not found - run VAMP ▸ Build All Scenes first.");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            // Player variant
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            if (inst.GetComponent<NetworkObject>() == null) inst.AddComponent<NetworkObject>();
            if (inst.GetComponent<NetPlayer>() == null) inst.AddComponent<NetPlayer>();
            inst.name = "VampNetPlayer";
            PrefabUtility.SaveAsPrefabAsset(inst, NetPlayerPath);
            Object.DestroyImmediate(inst);

            // Session object
            var s = new GameObject("VampNetSession");
            s.AddComponent<NetworkObject>();
            s.AddComponent<NetSession>();
            PrefabUtility.SaveAsPrefabAsset(s, NetSessionPath);
            Object.DestroyImmediate(s);

            // Bot (quick match backfill): the body is built at spawn time (host = AI, clients = proxy).
            var b = new GameObject("VampNetBot");
            b.AddComponent<NetworkObject>();
            b.AddComponent<NetBot>();
            PrefabUtility.SaveAsPrefabAsset(b, NetBotPath);
            Object.DestroyImmediate(b);

            AssetDatabase.SaveAssets();
            Debug.Log("[VAMP] Online prefabs built: " + NetPlayerPath + ", " + NetSessionPath + ", " + NetBotPath);
        }
    }
}
