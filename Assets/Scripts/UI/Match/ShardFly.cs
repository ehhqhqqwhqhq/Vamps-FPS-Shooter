using UnityEngine;
using UnityEngine.UI;

namespace Vamp.UI
{
    /// <summary>Tiny UI "particle" used by the level-up reveal.</summary>
    public sealed class ShardFly : MonoBehaviour
    {
        public Vector2 Velocity;
        private float _life = 0.9f;
        private Image _img;
        private RectTransform _rt;

        private void Awake()
        {
            _img = GetComponent<Image>();
            _rt = (RectTransform)transform;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _life -= dt;
            _rt.anchoredPosition += Velocity * dt;
            Velocity *= 1f - 2.5f * dt;
            _rt.localRotation = Quaternion.LookRotation(Vector3.forward, Velocity);
            if (_img != null)
            {
                var c = _img.color;
                c.a = Mathf.Clamp01(_life / 0.9f);
                _img.color = c;
            }
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
