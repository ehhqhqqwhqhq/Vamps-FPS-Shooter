using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Audio
{
    public enum SfxId
    {
        HitTick, HitHeadshot, Kill, ShotgunFire, PistolFire, RocketFire, Explosion, DryFire, Reload, Jump, Land, Dash, Slide,
        WallJump, Equip, RifleFire, SmgFire, SniperFire, EnergyFire, HeavyFire, MeleeSwing, Overheat,
        UIClick, UIHover, UIOpen, UIError, LevelUp, Unlock, Countdown, CountdownGo, MatchEnd, Announcer, Death, Checkpoint
    }

    public enum AudioCategory { Sfx, UI, Music, Announcer, Voice }

    /// <summary>
    /// Central audio: pooled one-shots (no per-shot allocations), volume categories driven by AUDIO settings
    /// (master / music / sfx / ui / announcer / voice), and a looping music player.
    /// Prototype clips are synthesised procedurally; assign real clips in <see cref="overrides"/> and they win.
    /// </summary>
    public sealed class AudioController : MonoBehaviour
    {
        [Serializable]
        public struct ClipOverride
        {
            public SfxId id;
            public AudioClip clip;
        }

        [SerializeField] private ClipOverride[] overrides = new ClipOverride[0];
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip matchMusic;
        [SerializeField] private int poolSize = 24;

        private static AudioController _instance;
        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private AudioSource[] _pool;
        private int _next;
        private AudioSource _music;
        private float _musicTarget;
        private string _musicKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        public static AudioController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AudioController>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[AudioController]");
                        _instance = go.AddComponent<AudioController>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject); // avoid duplicate global systems
                return;
            }
            _instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            _pool = new AudioSource[Mathf.Max(4, poolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                var child = new GameObject("Sfx" + i);
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2f;
                src.maxDistance = 80f;
                _pool[i] = src;
            }

            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform, false);
            _music = musicGo.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            _music.volume = 0f;

            foreach (var o in overrides)
                if (o.clip != null) _clips[o.id] = o.clip;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_music == null) return;
            float target = _musicTarget * CategoryVolume(AudioCategory.Music);
            _music.volume = Mathf.MoveTowards(_music.volume, target, Time.unscaledDeltaTime * 0.6f);
            if (_music.volume <= 0.001f && _musicTarget <= 0f && _music.isPlaying) _music.Stop();
        }

        // ------------------------------------------------------------------ Volume

        /// <summary>Per-category volume. Master volume is AudioListener.volume (set by SettingsController).</summary>
        public static float CategoryVolume(AudioCategory c)
        {
            var s = Core.Game.Settings;
            if (s == null) return 1f;
            var a = s.Current.audio;
            switch (c)
            {
                case AudioCategory.UI: return a.ui;
                case AudioCategory.Music: return a.music;
                case AudioCategory.Announcer: return a.announcer;
                case AudioCategory.Voice: return a.voice;
                default: return a.sfx;
            }
        }

        private static AudioCategory CategoryOf(SfxId id)
        {
            switch (id)
            {
                case SfxId.UIClick: case SfxId.UIHover: case SfxId.UIOpen: case SfxId.UIError:
                case SfxId.LevelUp: case SfxId.Unlock:
                    return AudioCategory.UI;
                case SfxId.Announcer: case SfxId.Countdown: case SfxId.CountdownGo: case SfxId.MatchEnd:
                    return AudioCategory.Announcer;
                default:
                    return AudioCategory.Sfx;
            }
        }

        // ------------------------------------------------------------------ One-shots

        /// <summary>Non-spatial (your own weapon, hit markers).</summary>
        public static void Play2D(SfxId id, float volume = 1f, float pitch = 1f)
        {
            Instance.PlayInternal(id, Vector3.zero, false, volume, pitch);
        }

        /// <summary>Spatial (explosions, other players).</summary>
        public static void Play(SfxId id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            Instance.PlayInternal(id, position, true, volume, pitch);
        }

        public static void PlayUI(SfxId id, float volume = 1f)
        {
            Instance.PlayInternal(id, Vector3.zero, false, volume, 1f);
        }

        private void PlayInternal(SfxId id, Vector3 position, bool spatial, float volume, float pitch)
        {
            if (_pool == null) return;
            AudioClip clip = GetClip(id);
            if (clip == null) return;

            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.Stop();
            src.clip = clip;
            src.spatialBlend = spatial ? 1f : 0f;
            src.transform.position = spatial ? position : transform.position;
            src.volume = volume * CategoryVolume(CategoryOf(id));
            src.pitch = pitch;
            src.Play();
        }

        private AudioClip GetClip(SfxId id)
        {
            AudioClip clip;
            if (_clips.TryGetValue(id, out clip)) return clip;
            clip = ProceduralSfx.Create(id);
            _clips[id] = clip;
            return clip;
        }

        // ------------------------------------------------------------------ Music

        /// <summary>Crossfade to menu / match music ("menu", "match", or null to fade out).</summary>
        public static void SetMusic(string key, float volume = 0.7f)
        {
            var self = Instance;
            if (self._music == null) return;
            if (key == null)
            {
                self._musicTarget = 0f;
                self._musicKey = null;
                return;
            }
            self._musicTarget = volume;
            if (self._musicKey == key && self._music.isPlaying) return;
            self._musicKey = key;
            AudioClip clip = key == "match" ? (self.matchMusic != null ? self.matchMusic : ProceduralSfx.MusicLoop(true))
                                            : (self.menuMusic != null ? self.menuMusic : ProceduralSfx.MusicLoop(false));
            self._music.clip = clip;
            self._music.volume = 0f;
            self._music.Play();
        }
    }
}
