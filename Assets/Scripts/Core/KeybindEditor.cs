using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vamp.Core
{
    /// <summary>
    /// Rebinding service used by SETTINGS ▸ CONTROLS. Works on its own copy of the action asset and saves binding
    /// overrides into the account settings (controls.keybindsJson); the in-game InputController reloads them.
    /// Rules: "PRESS A KEY" (Esc cancels), conflicts are reported and only replaced if the player confirms —
    /// binds are never silently removed. RESET TO DEFAULTS clears all overrides.
    /// </summary>
    public sealed class KeybindEditor : IDisposable
    {
        public struct Conflict
        {
            public InputAction Action;
            public int Index;
            public string Label;
        }

        public sealed class Candidate
        {
            public InputAction Action;
            public int Index;
            public string Path;
            public string DisplayPath;
            public List<Conflict> Conflicts = new List<Conflict>();
        }

        public InputActionAsset Asset { get; private set; }
        private InputActionRebindingExtensions.RebindingOperation _op;

        public KeybindEditor()
        {
            Asset = VampInputActions.CreateDefault();
            Load(Asset, Game.Settings != null ? Game.Settings.Current.controls.keybindsJson : "");
        }

        // ---- Persistence -------------------------------------------------------------------------
        // Binding IDs are regenerated every time the default asset is built in code, so Unity's
        // SaveBindingOverridesAsJson (which keys by ID) can't survive a restart. VAMP stores overrides
        // by action name + binding slot instead, which is stable across launches.

        [Serializable] private sealed class SavedBind { public string a; public int i; public string p; }
        [Serializable] private sealed class SavedBinds { public int v = 2; public List<SavedBind> b = new List<SavedBind>(); }

        public static string Serialize(InputActionAsset asset)
        {
            var data = new SavedBinds();
            foreach (var map in asset.actionMaps)
            {
                if (map.name != VampInputActions.GameplayMap) continue;
                foreach (var action in map.actions)
                {
                    var bs = action.bindings;
                    for (int i = 0; i < bs.Count; i++)
                        if (bs[i].overridePath != null) data.b.Add(new SavedBind { a = action.name, i = i, p = bs[i].overridePath });
                }
            }
            return data.b.Count == 0 ? "" : JsonUtility.ToJson(data);
        }

        public static void Load(InputActionAsset asset, string json)
        {
            asset.RemoveAllBindingOverrides();
            if (string.IsNullOrEmpty(json) || !json.Contains("\"v\":2")) return; // empty or old ID-based format: defaults
            try
            {
                var data = JsonUtility.FromJson<SavedBinds>(json);
                if (data == null || data.b == null) return;
                var map = asset.FindActionMap(VampInputActions.GameplayMap, false);
                if (map == null) return;
                foreach (var e in data.b)
                {
                    var action = map.FindAction(e.a, false);
                    if (action == null || e.i < 0 || e.i >= action.bindings.Count || action.bindings[e.i].isComposite) continue;
                    action.ApplyBindingOverride(e.i, e.p ?? "");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Invalid keybinds, using defaults: " + e.Message);
                asset.RemoveAllBindingOverrides();
            }
        }

        public bool IsRebinding { get { return _op != null; } }

        public string Display(string action, int index)
        {
            var a = Asset.FindAction(action, false);
            if (a == null || index >= a.bindings.Count) return "-";
            string s = a.GetBindingDisplayString(index);
            return string.IsNullOrEmpty(s) ? "—" : s.ToUpperInvariant();
        }

        public void StartRebind(string action, int index, Action<Candidate> onCandidate, Action onCancel)
        {
            Cancel();
            var a = Asset.FindAction(action, true);
            a.Disable();
            _op = a.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnApplyBinding((op, path) =>
                {
                    var c = new Candidate { Action = a, Index = index, Path = path, DisplayPath = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant() };
                    FindConflicts(path, a, index, c.Conflicts);
                    if (onCandidate != null) onCandidate(c);
                })
                .OnComplete(op => Finish())
                .OnCancel(op => { Finish(); if (onCancel != null) onCancel(); })
                .Start();
        }

        public void Cancel()
        {
            if (_op != null) _op.Cancel();
        }

        private void Finish()
        {
            if (_op != null) { _op.Dispose(); _op = null; }
        }

        public void FindConflicts(string path, InputAction ignore, int ignoreIndex, List<Conflict> results)
        {
            foreach (var map in Asset.actionMaps)
            {
                if (map.name != VampInputActions.GameplayMap) continue;
                foreach (var action in map.actions)
                {
                    var b = action.bindings;
                    for (int i = 0; i < b.Count; i++)
                    {
                        if (b[i].isComposite) continue;
                        if (action == ignore && i == ignoreIndex) continue;
                        if (string.Equals(b[i].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                            results.Add(new Conflict { Action = action, Index = i, Label = LabelFor(action, i) });
                    }
                }
            }
        }

        public static string LabelFor(InputAction a, int index)
        {
            foreach (var r in VampInputActions.Rebindable)
                if (r.action == a.name && (r.index == index || (r.index == 0 && index == 1 && r.hasSecondary))) return r.label + (index == 1 && r.index == 0 ? " (SECONDARY)" : "");
            return a.name.ToUpperInvariant();
        }

        /// <summary>Applies a candidate; conflicting binds are cleared only when <paramref name="replace"/> (the player chose REPLACE).</summary>
        public void Apply(Candidate c, bool replace)
        {
            if (c.Conflicts.Count > 0 && !replace) return;
            foreach (var x in c.Conflicts) x.Action.ApplyBindingOverride(x.Index, "");
            c.Action.ApplyBindingOverride(c.Index, c.Path);
            Save();
        }

        public void Clear(string action, int index)
        {
            var a = Asset.FindAction(action, true);
            a.ApplyBindingOverride(index, "");
            Save();
        }

        public void ResetDefaults()
        {
            Asset.RemoveAllBindingOverrides();
            Save();
        }

        private void Save()
        {
            if (Game.Settings == null) return;
            Game.Settings.Current.controls.keybindsJson = Serialize(Asset);
            Game.Settings.Save();
            Game.Settings.ApplyNonDisplay(); // notifies InputController to reload
        }

        public void Dispose()
        {
            Finish();
            if (Asset != null) UnityEngine.Object.Destroy(Asset);
        }
    }
}
