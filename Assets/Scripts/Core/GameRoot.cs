using UnityEngine;

namespace Vamp.Core
{
    /// <summary>The single persistent [VAMP] object that owns every global service (see <see cref="Game"/>).</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        private static GameRoot _instance;
        public static GameRoot Instance { get { return _instance; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Game.Init(this);
        }

        private void Update()
        {
            if (Game.Matchmaking != null) Game.Matchmaking.Tick(Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            Game.Shutdown();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                Game.Shutdown();
                _instance = null;
            }
        }
    }
}
