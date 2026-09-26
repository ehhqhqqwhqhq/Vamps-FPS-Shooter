using UnityEngine;

namespace Vamp.UI
{
    /// <summary>Level number reveal: scale punch + glow pulse (unscaled time so it works while paused).</summary>
    public sealed class LevelPop : MonoBehaviour
    {
        private float _t;

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float s = _t < 0.25f ? Mathf.Lerp(2.2f, 0.95f, _t / 0.25f) : Mathf.Lerp(0.95f, 1f, Mathf.Clamp01((_t - 0.25f) / 0.2f));
            transform.localScale = Vector3.one * s;
        }
    }
}
