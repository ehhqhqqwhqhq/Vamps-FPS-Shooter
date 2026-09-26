using UnityEngine;
using UnityEngine.Rendering;
using Vamp.Weapons;

namespace Vamp.Characters
{
    /// <summary>
    /// Procedural animation for the rigged stick-man character (the imported pack ships without animation clips).
    /// Every frame the skeleton is reset to its rest pose, then:
    ///   hips drop for crouch/slide + run bob, spine leans with speed and aim pitch,
    ///   legs follow a speed-driven gait (two-bone IK to foot targets; tuck in the air, lead leg forward in a slide),
    ///   the held weapon is placed in front of the chest along the aim direction and both hands IK onto its
    ///   Grip / Offhand points. Bone-axis agnostic: only bone positions are used, so any similar humanoid rig works.
    /// Driven by <see cref="CharacterPresenter"/> via <see cref="Pose"/>. Presentation only - hitboxes are separate.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        // Inputs (set by the presenter each frame)
        [System.NonSerialized] public Vector3 Velocity;
        [System.NonSerialized] public Vector3 AimDirection = Vector3.forward;
        [System.NonSerialized] public bool Grounded = true;
        [System.NonSerialized] public bool Sliding;
        [System.NonSerialized] public bool WallRunning;
        [System.NonSerialized] public float Crouch;
        [System.NonSerialized] public float WallSide; // -1 wall on the left, +1 on the right

        private Transform _hips, _spine, _chest, _head;
        private Transform _lUp, _lLow, _lHand, _rUp, _rLow, _rHand;
        private Transform _lThigh, _lShin, _lFoot, _rThigh, _rShin, _rFoot;
        private Transform[] _bones;
        private Quaternion[] _rest;
        private Vector3 _hipsRestLocal;
        private float _hipHeight, _armA, _armB, _legA, _legB;
        private bool _ready;

        private Transform _gun, _grip, _offhand;
        private WeaponData _weapon;
        private Renderer[] _renderers;
        private ShadowCastingMode _shadowMode = ShadowCastingMode.On;
        private Material _tint;

        private float _phase, _crouchS, _airS, _moveS, _slideS;
        private Vector3 _moveDirS = Vector3.forward;

        public WeaponData Weapon { get { return _weapon; } }

        public static CharacterRig Spawn(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("VampCharacter");
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent, false);
            go.name = "Character";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var rig = go.GetComponent<CharacterRig>();
            if (rig == null) rig = go.AddComponent<CharacterRig>();
            return rig;
        }

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (_ready) return;
            _hips = Find("Root") ?? Find("Hips");
            _spine = Find("Spine");
            _chest = Find("Spine2") ?? Find("Spine1") ?? _spine;
            _head = Find("Head");
            _lUp = Find("LeftArm"); _lLow = Find("LeftForeArm"); _lHand = Find("LeftHand");
            _rUp = Find("RightArm"); _rLow = Find("RightForeArm"); _rHand = Find("RightHand");
            _lThigh = Find("LeftUpLeg"); _lShin = Find("LeftLeg"); _lFoot = Find("LeftFoot");
            _rThigh = Find("RightUpLeg"); _rShin = Find("RightLeg"); _rFoot = Find("RightFoot");
            if (_hips == null || _chest == null || _lUp == null || _rUp == null || _lThigh == null || _rThigh == null ||
                _lLow == null || _rLow == null || _lHand == null || _rHand == null || _lShin == null || _rShin == null || _lFoot == null || _rFoot == null)
            {
                Debug.LogWarning("[VAMP] CharacterRig: skeleton bones not found - character will stay in its rest pose.");
                return;
            }
            _bones = _hips.GetComponentsInChildren<Transform>();
            _rest = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++) _rest[i] = _bones[i].localRotation;
            _hipsRestLocal = _hips.localPosition;
            _hipHeight = transform.InverseTransformPoint(_hips.position).y;
            _armA = Vector3.Distance(_lUp.position, _lLow.position);
            _armB = Vector3.Distance(_lLow.position, _lHand.position);
            _legA = Vector3.Distance(_lThigh.position, _lShin.position);
            _legB = Vector3.Distance(_lShin.position, _lFoot.position);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _ready = true;
        }

        private Transform Find(string bone)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == bone || t.name.EndsWith(":" + bone)) return t;
            return null;
        }

        // ------------------------------------------------------------------ Appearance

        public void SetTint(Color c, float emission = 0f)
        {
            Init();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (_tint == null)
                {
                    _tint = new Material(r.sharedMaterial != null ? r.sharedMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit")));
                    _tint.name = "StickManTint";
                }
                _tint.color = c;
                if (emission > 0f && _tint.HasProperty("_EmissionColor"))
                {
                    _tint.EnableKeyword("_EMISSION");
                    _tint.SetColor("_EmissionColor", c * emission);
                }
                r.sharedMaterial = _tint;
            }
        }

        /// <summary>ShadowsOnly for the local player's own body (you see your shadow, not your insides).</summary>
        public void SetShadowMode(ShadowCastingMode mode)
        {
            _shadowMode = mode;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
        }

        public void SetWeapon(WeaponData weapon)
        {
            Init();
            if (_weapon == weapon && (_gun != null || weapon == null)) return;
            _weapon = weapon;
            if (_gun != null) Destroy(_gun.gameObject);
            _gun = _grip = _offhand = null;
            if (weapon == null) return;

            GameObject g;
            if (weapon.viewModelPrefab != null) g = Instantiate(weapon.viewModelPrefab, transform, false);
            else
            {
                // Generated stand-in (launcher / blade have no imported model).
                g = new GameObject("Gun_" + weapon.id);
                g.transform.SetParent(transform, false);
                bool blade = weapon.delivery == DeliveryType.Melee;
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(g.transform, false);
                body.transform.localScale = blade ? new Vector3(0.02f, 0.05f, 0.45f) : new Vector3(0.14f, 0.14f, 0.8f);
                body.transform.localPosition = new Vector3(0f, 0f, blade ? 0.2f : 0.15f);
                var mr = body.GetComponent<Renderer>();
                mr.sharedMaterial = _tint != null ? _tint : mr.sharedMaterial;
                var off = new GameObject("Offhand").transform;
                off.SetParent(g.transform, false);
                off.localPosition = blade ? new Vector3(-0.05f, -0.2f, -0.2f) : new Vector3(0f, -0.06f, 0.35f);
            }
            g.name = "HeldWeapon";
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = _shadowMode;
            _gun = g.transform;
            _grip = _gun.Find("Grip");
            _offhand = _gun.Find("Offhand");
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void SetVisible(bool visible)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
        }

        // ------------------------------------------------------------------ Pose

        public void Pose(float dt)
        {
            if (!_ready || dt <= 0f) return;
            var t = transform;

            for (int i = 0; i < _bones.Length; i++) _bones[i].localRotation = _rest[i];
            _hips.localPosition = _hipsRestLocal;

            // ---- Smoothed state
            Vector3 v = t.InverseTransformDirection(Velocity);
            Vector3 planar = new Vector3(v.x, 0f, v.z);
            float speed = planar.magnitude;
            float k = 1f - Mathf.Exp(-12f * dt);
            _crouchS = Mathf.Lerp(_crouchS, Sliding ? 1f : Mathf.Clamp01(Crouch), k);
            _slideS = Mathf.Lerp(_slideS, Sliding ? 1f : 0f, k);
            _airS = Mathf.Lerp(_airS, Grounded || WallRunning || Sliding ? 0f : 1f, 1f - Mathf.Exp(-8f * dt));
            _moveS = Mathf.Lerp(_moveS, Mathf.Clamp01(speed / 3f), k);
            if (speed > 0.3f) _moveDirS = Vector3.Slerp(_moveDirS, planar / speed, k);

            bool gait = (Grounded || WallRunning) && !Sliding;
            float stride = Mathf.Clamp(0.9f + speed * 0.07f, 0.9f, 2.2f);
            if (gait) _phase += speed * dt / stride * Mathf.PI;

            // ---- Hips: crouch / slide drop + run bob
            float bob = gait ? Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * _moveS : 0f;
            float drop = _hipHeight * (0.32f * _crouchS + 0.18f * _slideS) - bob + 0.04f * _airS;
            _hips.position -= t.up * drop;

            // ---- Spine lean (speed forward, crouch, aim pitch; wall-run leans away from the wall)
            Vector3 aim = AimDirection.sqrMagnitude > 0.001f ? AimDirection.normalized : t.forward;
            float pitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(aim, t.up), -1f, 1f)) * Mathf.Rad2Deg; // + = looking up
            pitch = Mathf.Clamp(pitch, -70f, 70f);
            float lean = Mathf.Clamp(v.z * 1.4f, -8f, 16f) * (1f - _slideS) + 18f * _crouchS * (1f - _slideS) - 22f * _slideS;
            _spine.rotation = Quaternion.AngleAxis(lean, t.right) * _spine.rotation;
            if (WallRunning) _spine.rotation = Quaternion.AngleAxis(12f * WallSide, t.forward) * _spine.rotation;
            _chest.rotation = Quaternion.AngleAxis(-pitch * 0.35f, t.right) * _chest.rotation;
            if (_head != null) _head.rotation = Quaternion.AngleAxis(-pitch * 0.4f, t.right) * _head.rotation;

            // ---- Legs
            Leg(_lThigh, _lShin, _lFoot, -1f, dt);
            Leg(_rThigh, _rShin, _rFoot, 1f, dt);

            // ---- Weapon + arms
            if (_gun != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(aim, t.up);
                if (flat.sqrMagnitude < 0.001f) flat = t.forward;
                Quaternion aimRot = Quaternion.AngleAxis(-pitch, t.right) * Quaternion.LookRotation(t.forward, t.up);
                bool pistol = _weapon != null && _weapon.slot == WeaponSlot.Secondary;
                Vector3 hold = pistol ? new Vector3(0.05f, 0.1f, 0.42f) : new Vector3(0.14f, 0.04f, 0.26f);
                Vector3 pos = _chest.position + aimRot * hold;
                _gun.SetPositionAndRotation(pos, aimRot);

                Vector3 rTarget = _grip != null ? _grip.position : _gun.position;
                Vector3 lTarget = _offhand != null ? _offhand.position : _gun.position + _gun.forward * 0.25f;
                Vector3 down = -t.up;
                TwoBone(_rUp, _rLow, _rHand, rTarget, _rUp.position + t.right * 0.4f + down * 0.6f, _armA, _armB);
                TwoBone(_lUp, _lLow, _lHand, lTarget, _lUp.position - t.right * 0.4f + down * 0.6f, _armA, _armB);
            }
            else
            {
                float swing = gait ? Mathf.Sin(_phase) * 0.25f * _moveS : 0f;
                Vector3 lT = _lUp.position - t.up * (_armA + _armB) * 0.92f - t.right * 0.1f + t.forward * swing;
                Vector3 rT = _rUp.position - t.up * (_armA + _armB) * 0.92f + t.right * 0.1f - t.forward * swing;
                TwoBone(_lUp, _lLow, _lHand, lT, _lUp.position + t.forward * -0.3f, _armA, _armB);
                TwoBone(_rUp, _rLow, _rHand, rT, _rUp.position + t.forward * -0.3f, _armA, _armB);
            }
        }

        private void Leg(Transform thigh, Transform shin, Transform foot, float side, float dt)
        {
            var t = transform;
            Vector3 hip = thigh.position;
            Vector3 hipLocal = t.InverseTransformPoint(hip);
            float x = hipLocal.x * 1.05f;
            float legLen = _legA + _legB;
            float ph = _phase + (side > 0f ? 0f : Mathf.PI);

            // Ground gait (character space, feet on y = ankle height)
            float ankle = 0.08f;
            Vector3 dir = _moveDirS;
            float swing = Mathf.Sin(ph);
            float lift = Mathf.Max(0f, Mathf.Cos(ph)) * 0.2f * _moveS;
            float strideHalf = Mathf.Clamp(0.25f + _moveS * 0.25f, 0.25f, 0.5f) * _moveS;
            Vector3 ground = new Vector3(x, ankle + lift, 0.02f) + dir * swing * strideHalf;

            // Air: knees up, alternate legs
            Vector3 air = new Vector3(x, hipLocal.y - legLen * 0.62f, 0.12f + side * 0.1f);

            // Slide: lead (right) leg extended, rear leg folded under
            Vector3 slide = side > 0f
                ? new Vector3(x, ankle, hipLocal.z + legLen * 0.85f)
                : new Vector3(x, ankle, hipLocal.z + legLen * 0.25f);

            Vector3 local = Vector3.Lerp(ground, air, _airS);
            local = Vector3.Lerp(local, slide, _slideS);
            Vector3 target = t.TransformPoint(local);
            Vector3 pole = hip + t.forward * 1f + t.right * side * 0.1f;
            TwoBone(thigh, shin, foot, target, pole, _legA, _legB);
        }

        /// <summary>Analytic two-bone IK: rotates a (upper) and b (lower) so c (end) reaches the target, bending toward pole.</summary>
        private static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float lenA, float lenB)
        {
            Vector3 ap = a.position;
            Vector3 toT = target - ap;
            float d = toT.magnitude;
            if (d < 1e-4f) return;
            Vector3 dir = toT / d;
            d = Mathf.Clamp(d, Mathf.Abs(lenA - lenB) + 0.001f, lenA + lenB - 0.001f);
            Vector3 poleDir = Vector3.ProjectOnPlane(pole - ap, dir);
            if (poleDir.sqrMagnitude < 1e-6f) poleDir = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (poleDir.sqrMagnitude < 1e-6f) return;
            poleDir.Normalize();
            float cosA = Mathf.Clamp((lenA * lenA + d * d - lenB * lenB) / (2f * lenA * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = ap + dir * (lenA * cosA) + poleDir * (lenA * sinA);
            a.rotation = Quaternion.FromToRotation(b.position - ap, elbow - ap) * a.rotation;
            Vector3 end = ap + dir * d;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, end - b.position) * b.rotation;
        }

        private void OnDestroy()
        {
            if (_tint != null) Destroy(_tint);
        }
    }
}
