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
            public Quaternion HandInLower = Quaternion.identity;  // natural wrist: hand rotation relative to the forearm
            public Transform[] Fingers;                           // index..pinky, joints 1-3
            public Transform Index1, Pinky1;
            public float CurlSign = 1f;
            public float Curl;                                    // extra bend per finger joint (degrees)
            public bool Ok;
        }

        /// <summary>Extra finger bend per joint on top of the pose (degrees): right = tighter fist, left = wrap the handguard.</summary>
        public static float RightCurl = 12f, LeftCurl = 24f;

        public void SetCurl(float right, float left)
        {
            if (_r != null) _r.Curl = right;
            if (_l != null) _l.Curl = left;
        }

        private static void FindCurlSign(Arm a)
        {
            if (a.Fingers == null || a.Fingers.Length < 6 || a.Index1 == null || a.Pinky1 == null) return;
            var mid1 = a.Fingers[3];
            var tip = a.Fingers[5];
            Quaternion keep = mid1.rotation;
            Vector3 w = (a.Index1.position - a.Pinky1.position).normalized;
            mid1.rotation = Quaternion.AngleAxis(15f, w) * keep;
            float dPlus = (tip.position - a.Hand.position).magnitude;
            mid1.rotation = Quaternion.AngleAxis(-15f, w) * keep;
            float dMinus = (tip.position - a.Hand.position).magnitude;
            mid1.rotation = keep;
            a.CurlSign = dPlus < dMinus ? 1f : -1f;
        }

        private static void ApplyCurl(Arm a)
        {
            if (a.Fingers == null || Mathf.Abs(a.Curl) < 0.01f || a.Index1 == null || a.Pinky1 == null) return;
            Vector3 w = (a.Index1.position - a.Pinky1.position).normalized;
            Quaternion q = Quaternion.AngleAxis(a.Curl * a.CurlSign, w);
            foreach (var f in a.Fingers) f.rotation = q * f.rotation;   // proximal → distal, so bends accumulate
        }

        /// <summary>How much of the wrist roll the forearm takes.</summary>
        public static float TwistShare = 0.65f;
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
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var f in new[] { "Index", "Middle", "Ring", "Pinky" })
                for (int j = 1; j <= 3; j++)
                {
                    var t = Find(transform, p + "Hand" + f + j);
                    if (t != null) list.Add(t);
                }
            a.Fingers = list.ToArray();
            a.Index1 = Find(transform, p + "HandIndex1");
            a.Pinky1 = Find(transform, p + "HandPinky1");
            return a;
        }

        private void Init()
        {
            _anim = GetComponentInChildren<Animation>();
            _r = Bones("Right");
            _l = Bones("Left");
            if (_anim != null && !Application.isPlaying)
            {
                // Editor (Arms Lab): no animation update - sample the pose directly.
                var clip = _anim.GetClip("GunPose");
                if (clip != null) clip.SampleAnimation(_anim.gameObject, 0f);
            }
            else if (_anim != null && _anim["GunPose"] != null)
            {
                var st = _anim["GunPose"];
                st.wrapMode = WrapMode.ClampForever;
                _anim.Play("GunPose");
                st.enabled = true;
                st.weight = 1f;
                st.time = 0f;
                _anim.Sample();
            }
            if (_r.Ok) { _r.Curl = RightCurl; FindCurlSign(_r); ApplyCurl(_r); Calibrate(_r, "Right", false); }
            if (_l.Ok) { _l.Curl = LeftCurl; FindCurlSign(_l); ApplyCurl(_l); Calibrate(_l, "Left", true); }
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
            a.HandInLower = Quaternion.Inverse(a.Lower.rotation) * a.Hand.rotation;
            a.CenterInHand = Quaternion.Inverse(a.Hand.rotation) * (center - a.Hand.position);
        }

        /// <summary>Put the right hand's grip frame at (rPos, rRot) and the left at (lPos, lRot), all in weapon space.</summary>
        public void Solve(Transform weapon, Vector3 rPos, Quaternion rRot, bool left, Vector3 lPos, Quaternion lRot)
        {
            if (weapon == null) return;
            if (!Application.isPlaying && _anim != null)
            {
                var clip = _anim.GetClip("GunPose");
                if (clip != null) clip.SampleAnimation(_anim.gameObject, 0f);
            }
            if (_r != null && _r.Ok) Place(_r, weapon.TransformPoint(rPos), weapon.rotation * rRot, transform.TransformDirection(new Vector3(0.9f, -1f, -0.3f)));
            if (_l == null || !_l.Ok) return;
            if (left) Place(_l, weapon.TransformPoint(lPos), weapon.rotation * lRot, transform.TransformDirection(new Vector3(-0.9f, -1f, -0.3f)));
            else Place(_l, transform.TransformPoint(new Vector3(-0.2f, -0.62f, 0.05f)), transform.rotation, transform.TransformDirection(new Vector3(-1f, 0f, -0.3f))); // one-handed: left arm down, out of view
        }

        /// <summary>Debug: palm/finger directions of a hand in the given space.</summary>
        public string Describe(Transform space, bool left)
        {
            var a = left ? _l : _r;
            if (a == null || !a.Ok || a.Fingers.Length < 12) return "-";
            string side = left ? "Left" : "Right";
            var ring1 = Find(transform, "mixamorig:" + side + "HandRing1");
            var ringEnd = Find(transform, "mixamorig:" + side + "HandRing3_end");
            var mid1 = Find(transform, "mixamorig:" + side + "HandMiddle1");
            Vector3 w = space.InverseTransformDirection((a.Index1.position - a.Pinky1.position).normalized);
            Vector3 f = space.InverseTransformDirection((mid1.position - a.Hand.position).normalized);
            Vector3 c = space.InverseTransformDirection((ringEnd.position - ring1.position).normalized);
            Vector3 h = space.InverseTransformPoint(a.Hand.position);
            return side + " wrist " + h.ToString("F3") + " W(pinky>index) " + w.ToString("F2") + " F(wrist>knuckles) " + f.ToString("F2") + " curl " + c.ToString("F2");
        }

        private static void Place(Arm a, Vector3 gripWorld, Quaternion frameWorld, Vector3 pole)
        {
            Quaternion handRot = frameWorld * Quaternion.Inverse(a.FrameInHand);
            Vector3 handPos = gripWorld - handRot * a.CenterInHand;
            TwoBone(a.Upper, a.Lower, a.End, handPos, pole);
            // Share the wrist roll with the forearm (the rig has no twist bones), so the wrist doesn't candy-wrap.
            Vector3 axis = (a.End.position - a.Lower.position).normalized;
            Quaternion diff = handRot * Quaternion.Inverse(a.Lower.rotation * a.HandInLower);
            Vector3 v = Vector3.Project(new Vector3(diff.x, diff.y, diff.z), axis);
            var twist = new Quaternion(v.x, v.y, v.z, diff.w);
            float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (m > 1e-5f)
            {
                twist = new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m);
                a.Lower.rotation = Quaternion.Slerp(Quaternion.identity, twist, TwistShare) * a.Lower.rotation;
            }
            a.Hand.SetPositionAndRotation(handPos, handRot);
            ApplyCurl(a);
        }

        private static void TwoBone(Transform a, Transform b, Transform c, Vector3 t, Vector3 pole)
        {
            float lab = (b.position - a.position).magnitude;
            float lcb = (c.position - b.position).magnitude;
            float reach = (lab + lcb) * 0.97f;
            Vector3 toT = t - a.position;
            if (toT.magnitude > reach)
            {
                // Slide the whole shoulder (collarbone) so the skin moves with it instead of stretching.
                var mover = a.parent != null ? a.parent : a;
                mover.position += toT.normalized * (toT.magnitude - reach);
            }

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
