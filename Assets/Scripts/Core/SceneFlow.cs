using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamp.Maps;
using Vamp.Match;
using Vamp.Social;
using Vamp.UI;

namespace Vamp.Core
{
    /// <summary>
    /// Scene transitions with the loading screen ("LOADING VERTEX... CONNECTING / LOADING MAP / LOADING PLAYERS").
    /// Recoverable failures (missing scene, load error) show an error instead of crashing.
    /// </summary>
    public sealed class SceneFlow
    {
        private readonly MonoBehaviour _host;
        private readonly LoadingScreenView _view;

        public bool IsLoading { get; private set; }
        public event Action<string> SceneReady;

        public SceneFlow(MonoBehaviour host, LoadingScreenView view)
        {
            _host = host;
            _view = view;
        }

        public string Current { get { return SceneManager.GetActiveScene().name; } }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            if (Game.Party != null && Game.Party.State != LobbyState.Offline) Game.Party.SetState(LobbyState.ReturnToParty);
            Load(MapCatalog.MainMenuScene, "VAMP", new[] { "LOADING MENU" });
        }

        public void StartMatch(MatchConfig config)
        {
            if (config == null) return;
            MatchLaunch.Pending = config;
            MatchLaunch.Last = config.Clone();
            var map = MapCatalog.Get(config.mapId);
            if (Game.Party != null) Game.Party.SetState(LobbyState.PreMatch);
            if (Game.Matchmaking != null) Game.Matchmaking.Reset();
            Load(map.SceneName, "LOADING " + map.DisplayName + "...", new[] { "CONNECTING", "LOADING MAP", "LOADING PLAYERS" });
        }

        public void Load(string sceneName, string title, string[] steps)
        {
            if (IsLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Game.Notifications.Push(NotificationKind.Error, "SCENE UNAVAILABLE",
                    sceneName + " IS NOT IN BUILD SETTINGS. RUN VAMP ▸ BUILD ALL SCENES.");
                return;
            }
            _host.StartCoroutine(Run(sceneName, title, steps));
        }

        private IEnumerator Run(string sceneName, string title, string[] steps)
        {
            IsLoading = true;
            _view.Show(title, steps);
            yield return new WaitForSecondsRealtime(0.15f);
            _view.SetStep(0, true);

            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError("[VAMP] Scene load failed: " + e);
            }

            if (op == null)
            {
                _view.Hide();
                IsLoading = false;
                Game.Notifications.Push(NotificationKind.Error, "LOADING FAILED", "PLEASE TRY AGAIN");
                yield break;
            }

            while (!op.isDone)
            {
                _view.SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                if (op.progress > 0.3f) _view.SetStep(1, true);
                yield return null;
            }
            _view.SetProgress(1f);
            if (steps.Length > 2) _view.SetStep(2, true);
            yield return new WaitForSecondsRealtime(0.2f);
            _view.Hide();
            IsLoading = false;
            if (SceneReady != null) SceneReady(sceneName);
        }
    }
}
