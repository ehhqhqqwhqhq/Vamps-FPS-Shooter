using UnityEngine;
using Vamp.Core;
using Vamp.Movement;

namespace Vamp.Weapons
{
    /// <summary>
    /// Procedural first-person weapon presentation: placeholder block models, hip/ADS positioning,
    /// fire kick, reload dip, look sway and movement bob. Uses WeaponData.viewModelPrefab when assigned.
    /// Presentation only - never affects where bullets go.
    /// </summary>
    public sealed class WeaponViewModel : MonoBehaviour
    {
        [SerializeField] private WeaponController weapons;
        [SerializeField] private InputController input;
        [SerializeField] private MovementController movement;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material accentMaterial;

        [Header("Placement")]
        [SerializeField] private Vector3 hipPosition = new Vector3(0.2f, -0.2f, 0.42f);
        [SerializeField] private Vector3 adsPosition = new Vector3(0f, -0.13f, 0.32f);

        [Header("Motion")]
        [SerializeField] private float kickBack = 0.07f;
        [SerializeField] private float kickPitch = 8f;
        [SerializeField] private float kickRecover = 14f;
        [SerializeField] private float swayAmount = 0.02f;
        [SerializeField] private float swayMax = 4f;
        [SerializeField] private float bobAmount = 0.012f;
        [SerializeField] private float bobFrequency = 11f;

        private Transform _model;
        private Transform _muzzle;
        private float _kick;
        private Vector2 _sway;
        private float _bobPhase;
        private float _equipLower;
        private Vector3 _hip, _ads;
        private bool _melee;
        private float _stab;

        // Inspect (style only - no gameplay effect)
        private float _inspectT = -1f;
        private float _inspectDur = 2.6f;
        private WeaponData _data;
        // Butterfly knife
        private BalisongModel _balisong;
        private float _balEquip = 1f;
        // Butterfly knife with animated arms ("FPS Butterfly Knife" by BURNER, CC BY)
        private Animation _arms;
        private static GameObject _armsPrefab;
        // Skinned arms holding every other weapon (IK onto the grip points)
        private GameObject _gunArmsGo;
        private FirstPersonArms _gunArms;
        private Vector3 _rGrip, _lGrip;
        private Quaternion _rGripRot = Quaternion.identity, _lGripRot = Quaternion.identity;
        private bool _hasLeft;
        // Reload animation (magazine / shells / pump)
        private WeaponParts _parts;
        private float _shellBlend, _pumpT = 1f;
        private Transform _shell;
        private static bool _loadedArt;

        /// <summary>True while the inspect animation plays.</summary>
        public bool Inspecting { get { return _inspectT >= 0f; } }

        private void Awake()
        {
            if (weapons == null) weapons = GetComponentInParent<WeaponController>();
            if (input == null) input = GetComponentInParent<InputController>();
            if (movement == null) movement = GetComponentInParent<MovementController>();
        }

        private void OnEnable()
        {
            if (weapons == null) return;
            weapons.WeaponChanged += Rebuild;
            weapons.Fired += OnFired;
            if (weapons.Current != null) Rebuild(weapons.Current);
        }

        private void OnDisable()
        {
            if (weapons == null) return;
            weapons.WeaponChanged -= Rebuild;
            weapons.Fired -= OnFired;
        }

        private void OnFired(WeaponData d)
        {
            if (_arms != null && (_inspectT >= 0f || _arms.IsPlaying("Draw"))) PlayArms("Idle", 0.12f, 1f);
            _inspectT = -1f; // firing cancels an inspect
            if (d.delivery == DeliveryType.Melee) { _stab = 1f; return; }
            _kick = Mathf.Min(1.5f, _kick + (d.pelletsPerShot > 1 || d.delivery == DeliveryType.Projectile ? 1f : 0.45f));
            if (_parts != null && _parts.HasPump) _pumpT = -0.08f; // rack the pump just after the shot
        }

        private void Rebuild(WeaponData d)
        {
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            _balisong = null;
            _arms = null;
            if (_gunArmsGo != null) Destroy(_gunArmsGo);
            _gunArmsGo = null;
            _gunArms = null;
            _parts = null;
            _shell = null;
            _shellBlend = 0f;
            _pumpT = 1f;
            _inspectT = -1f;
            _data = d;
            if (d == null) return;
            _equipLower = 1f;

            LoadArt();
            GameObject root;
            GameObject camoTarget = null;
            if (d.id == "balisong" && _armsPrefab != null)
            {
                // Animated arms + butterfly knife: the rig's own camera sits at our origin.
                root = Instantiate(_armsPrefab, transform);
                root.name = "VM_balisong";
                _arms = root.GetComponentInChildren<Animation>();
                var knife = FindDeep(root.transform, "KnifeMesh");
                camoTarget = knife != null ? knife.gameObject : null;
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0.08f, -0.06f, 0.45f);
                _hip = _ads = Vector3.zero;
                if (_arms != null)
                {
                    PlayArms("Draw", 0f, 1.7f);
                    QueueIdle(0.35f);
                }
            }
            else if (d.id == "balisong")
            {
                // Procedural butterfly knife with a flip-open on every equip.
                root = new GameObject("VM_balisong");
                root.transform.SetParent(transform, false);
                _balisong = BalisongModel.Build(root.transform);
                var tip = _balisong.Tip;
                tip.name = "BladeTipPoint";
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0f, 0f, 0.2f);
                _hip = _ads = new Vector3(0.19f, -0.15f, 0.27f);
                _balEquip = 0f;
                _balisong.SetPose(180f, 0f);
            }
            else if (d.viewModelPrefab != null)
            {
                // Imported model: origin = grip, barrel +Z, real-world scale (see VampArtBuilder).
                root = Instantiate(d.viewModelPrefab, transform);
                foreach (var c in root.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.gameObject.layer = 2;
                }
                var sight = root.transform.Find("Sight");
                _ads = sight != null ? new Vector3(0f, -sight.localPosition.y - 0.005f, 0.24f) : adsPosition;
                _hip = ArmsFitting.Hip(d, false, hipPosition);
                if (d.delivery == DeliveryType.Melee) _ads = _hip;
            }
            else
            {
                root = new GameObject("VM_" + d.id);
                root.transform.SetParent(transform, false);
                BuildPlaceholder(root.transform, d, bodyMaterial, accentMaterial);
                _ads = adsPosition;
                _hip = ArmsFitting.Hip(d, true, hipPosition);
            }
            if (_arms == null) camoTarget = root;
            if (camoTarget != null) WeaponCamo.Apply(camoTarget, WeaponCamo.LocalFor(d));
            _melee = d.delivery == DeliveryType.Melee;
            if (_arms != null) GloveSkins.Apply(root, GloveSkins.Local);
            else if (_balisong == null) AttachArms(root.transform, d);
            _stab = 0f;
            _model = root.transform;
            _parts = root.GetComponent<WeaponParts>();
            if (_parts != null) _parts.ResetParts();
            _model.localPosition = _hip;
            _model.localRotation = Quaternion.identity;

            _muzzle = _model.Find("Muzzle");
            if (_muzzle == null)
            {
                _muzzle = new GameObject("Muzzle").transform;
                _muzzle.SetParent(_model, false);
                _muzzle.localPosition = new Vector3(0f, 0.03f, 0.45f);
            }
            if (weapons != null) weapons.SetMuzzle(_muzzle);
        }

        public static void BuildPlaceholder(Transform root, WeaponData d, Material bodyMaterial, Material accentMaterial)
        {
            // Rough silhouettes so weapons read differently at a glance.
            float length = 0.35f, height = 0.09f, width = 0.07f;
            if (d.delivery == DeliveryType.Melee) { length = 0.45f; height = 0.035f; width = 0.012f; } // blade
            else if (d.isSniper) { length = 0.8f; height = 0.08f; width = 0.06f; }             // sniper
            else if (d.usesHeat) { length = 0.42f; height = 0.12f; width = 0.1f; }             // energy
            else if (d.pelletsPerShot > 1) { length = 0.5f; height = 0.11f; width = 0.09f; }  // shotgun
            else if (d.delivery == DeliveryType.Projectile) { length = 0.6f; height = 0.14f; width = 0.14f; } // launcher
            else if (d.slot == WeaponSlot.Secondary) { length = 0.22f; height = 0.08f; width = 0.05f; }   // pistol

            Part(root, "Body", new Vector3(0f, 0f, length * 0.35f), new Vector3(width, height, length), bodyMaterial);
            // Grip sized for a hand (the arms wrap it). Blades get a handle behind the blade instead.
            if (d.delivery == DeliveryType.Melee)
                Part(root, "Grip", new Vector3(0f, 0f, -0.12f), new Vector3(0.024f, 0.03f, 0.11f), bodyMaterial);
            else
                Part(root, "Grip", new Vector3(0f, -height * 0.5f - 0.05f, 0f), new Vector3(0.032f, 0.11f, 0.045f), bodyMaterial);
            Part(root, "Accent", new Vector3(0f, height * 0.55f, length * 0.4f), new Vector3(width * 1.02f, height * 0.12f, length * 0.7f), accentMaterial);
            if (d.isSniper) Part(root, "Scope", new Vector3(0f, height * 1.1f, length * 0.25f), new Vector3(width * 0.8f, width * 0.8f, length * 0.35f), bodyMaterial);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, 0f, length * 0.85f);

            // Reloadable placeholders get a box magazine under the body (reload animation).
            if (d.delivery != DeliveryType.Melee && !d.usesHeat)
            {
                var parts = root.gameObject.AddComponent<WeaponParts>();
                var socket = new GameObject("MagSocket").transform;
                socket.SetParent(root, false);
                socket.localPosition = new Vector3(0f, -height * 0.5f, 0.09f);
                socket.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                Part(socket, "Mag", new Vector3(0f, 0f, 0.05f), new Vector3(0.04f, 0.05f, 0.1f), accentMaterial);
                parts.MagSocket = socket;
                parts.MagLength = 0.1f;
            }
        }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = 2; // Ignore Raycast
            if (Application.isPlaying) Destroy(go.GetComponent<Collider>()); else DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            if (_model == null || weapons == null) return;
            float dt = Time.deltaTime;

            _kick = Mathf.Lerp(_kick, 0f, 1f - Mathf.Exp(-kickRecover * dt));
            _equipLower = Mathf.MoveTowards(_equipLower, 0f, dt * 5f);

            Vector2 look = input != null ? input.Frame.LookDelta : Vector2.zero;
            Vector2 targetSway = Vector2.ClampMagnitude(-look * swayAmount, swayMax);
            _sway = Vector2.Lerp(_sway, targetSway, 1f - Mathf.Exp(-10f * dt));

            float speed = movement != null && movement.IsGrounded ? movement.HorizontalSpeed : 0f;
            _bobPhase += dt * bobFrequency * Mathf.Clamp01(speed / 8f);
            float bobScale = Mathf.Clamp01(speed / 10f) * (1f - weapons.AimBlend * 0.8f);
            Vector3 bob = new Vector3(Mathf.Cos(_bobPhase) * bobAmount, Mathf.Abs(Mathf.Sin(_bobPhase)) * bobAmount, 0f) * bobScale;

            // Reload: real choreography when the arms and moving parts exist, otherwise the old dip.
            bool animReload = _gunArms != null && _parts != null && (_parts.HasMag || _data.reloadPerShell);
            float reload = weapons.IsReloading && !animReload ? Mathf.Sin(weapons.ReloadProgress * Mathf.PI) : 0f;
            var rp = ReloadAnim.Idle(_lGrip, _lGripRot);
            if (animReload)
            {
                if (_data.reloadPerShell)
                {
                    _shellBlend = Mathf.MoveTowards(_shellBlend, weapons.IsReloading ? 1f : 0f, dt * 6f);
                    if (_shellBlend > 0f) rp = ReloadAnim.Shell(_parts, weapons.IsReloading ? weapons.ReloadProgress : 0f, _shellBlend, _lGrip, _lGripRot);
                }
                else if (weapons.IsReloading) rp = ReloadAnim.Magazine(_parts, weapons.ReloadProgress, _lGrip, _lGripRot);
                _parts.SetMag(rp.MagOut, rp.MagExtra, rp.MagExtraEuler, rp.MagVisible);
            }
            if (_parts != null && _parts.HasPump)
            {
                if (_pumpT < 1f) _pumpT = Mathf.Min(1f, _pumpT + dt / 0.42f);
                float pump = ReloadAnim.Pump(_pumpT);
                _parts.SetPump(pump);
                if (_shellBlend < 0.5f) rp.LeftPos += Vector3.back * (_parts.PumpTravel * pump); // left hand rides the pump
            }
            if (_gunArms != null) UpdateShell(rp);

            Vector3 pos = Vector3.Lerp(_hip, _ads, weapons.AimBlend) + bob;
            pos.z -= _kick * kickBack;
            pos.y -= (reload * 0.12f) + _equipLower * 0.25f;
            pos += rp.GunPos;
            // Melee: quick forward stab (out fast, back slower).
            float stab = 0f;
            if (_melee && _stab > 0f)
            {
                _stab = Mathf.MoveTowards(_stab, 0f, dt * 4f);
                float t = 1f - _stab;
                stab = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
            }
            pos += (_arms != null ? new Vector3(-0.05f, 0.03f, 0.2f) : new Vector3(-0.1f, 0.04f, 0.26f)) * stab;

            // ---- Inspect
            var frame = input != null ? input.Frame : default(PlayerInputFrame);
            bool busy = weapons.IsReloading || weapons.AimBlend > 0.2f || _equipLower > 0.3f || stab > 0f;
            if (frame.InspectPressed && !busy && _data != null)
            {
                _inspectT = 0f;
                _inspectDur = _balisong != null ? 3.4f : _melee ? 2.3f : 2.8f;
                if (_arms != null)
                {
                    string clip = Random.value < 0.5f ? "InspectA" : "InspectB";
                    var st = _arms[clip];
                    if (st != null)
                    {
                        _inspectDur = st.length;
                        PlayArms(clip, 0.2f, 1f);
                        QueueIdle(0.3f);
                    }
                }
            }
            if (_inspectT >= 0f && (weapons.IsReloading || weapons.AimBlend > 0.3f))
            {
                _inspectT = -1f;
                if (_arms != null) PlayArms("Idle", 0.15f, 1f);
            }
            Vector3 inspectPos = Vector3.zero, inspectEuler = Vector3.zero;
            if (_inspectT >= 0f)
            {
                _inspectT += dt;
                float u = _inspectT / _inspectDur;
                if (u >= 1f) _inspectT = -1f;
                else if (_arms != null) { }
                else if (_melee) KnifeInspect(u, out inspectPos, out inspectEuler);
                else GunInspect(u, out inspectPos, out inspectEuler);
            }
            pos += inspectPos;
            if (_balisong != null) AnimateBalisong(dt);
            _model.localPosition = pos;
            var rot = Quaternion.Euler(-_kick * kickPitch + reload * 35f + _sway.y, _sway.x, reload * -15f);
            // Knife held tip-up towards the centre of the screen; the stab straightens it out.
            if (_arms != null) rot *= Quaternion.Euler(-6f * stab, -4f * stab, 0f);
            else if (_melee) rot *= Quaternion.Euler(Mathf.Lerp(-22f, 4f, stab), Mathf.Lerp(-28f, -8f, stab), Mathf.Lerp(-35f, -10f, stab));
            rot *= Quaternion.Euler(inspectEuler);
            rot *= Quaternion.Euler(rp.GunEuler);
            _model.localRotation = rot;
            bool scoped = weapons.ShowScope;
            if (_model.gameObject.activeSelf == scoped) _model.gameObject.SetActive(!scoped);
            if (_gunArmsGo != null)
            {
                if (_gunArmsGo.activeSelf == scoped) _gunArmsGo.SetActive(!scoped);
                if (!scoped && _gunArms != null) _gunArms.Solve(_model, _rGrip, _rGripRot, _hasLeft, rp.LeftPos, rp.LeftRot);
            }
        }

        // ------------------------------------------------------------------ Arms / hands

        /// <summary>The shotgun shell in the left hand while feeding the tube.</summary>
        private void UpdateShell(ReloadAnim.Pose rp)
        {
            if (_shell == null)
            {
                if (!rp.ShellVisible) return;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = "Shell";
                Destroy(g.GetComponent<Collider>());
                g.layer = 2;
                var r = g.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                r.sharedMaterial = new Material(sh != null ? sh : Shader.Find("Standard")) { color = new Color(0.7f, 0.06f, 0.08f) };
                _shell = g.transform;
                _shell.SetParent(_model, false);
                _shell.localScale = new Vector3(0.019f, 0.032f, 0.019f);
                _shell.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (_shell.gameObject.activeSelf != rp.ShellVisible) _shell.gameObject.SetActive(rp.ShellVisible);
            _shell.localPosition = rp.ShellPos;
        }

        private static void LoadArt()
        {
            if (_loadedArt) return;
            _loadedArt = true;
            _armsPrefab = Resources.Load<GameObject>("ButterflyArms");
        }

        private void PlayArms(string clip, float fade, float speed)
        {
            if (_arms == null) return;
            var st = _arms[clip];
            if (st == null) return;
            st.speed = speed;
            if (fade <= 0f) { _arms.Stop(); _arms.Play(clip); }
            else _arms.CrossFade(clip, fade);
        }

        private void QueueIdle(float fade)
        {
            if (_arms == null || _arms["Idle"] == null) return;
            _arms.CrossFadeQueued("Idle", fade, QueueMode.CompleteOthers);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// Skinned arms (with the equipped gloves) on every weapon: the right hand grips the pistol grip, the left
        /// holds the handguard (long guns) or wraps the right hand (pistols). Knives: hammer grip, left hand in guard.
        /// </summary>
        private void AttachArms(Transform root, WeaponData d)
        {
            if (_armsPrefab == null) return;
            _gunArmsGo = Instantiate(_armsPrefab, transform);
            _gunArmsGo.name = "VM_arms";
            var knife = FindDeep(_gunArmsGo.transform, "KnifeMesh");
            if (knife != null) knife.gameObject.SetActive(false);
            GloveSkins.Apply(_gunArmsGo, GloveSkins.Local);
            _gunArms = FirstPersonArms.Create(_gunArmsGo);
            if (!_gunArms.Ready) { Destroy(_gunArmsGo); _gunArmsGo = null; _gunArms = null; return; }

            ArmsFitting.Compute(root, d, out _rGrip, out _rGripRot, out _hasLeft, out _lGrip, out _lGripRot);
        }

        // ------------------------------------------------------------------ Inspect animations

        private struct Key
        {
            public float T;
            public Vector3 Pos, Euler;
            public Key(float t, Vector3 pos, Vector3 euler) { T = t; Pos = pos; Euler = euler; }
        }

        private static readonly Key[] GunKeys =
        {
            new Key(0f,    Vector3.zero,                       Vector3.zero),
            new Key(0.16f, new Vector3(-0.11f, 0.06f, 0.03f),  new Vector3(-8f, -58f, 24f)),   // turn to show the left side
            new Key(0.46f, new Vector3(-0.1f, 0.065f, 0.035f), new Vector3(-14f, -68f, 30f)),  // hold
            new Key(0.64f, new Vector3(-0.09f, 0.08f, 0.04f),  new Vector3(12f, 48f, -36f)),   // flip to the right side
            new Key(0.86f, new Vector3(-0.085f, 0.075f, 0.04f),new Vector3(16f, 56f, -40f)),   // hold
            new Key(1f,    Vector3.zero,                       Vector3.zero),
        };

        private static readonly Key[] KnifeKeys =
        {
            new Key(0f,    Vector3.zero,                        Vector3.zero),
            new Key(0.15f, new Vector3(-0.1f, 0.05f, 0.02f),    new Vector3(10f, -40f, 60f)),    // show the blade
            new Key(0.3f,  new Vector3(-0.1f, 0.06f, 0.02f),    new Vector3(10f, -45f, 70f)),
            new Key(0.42f, new Vector3(-0.08f, 0.2f, 0.06f),    new Vector3(180f, -20f, 420f)),  // toss + spin
            new Key(0.56f, new Vector3(-0.08f, 0.05f, 0.03f),   new Vector3(360f, 0f, 720f)),    // catch
            new Key(0.8f,  new Vector3(-0.1f, 0.06f, 0.02f),    new Vector3(360f, 40f, 690f)),   // other side
            new Key(1f,    Vector3.zero,                        new Vector3(360f, 0f, 720f)),
        };

        private static void Eval(Key[] keys, float u, out Vector3 pos, out Vector3 euler)
        {
            int i = 0;
            while (i < keys.Length - 2 && u > keys[i + 1].T) i++;
            var a = keys[i];
            var b = keys[i + 1];
            float t = Mathf.InverseLerp(a.T, b.T, u);
            t = t * t * (3f - 2f * t);
            pos = Vector3.Lerp(a.Pos, b.Pos, t);
            euler = Vector3.Lerp(a.Euler, b.Euler, t);
        }

        private void GunInspect(float u, out Vector3 pos, out Vector3 euler)
        {
            Eval(GunKeys, u, out pos, out euler);
            // A little life while holding it.
            euler += new Vector3(Mathf.Sin(u * 17f) * 1.2f, Mathf.Sin(u * 11f) * 1.5f, 0f);
        }

        private void KnifeInspect(float u, out Vector3 pos, out Vector3 euler)
        {
            Eval(KnifeKeys, u, out pos, out euler);
        }

        // ------------------------------------------------------------------ Butterfly knife

        private struct BalKey
        {
            public float T, Blade, Latch, Roll;
            public BalKey(float t, float blade, float latch, float roll) { T = t; Blade = blade; Latch = latch; Roll = roll; }
        }

        // Inspect trick: close, flip open, close again, aerial spin, flip open.
        private static readonly BalKey[] BalTrick =
        {
            new BalKey(0f,    0f,   0f,    0f),
            new BalKey(0.12f, 180f, 360f,  0f),    // flip closed (latch swings round)
            new BalKey(0.24f, 0f,   0f,    0f),    // flip open
            new BalKey(0.36f, 180f, -360f, 90f),   // reverse flip closed
            new BalKey(0.46f, 180f, -360f, 90f),
            new BalKey(0.66f, 180f, -360f, 450f),  // aerial - spins closed in the air
            new BalKey(0.8f,  0f,   0f,    450f),  // snap open
            new BalKey(0.9f,  0f,   0f,    450f),
            new BalKey(1f,    0f,   0f,    360f),
        };

        private void AnimateBalisong(float dt)
        {
            float blade, latch, roll = 0f;
            if (_balEquip < 1f)
            {
                // Equip: flip it open
                _balEquip = Mathf.MoveTowards(_balEquip, 1f, dt / 0.55f);
                float e = _balEquip * _balEquip * (3f - 2f * _balEquip);
                blade = Mathf.Lerp(180f, 0f, e);
                latch = Mathf.Lerp(0f, -360f, e);
            }
            else if (_inspectT >= 0f)
            {
                float u = Mathf.Clamp01(_inspectT / _inspectDur);
                int i = 0;
                while (i < BalTrick.Length - 2 && u > BalTrick[i + 1].T) i++;
                var a = BalTrick[i];
                var b = BalTrick[i + 1];
                float t = Mathf.InverseLerp(a.T, b.T, u);
                t = t * t * (3f - 2f * t);
                blade = Mathf.Lerp(a.Blade, b.Blade, t);
                latch = Mathf.Lerp(a.Latch, b.Latch, t);
                roll = Mathf.Lerp(a.Roll, b.Roll, t);
            }
            else { blade = 0f; latch = 0f; }
            _balisong.SetPose(blade, latch);
            _balisong.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
