using System.Collections.Generic;
using UnityEngine;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Match;
using Vamp.Settings;

namespace Vamp.Graphics
{
    /// <summary>Runs inside the benchmark match (added by MatchController).</summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        private const float Warmup = 2f;
        private const float Duration = 20f;

        private readonly List<float> _frames = new List<float>();
        private Camera _cam;
        private float _t;
        private float _nextBoom;
        private Vector3[] _path;
        private Weapons.WeaponData _blast;
        private bool _done;

        private void Start()
        {
            var match = GetComponent<MatchController>();
            if (match != null && match.Player != null)
            {
                match.Player.Frozen = true;
                match.Player.Input.GameplayInputEnabled = false;
                foreach (var r in match.Player.GetComponentsInChildren<Renderer>()) r.enabled = false;
                _cam = match.Player.View.PlayerCamera;
            }
            if (_cam == null) _cam = Camera.main;
            _blast = Game.Weapons != null ? Game.Weapons.Get("blast") : null;

            var points = FindObjectsByType<SpawnPoint>();
            _path = new Vector3[Mathf.Max(4, points.Length)];
            for (int i = 0; i < _path.Length; i++)
                _path[i] = points.Length > 0 ? points[i % points.Length].transform.position + Vector3.up * (4f + (i % 3) * 5f) : Random.insideUnitSphere * 20f + Vector3.up * 8f;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            if (Game.Notifications != null) Game.Notifications.Push(NotificationKind.Info, "BENCHMARK RUNNING", "20 SECONDS · DON'T TOUCH ANYTHING");
        }

        private void Update()
        {
            if (_done || _cam == null) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;

            // Flythrough along spawn points (vertical routes)
            float s = _t * 0.18f;
            int i0 = Mathf.FloorToInt(s) % _path.Length;
            int i1 = (i0 + 1) % _path.Length;
            float k = Mathf.SmoothStep(0f, 1f, s - Mathf.Floor(s));
            Vector3 pos = Vector3.Lerp(_path[i0], _path[i1], k);
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation((_path[(i1 + 1) % _path.Length] - pos).normalized + Vector3.down * 0.25f), dt * 2f);

            // Explosions / particles stress
            if (_t > _nextBoom && _blast != null)
            {
                _nextBoom = _t + 0.8f;
                var p = _path[Random.Range(0, _path.Length)] + Random.insideUnitSphere * 6f;
                Explosion.Detonate(p, _blast, null, Physics.DefaultRaycastLayers, null);
            }

            if (_t > Warmup) _frames.Add(dt);
            if (_t > Warmup + Duration) Finish();
        }

        private void Finish()
        {
            _done = true;
            if (_frames.Count == 0) return;
            var sorted = new List<float>(_frames);
            sorted.Sort();
            float total = 0f;
            foreach (var f in _frames) total += f;
            float avg = _frames.Count / total;
            int n = Mathf.Max(1, sorted.Count / 100);
            float worst = 0f;
            for (int i = sorted.Count - n; i < sorted.Count; i++) worst += sorted[i];
            float low = n / worst;
            float max = 1f / sorted[0];
            var current = Game.Settings != null ? Game.Settings.Current.graphics.preset : QualityPreset.High;
            Benchmark.Last = new BenchmarkResult
            {
                Valid = true,
                Average = avg,
                OnePercentLow = low,
                Max = max,
                Recommended = Benchmark.Recommend(avg, low, current)
            };
            if (Game.Notifications != null)
                Game.Notifications.Push(NotificationKind.Achievement, "BENCHMARK COMPLETE",
                    "AVG " + Mathf.RoundToInt(avg) + " · 1% LOW " + Mathf.RoundToInt(low) + " · MAX " + Mathf.RoundToInt(max) + " · RECOMMENDED " + GraphicsDetector.PresetLabel(Benchmark.Last.Recommended));
            if (Game.Scenes != null) Game.Scenes.GoToMainMenu();
        }
    }
}
