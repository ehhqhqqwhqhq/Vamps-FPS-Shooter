using UnityEngine;
using UnityEngine.SceneManagement;
using Vamp.Accounts;
using Vamp.Audio;
using Vamp.Customization;
using Vamp.Graphics;
using Vamp.Maps;
using Vamp.Matchmaking;
using Vamp.Progression;
using Vamp.Settings;
using Vamp.Social;
using Vamp.UI;
using Vamp.Weapons;

namespace Vamp.Core
{
    /// <summary>
    /// Global access to VAMP's services. Created exactly once by <see cref="GameRoot"/> (auto-spawned before the
    /// first scene loads, so pressing Play in ANY scene works). No duplicate global systems: everything lives on the
    /// single persistent [VAMP] object.
    /// </summary>
    public static class Game
    {
        public static bool Initialized { get; private set; }
        public static NotificationService Notifications { get; private set; }
        public static SettingsController Settings { get; private set; }
        public static IAccountService Accounts { get; private set; }
        public static ProgressionService Progression { get; private set; }
        public static CustomizationService Customization { get; private set; }
        public static ISocialService Social { get; private set; }
        /// <summary>Set by the online assembly (before Init) to provide the online friends service.</summary>
        public static System.Func<IAccountService, ISocialService> SocialFactory;
        public static IPartyService Party { get; private set; }
        public static MatchmakingService Matchmaking { get; private set; }
        public static WeaponCatalog Weapons { get; private set; }
        public static SceneFlow Scenes { get; private set; }
        /// <summary>Online multiplayer (set by the Vamp.Online assembly; null if it isn't installed).</summary>
        public static IOnlineService Online { get; set; }

        public static bool IsLoggedIn { get { return Accounts != null && Accounts.IsLoggedIn; } }
        public static string Username { get { return IsLoggedIn ? Accounts.Current.username : "GUEST"; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Initialized = false;
            Notifications = null; Settings = null; Accounts = null; Progression = null; Customization = null;
            Social = null; Party = null; Matchmaking = null; Weapons = null; Scenes = null; Online = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateRoot()
        {
            if (Object.FindAnyObjectByType<GameRoot>() != null) return;
            new GameObject("[VAMP]").AddComponent<GameRoot>();
        }

        internal static void Init(GameRoot root)
        {
            if (Initialized) return;

            Notifications = new NotificationService();
            Weapons = WeaponCatalog.Load();

            Settings = new SettingsController();
            Settings.LoadDevice();

            var accounts = new LocalAccountService();
            Accounts = accounts;
            // Order matters: settings/progression/social create their data when an account is created,
            // then load it when the session starts.
            accounts.AccountCreated += a => Settings.CreateForNewAccount(a.id);
            Progression = new ProgressionService(accounts);
            Customization = new CustomizationService(Progression);
            Social = SocialFactory != null ? SocialFactory(accounts) : new OfflineSocialService(accounts);
            Party = new LocalPartyService(accounts, Progression);
            Matchmaking = new MatchmakingService(Party);
            Matchmaking.Game_Notify = () => Notifications.Push(NotificationKind.MatchFound, "MATCH FOUND", "");
            accounts.LoggedIn += a => Settings.LoadForAccount(a.id);
            accounts.LoggedOut += () => Settings.LoadDevice();

            // Persistent helpers (children of the root)
            root.gameObject.AddComponent<FramerateController>();
            var audio = new GameObject("Audio");
            audio.transform.SetParent(root.transform, false);
            audio.AddComponent<AudioController>();

            UIKit.EnsureEventSystem();
            var loading = root.gameObject.AddComponent<LoadingScreenView>();
            root.gameObject.AddComponent<NotificationToaster>();
            root.gameObject.AddComponent<PerformanceOverlay>();
            Scenes = new SceneFlow(root, loading);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Initialized = true;

            accounts.TryResumeSession(); // Remember Me
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Initialized) return;
            bool menu = scene.name == MapCatalog.MainMenuScene || scene.name == MapCatalog.BootScene;
            FramerateController.InMenu = menu;
            if (Settings != null) GraphicsSettingsApplier.ApplyToCameras(Settings.Current.graphics);
            if (menu) AudioController.SetMusic("menu", 0.7f);
            else AudioController.SetMusic("match", 0.25f);
        }

        internal static void Shutdown()
        {
            if (Progression != null) Progression.Save();
            GraphicsSettingsApplier.RestoreOriginal();
        }
    }
}
