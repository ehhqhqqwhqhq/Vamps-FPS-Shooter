using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// Moving parts of a weapon model for first-person animation (set up by VampArtBuilder / the placeholder builder):
    /// the magazine (on a socket whose +Z points the way the mag drops out), the shotgun pump, and the shell port.
    /// All positions are in weapon-root space.
    /// </summary>
    public sealed class WeaponParts : MonoBehaviour
    {
        public Transform MagSocket;
        public float MagLength = 0.12f;
        public Transform PumpSocket;
        public float PumpTravel = 0.07f;
        public Vector3 LoadPort = new Vector3(0f, -0.02f, 0.1f);
        public bool Pistol;

        private bool _cached;
        private Vector3 _magRest, _pumpRest;
        private Quaternion _magRestRot;

        private void Cache()
        {
            if (_cached) return;
            _cached = true;
            if (MagSocket != null) { _magRest = MagSocket.localPosition; _magRestRot = MagSocket.localRotation; }
            if (PumpSocket != null) _pumpRest = PumpSocket.localPosition;
        }

        public bool HasMag { get { return MagSocket != null; } }
        public bool HasPump { get { return PumpSocket != null; } }

        /// <summary>Mag drop-out direction (weapon space).</summary>
        public Vector3 MagOutDir { get { Cache(); return MagSocket != null ? _magRestRot * Vector3.forward : Vector3.down; } }
        /// <summary>Top of the magazine (where it seats), weapon space.</summary>
        public Vector3 MagTop { get { Cache(); return MagSocket != null ? _magRest : Vector3.zero; } }

        /// <summary>Slide the magazine out along its axis, plus an extra free offset/rotation (weapon space).</summary>
        public void SetMag(float outDistance, Vector3 extra, Vector3 extraEuler, bool visible)
        {
            if (MagSocket == null) return;
            Cache();
            MagSocket.localPosition = _magRest + (_magRestRot * Vector3.forward) * outDistance + extra;
            MagSocket.localRotation = Quaternion.Euler(extraEuler) * _magRestRot;
            if (MagSocket.gameObject.activeSelf != visible) MagSocket.gameObject.SetActive(visible);
        }

        /// <summary>0 = pump forward, 1 = pulled all the way back.</summary>
        public void SetPump(float t)
        {
            if (PumpSocket == null) return;
            Cache();
            PumpSocket.localPosition = _pumpRest + Vector3.back * (PumpTravel * t);
        }

        public void ResetParts()
        {
            SetMag(0f, Vector3.zero, Vector3.zero, true);
            SetPump(0f);
        }
    }
}
