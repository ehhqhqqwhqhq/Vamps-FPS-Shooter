using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// Skinned first-person arms (the butterfly-knife rig) holding any weapon. The "GunPose" clip gives the finger
    /// poses (right fist, relaxed left hand); every frame the hands are placed on the weapon's grip points and each
    /// arm is solved with two-bone IK (elbows out and down). If a target is out of reach the shoulder slides towards
    /// it (shoulders are off screen).
    /// </summary>
    public sealed class FirstPersonArms : MonoBehaviour
    {
        private sealed class Arm
        {
            public Transform Upper, Lower, End, Hand;
            public Quaternion FrameInHand = Quaternion.identity; // grip frame, relative to the hand's rotation
            public Vector3 CenterInHand;                          // grip centre, hand-rotation space (unscaled)
            public bool Ok;
        }

        private Arm _r, _l;
        private Animation _anim;
        public bool Ready { get { return _r != null && _r.Ok; } }

        public static FirstPersonArms Create(GameObject rig)
        {
            var a = rig.AddComponent<FirstPersonArms>();
            a.Init();
            return a;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = Find(c, name);
                if (r != null) return r;
            }
            return null;
        }

        private Arm Bones(string side)
        {
            string p = "mixamorig:" + side;
            var a = new Arm
            {
                Upper = Find(transform, p + "Arm"),
                Lower = Find(transform, p + "ForeArm"),
                End = Find(transform, p + "ForeArm_end"),
                Hand = Find(transform, p + "Hand"),
            };
            a.Ok = a.Upper != null && a.Lower != null && a.End != null && a.Hand != null;
            return a;
        }

        private void Init()
        {
            _anim = GetComponentInChildren<Animation>();
            _r = Bones("Right");
            _l = Bones("Left");
            if (_anim != null && _anim["GunPose"] != null)
            {
                var st = _anim["GunPose"];
                st.wrapMode = WrapMode.ClampForever;
                _anim.Play("GunPose");
                st.enabled = true;
                st.weight = 1f;
                st.time = 0f;
                _anim.Sample();
            }
            if (_r.Ok) Calibrate(_r, "Right", false);
            if (_l.Ok) Calibrate(_l, "Left", true);
        }

        /// <summary>
        /// Grip frame from the posed fingers. Right (fist): up = pinky→index knuckles, forward = wrist→knuckles.
        /// Left: forward = pinky→index, up = the way the fingers curl (palm side).
        /// </summary>
        private void Calibrate(Arm a, string side, bool left)
        {
            string p = "mixamorig:" + side + "Hand";
            var idx = Find(transform, p + "Index1");
            var pinky = Find(transform, p + "Pinky1");
            var mid1 = Find(transform, p + "Middle1");
            var mid2 = Find(transform, p + "Middle2");
            var mid3 = Find(transform, p + "Middle3");
            var midEnd = Find(transform, p + "Middle3_end");
            var ringEnd = Find(transform, p + "Ring3_end");
            var ring1 = Find(transform, p + "Ring1");
            if (idx == null || pinky == null || mid1 == null || midEnd == null || ringEnd == null || ring1 == null) { a.Ok = false; return; }

            Vector3 w = (idx.position - pinky.position).normalized;
            Vector3 f = (mid1.position - a.Hand.position).normalized;
            Vector3 curl = Vector3.ProjectOnPlane(ringEnd.position - ring1.position, f).normalized; // towards the palm
            Quaternion frame;
            Vector3 center;
            if (!left)
            {
                frame = Quaternion.LookRotation(Vector3.ProjectOnPlane(f, w), w);
                center = (mid1.position + (mid2 != null ? mid2.position : mid1.position) + (mid3 != null ? mid3.position : midEnd.position) + midEnd.position) * 0.25f;
                center = Vector3.Lerp(center, (a.Hand.position + mid1.position) * 0.5f, 0.35f);
            }
            else
            {
                frame = Quaternion.LookRotation(Vector3.ProjectOnPlane(w, curl), curl);
                center = (a.Hand.position + mid1.position) * 0.5f + curl * 0.028f;
            }
            a.FrameInHand = Quaternion.Inverse(a.Hand.rotation) * frame;
            a.CenterInHand = Quaternion.Inverse(a.Hand.rotation) * (center - a.Hand.position);
        }

        /// <summary>Put the right hand's grip frame at (rPos, rRot) and the left at (lPos, lRot), all in weapon space.</summary>
        public void Solve(Transform weapon, Vector3 rPos, Quaternion rRot, bool left, Vector3 lPos, Quaternion lRot)
        {
            if (weapon == null) return;
            if (_r != null && _r.Ok) Place(_r, weapon.TransformPoint(rPos), weapon.rotation * rRot, transform.TransformDirection(new Vector3(0.9f, -1f, -0.3f)));
            if (_l == null || !_l.Ok) return;
            if (left) Place(_l, weapon.TransformPoint(lPos), weapon.rotation * lRot, transform.TransformDirection(new Vector3(-0.9f, -1f, -0.3f)));
            else Place(_l, transform.TransformPoint(new Vector3(-0.2f, -0.62f, 0.05f)), transform.rotation, transform.TransformDirection(new Vector3(-1f, 0f, -0.3f))); // one-handed: left arm down, out of view
        }

        private static void Place(Arm a, Vector3 gripWorld, Quaternion frameWorld, Vector3 pole)
        {
            Quaternion handRot = frameWorld * Quaternion.Inverse(a.FrameInHand);
            Vector3 handPos = gripWorld - handRot * a.CenterInHand;
            TwoBone(a.Upper, a.Lower, a.End, handPos, pole);
            a.Hand.SetPositionAndRotation(handPos, handRot);
        }

        private static void TwoBone(Transform a, Transform b, Transform c, Vector3 t, Vector3 pole)
        {
            float lab = (b.position - a.position).magnitude;
            float lcb = (c.position - b.position).magnitude;
            float reach = (lab + lcb) * 0.97f;
            Vector3 toT = t - a.position;
            if (toT.magnitude > reach) a.position += toT.normalized * (toT.magnitude - reach); // slide the shoulder

            Vector3 A = a.position, B = b.position, C = c.position;
            float lat = Mathf.Clamp((t - A).magnitude, 0.01f, (lab + lcb) * 0.999f);
            float acab0 = Vector3.Angle(C - A, B - A);
            float babc0 = Vector3.Angle(A - B, C - B);
            float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f)) * Mathf.Rad2Deg;
            float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 axis0 = Vector3.Cross(C - A, B - A);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(C - A, pole);
            axis0.Normalize();
            a.rotation = Quaternion.AngleAxis(acab1 - acab0, axis0) * a.rotation;
            b.rotation = Quaternion.AngleAxis(babc1 - babc0, axis0) * b.rotation;

            // Swing the chain onto the target.
            a.rotation = Quaternion.FromToRotation(c.position - a.position, t - a.position) * a.rotation;

            // Twist round the shoulder→target line so the elbow points at the pole.
            Vector3 axis = (t - a.position).normalized;
            Vector3 e = Vector3.ProjectOnPlane(b.position - a.position, axis);
            Vector3 p = Vector3.ProjectOnPlane(pole, axis);
            if (e.sqrMagnitude > 1e-8f && p.sqrMagnitude > 1e-8f)
                a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(e, p, axis), axis) * a.rotation;
        }
    }
}
