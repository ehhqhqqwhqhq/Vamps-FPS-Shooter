using UnityEngine;
using UnityEngine.AI;
using Vamp.Combat;
using Vamp.Match;
using Vamp.Progression;
using Vamp.UI;
using Vamp.Weapons;

namespace Vamp.Bots
{
    /// <summary>Keeps name plates facing the camera and fades them with distance.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        private TextMesh _text;
        private Color _base;

        private void Awake()
        {
            _text = GetComponent<TextMesh>();
            if (_text != null) _base = _text.color;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            if (_text == null) return;
            float d = Vector3.Distance(cam.transform.position, transform.position);
            var c = _base;
            c.a = Mathf.Clamp01(1.4f - d / 45f);
            _text.color = c;
        }
    }
}
