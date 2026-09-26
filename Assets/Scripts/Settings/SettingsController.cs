using System;
using UnityEngine;
using Vamp.Core;
using Vamp.Graphics;

namespace Vamp.Settings
{
    /// <summary>
    /// Owns the player's <see cref="GameSettings"/>: load/save (per account, device fallback), validation,
    /// presets and applying to the engine. UI edits <see cref="Current"/> then calls <see cref="Apply"/>/<see cref="Save"/>;
    /// gameplay systems (camera, HUD, input) listen to <see cref="Changed"/> - UI never touches them directly.
    /// </summary>
    public sealed class SettingsController
    {
        private const string DeviceFile = "device_settings.json";

        private string _accountId;

        public GameSettings Current { get; private set; }
        public event Action<GameSettings> Changed;

        public SettingsController()
        {
            Current = new GameSettings();
            GameSettings.ApplyPreset(Current.graphics, QualityPreset.High);
        }

        private string FilePath
        {
            get { return string.IsNullOrEmpty(_accountId) ? DeviceFile : "accounts/" + _accountId + "/settings.json"; }
        }

        public void LoadDevice()
        {
            _accountId = null;
            GameSettings loaded;
            Current = JsonStore.TryLoad(DeviceFile, out loaded) ? loaded : CreateDefaults();
            Current.Validate();
            Apply();
        }

        /// <summary>After login: account settings follow the player; device graphics act as a template for new accounts.</summary>
        public void LoadForAccount(string accountId)
        {
            _accountId = accountId;
            GameSettings loaded;
            if (JsonStore.TryLoad(FilePath, out loaded)) Current = loaded;
            else
            {
                Current = Current != null ? Current.Clone() : CreateDefaults();
                Save();
            }
            Current.Validate();
            Apply();
        }

        public void CreateForNewAccount(string accountId)
        {
            _accountId = accountId;
            Current = Current != null ? Current.Clone() : CreateDefaults();
            Current.controls.keybindsJson = "";
            Current.Validate();
            Save();
        }

        public void Save()
        {
            Current.Validate();
            JsonStore.Save(FilePath, Current);
            if (!string.IsNullOrEmpty(_accountId)) JsonStore.Save(DeviceFile, Current); // keep device template fresh
        }

        /// <summary>Validate + push to engine + notify listeners.</summary>
        public void Apply()
        {
            Current.Validate();
            try
            {
                DisplaySettingsApplier.Apply(Current.display);
                GraphicsSettingsApplier.Apply(Current.graphics, Current.accessibility);
                AudioListener.volume = Current.audio.master;
            }
            catch (Exception e)
            {
                // Never let a bad setting crash the game.
                Debug.LogError("[VAMP] Applying settings failed: " + e);
            }
            if (Changed != null) Changed(Current);
        }

        /// <summary>Apply without touching display mode (used for live previews of non-display options).</summary>
        public void ApplyNonDisplay()
        {
            Current.Validate();
            try
            {
                GraphicsSettingsApplier.Apply(Current.graphics, Current.accessibility);
                AudioListener.volume = Current.audio.master;
                QualitySettings.vSyncCount = Current.display.vSync ? 1 : 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[VAMP] Applying settings failed: " + e);
            }
            if (Changed != null) Changed(Current);
        }

        public void SetGraphicsPreset(QualityPreset preset)
        {
            GameSettings.ApplyPreset(Current.graphics, preset);
        }

        /// <summary>Call after changing an individual graphics option: switches preset label to CUSTOM when needed.</summary>
        public void RefreshPresetLabel()
        {
            var g = Current.graphics;
            if (g.preset != QualityPreset.Custom && !GameSettings.MatchesPreset(g, g.preset)) g.preset = QualityPreset.Custom;
        }

        public void ResetAll()
        {
            string keepKeybinds = "";
            Current = CreateDefaults();
            Current.firstLaunchDone = true;
            Current.controls.keybindsJson = keepKeybinds;
            Save();
            Apply();
        }

        public static GameSettings CreateDefaults()
        {
            var s = new GameSettings();
            GameSettings.ApplyPreset(s.graphics, GraphicsDetector.Recommend().Preset);
            // Competitive defaults: fullscreen, highest refresh, v-sync off, FPS limit AUTO (monitor), MB/DOF off,
            // screen shake 50%, FOV 90, acceleration off, raw input on.
            s.display.displayMode = DisplayModeOption.Fullscreen;
            s.display.vSync = false;
            s.display.fpsLimit = 0;
            return s;
        }
    }
}
