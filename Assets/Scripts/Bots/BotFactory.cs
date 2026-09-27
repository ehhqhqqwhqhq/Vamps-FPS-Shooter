using UnityEngine;
using UnityEngine.AI;
using Vamp.Combat;
using Vamp.Match;
using Vamp.Progression;
using Vamp.UI;
using Vamp.Weapons;

namespace Vamp.Bots
{
    /// <summary>Builds bot characters at runtime (placeholder art: armoured capsule + head, team-coloured visor, name plate).</summary>
    public static class BotFactory
    {
        private static readonly string[] Names =
        {
            "Specter", "Nightjar", "Kestrel", "Havoc_9", "Wraith", "Ember", "Vex", "Onyx", "Razorback", "Halcyon",
            "Mortis", "Cinder", "Vanta", "Nyx_21", "Grim", "Solace", "Rook", "Talon", "Viper_X", "Shade"
        };

        private static Material _body, _head, _visorEnemy, _visorAlly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _body = _head = _visorEnemy = _visorAlly = null; }

        private static Material Mat(Color c, Color? emission = null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { color = c };
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }

        public static string NameFor(int index)
        {
            return Names[index % Names.Length] + (index >= Names.Length ? (index / Names.Length).ToString() : "");
        }

        public static Participant Create(int index, int team, BotDifficulty difficulty, WeaponData weapon, bool allyOfLocal, Vector3 position, float yaw)
        {
            var root = new GameObject("Bot_" + Names[index % Names.Length]);
            NavMeshHit nh;
            if (NavMesh.SamplePosition(position, out nh, 4f, NavMesh.AllAreas)) position = nh.position;
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var p = Build(root, NameFor(index), Random.Range(3, 90), team, difficulty, weapon, allyOfLocal, true);
            return p;
        }

        /// <summary>
        /// Builds a bot onto an existing object. <paramref name="brain"/> = NavMesh agent + AI (offline, or the online
        /// host); without it the bot is a network proxy that only shows the character and takes hits.
        /// </summary>
        public static Participant Build(GameObject root, string displayName, int level, int team, BotDifficulty difficulty, WeaponData weapon,
                                        bool allyOfLocal, bool brain)
        {
            if (_body == null)
            {
                _body = Mat(new Color(0.16f, 0.16f, 0.18f));
                _head = Mat(new Color(0.22f, 0.22f, 0.24f));
                _visorEnemy = Mat(new Color(1f, 0.1f, 0.15f), new Color(2.5f, 0.1f, 0.2f));
                _visorAlly = Mat(new Color(0.3f, 0.6f, 1f), new Color(0.4f, 0.9f, 2.5f));
            }

            if (brain)
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.baseOffset = 0f;
            }
            if (root.GetComponent<HealthController>() == null) root.AddComponent<HealthController>();
            var p = root.AddComponent<Participant>();
            p.IsBot = true;
            p.Team = team;
            p.DisplayName = displayName;
            p.Level = level;
            p.Ping = 0;
            var icons = CosmeticCatalog.OfType(CosmeticType.Icon);
            var eligible = icons.FindAll(i => i.Source == UnlockSource.Level && i.UnlockLevel <= p.Level);
            p.Icon = eligible.Count > 0 ? eligible[Random.Range(0, eligible.Count)].Id : "icon_vamp_symbol";
            p.Title = CosmeticCatalog.TierName(p.Level);
            p.ApplyIdentity();

            // Body (hitbox), head (head hitbox), visor
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.75f, 0.8f, 0.75f);
            body.GetComponent<Renderer>().sharedMaterial = _body;
            body.AddComponent<Hitbox>();

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = Vector3.one * 0.4f;
            head.GetComponent<Renderer>().sharedMaterial = _head;
            head.AddComponent<Hitbox>().Configure(true, 1f);

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            Object.Destroy(visor.GetComponent<Collider>());
            visor.transform.SetParent(head.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.05f, 0.42f);
            visor.transform.localScale = new Vector3(0.8f, 0.22f, 0.2f);
            visor.GetComponent<Renderer>().sharedMaterial = allyOfLocal ? _visorAlly : _visorEnemy;

            // Stick-man character (imported model) replaces the capsule visuals; the capsule/sphere stay as hitboxes.
            var ch = Characters.CharacterPresenter.Attach(root, false,
                allyOfLocal ? new Color(0.35f, 0.55f, 0.95f) : new Color(0.9f, 0.18f, 0.2f), 0.15f, Characters.CharacterSkins.RandomSkin());
            if (ch != null && ch.HasCharacter)
            {
                foreach (var go in new[] { body, head })
                {
                    Object.Destroy(go.GetComponent<MeshRenderer>());
                    Object.Destroy(go.GetComponent<MeshFilter>());
                }
                Object.Destroy(visor);
            }

            var eye = new GameObject("Eye").transform;
            eye.SetParent(root.transform, false);
            eye.localPosition = new Vector3(0f, 1.65f, 0.2f);

            if (brain)
            {
                var bot = root.AddComponent<BotController>();
                bot.Difficulty = difficulty;
                bot.Weapon = weapon;
                bot.Eye = eye;
            }

            var plate = new GameObject("NamePlate");
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var tm = plate.AddComponent<TextMesh>();
            tm.text = "[" + p.Level + "] " + p.DisplayName;
            tm.font = UIFactory.Font;
            tm.fontSize = 48;
            tm.characterSize = 0.03f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = allyOfLocal ? UIKit.AllyColor() : UIKit.EnemyColor();
            plate.GetComponent<MeshRenderer>().sharedMaterial = UIFactory.Font.material;
            plate.AddComponent<Billboard>();
            return p;
        }
    }

}
