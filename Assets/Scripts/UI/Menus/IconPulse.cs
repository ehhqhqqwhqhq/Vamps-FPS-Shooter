using UnityEngine;
using UnityEngine.UI;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>Subtle glow for high-level / animated icons (kept gentle so it doesn't distract in-game).</summary>
    public sealed class IconPulse : MonoBehaviour
    {
        public Outline Target;
        public Text Glyph;
        public Color Base;

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            if (Target != null) Target.effectColor = Color.Lerp(Base, Color.white, k * 0.45f);
            if (Glyph != null) Glyph.transform.localScale = Vector3.one * (1f + 0.04f * k);
        }
    }
}
