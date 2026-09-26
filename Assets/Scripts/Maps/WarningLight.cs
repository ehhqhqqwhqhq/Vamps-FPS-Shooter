using UnityEngine;

namespace Vamp.Maps
{
    /// <summary>Industrial warning light: pulses or strobes. Cheap (no shadows), phase-offset so lights don't sync.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class WarningLight : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 4f;
        [SerializeField] private float speed = 2.5f;
        [SerializeField] private bool strobe = false;
        private Light _light;
        private float _phase;

        public void Configure(float intensity, float rate, bool isStrobe)
        {
            baseIntensity = intensity;
            speed = rate;
            strobe = isStrobe;
        }

        private void Awake()
        {
            _light = GetComponent<Light>();
            _phase = Random.value * 10f;
        }

        private void Update()
        {
            float t = Time.time * speed + _phase;
            float k = strobe ? (Mathf.Sin(t * 3f) > 0.6f ? 1f : 0.08f) : 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t));
            _light.intensity = baseIntensity * k;
        }
    }
}
