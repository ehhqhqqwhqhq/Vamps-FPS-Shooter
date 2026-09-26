using UnityEngine;

namespace Vamp.Match
{
    /// <summary>Movement Race checkpoint ring (trigger). Must be passed in index order; the last one is the finish.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RaceCheckpoint : MonoBehaviour
    {
        [SerializeField] private int index;
        private Renderer[] _parts;

        public int Index { get { return index; } set { index = value; } }

        public static System.Action<RaceCheckpoint, Participant> Passed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Passed = null; }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _parts = GetComponentsInChildren<Renderer>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponentInParent<Participant>();
            if (p != null && Passed != null) Passed(this, p);
        }

        public void SetState(bool next, bool done)
        {
            if (_parts == null) return;
            foreach (var r in _parts)
            {
                if (r == null) continue;
                r.enabled = !done;
                var m = r.material;
                m.color = next ? new Color(1f, 0.15f, 0.2f, 1f) : new Color(0.35f, 0.35f, 0.38f, 1f);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", next ? new Color(2.5f, 0.1f, 0.2f) : new Color(0.1f, 0.1f, 0.1f));
            }
        }
    }
}
