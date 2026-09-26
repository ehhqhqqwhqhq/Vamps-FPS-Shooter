using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Vamp.Combat;
using Vamp.Core;

namespace Vamp.Online
{
    /// <summary>
    /// Creates the persistent NetworkManager (Unity Transport, used through Unity Relay so hosts don't need port
    /// forwarding), registers the network prefabs and plugs <see cref="OnlineService"/> into Game.Online.
    /// </summary>
    public static class OnlineBootstrap
    {
        public static GameObject PlayerPrefab { get; private set; }
        public static GameObject SessionPrefab { get; private set; }
        public static GameObject BotPrefab { get; private set; }
        public static NetworkManager Manager { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { PlayerPrefab = null; SessionPrefab = null; BotPrefab = null; Manager = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            PlayerPrefab = Resources.Load<GameObject>("VampNetPlayer");
            SessionPrefab = Resources.Load<GameObject>("VampNetSession");
            BotPrefab = Resources.Load<GameObject>("VampNetBot");
            if (PlayerPrefab == null || SessionPrefab == null)
            {
                Debug.LogWarning("[VAMP] Online disabled: network prefabs missing. Run VAMP ▸ Build All Scenes (or VAMP ▸ Build Online Prefabs).");
                return;
            }

            Application.runInBackground = true; // the host must keep simulating when alt-tabbed
            var go = new GameObject("[VAMP Network]");
            Object.DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            Manager = go.AddComponent<NetworkManager>();
            Manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30,
                ClientConnectionBufferTimeout = 15
            };
            Manager.AddNetworkPrefab(PlayerPrefab);
            Manager.AddNetworkPrefab(SessionPrefab);
            if (BotPrefab != null) Manager.AddNetworkPrefab(BotPrefab);
            else Debug.LogWarning("[VAMP] Resources/VampNetBot missing - quick match can't fill empty slots with bots (VAMP ▸ Build Online Prefabs).");

            HealthController.Intercept = NetPlayer.InterceptDamage;
            VFX.CosmeticFx.KillFxResolver = killer =>
            {
                var np = killer != null ? killer.GetComponent<NetPlayer>() : null;
                return np != null && np.IsSpawned ? np.KillFx : null;
            };
            Game.Online = new OnlineService(Manager);
            Game.Leaderboard = new OnlineLeaderboard();
            go.AddComponent<OnlineRunner>();
        }

        /// <param name="matched">Joining through matchmaking (quick match / ranked) rather than an invite.</param>
        public static byte[] Payload(string name, int level, bool matched = false)
        {
            return Encoding.UTF8.GetBytes(Application.version + "|" + (name ?? "PLAYER").Replace("|", "") + "|" + level + "|" + (matched ? "m" : "p"));
        }

        public static bool ParsePayload(byte[] data, out string version, out string name, out int level)
        {
            bool matched;
            return ParsePayload(data, out version, out name, out level, out matched);
        }

        public static bool ParsePayload(byte[] data, out string version, out string name, out int level, out bool matched)
        {
            version = ""; name = "PLAYER"; level = 1; matched = false;
            if (data == null || data.Length == 0 || data.Length > 256) return false;
            var parts = Encoding.UTF8.GetString(data).Split('|');
            if (parts.Length < 3) return false;
            version = parts[0];
            name = parts[1];
            int.TryParse(parts[2], out level);
            matched = parts.Length > 3 && parts[3] == "m";
            return true;
        }
    }
}
