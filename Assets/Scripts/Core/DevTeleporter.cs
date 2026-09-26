using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vamp.Player;

namespace Vamp.Core
{
    /// <summary>
    /// Developer tool for test maps: F2..F9 teleport to named test areas (spawn is moved there too).
    /// Dev-only: reads the keyboard directly on purpose and compiles out of release builds.
    /// </summary>
    public sealed class DevTeleporter : MonoBehaviour
    {
        [Serializable]
        public struct Point
        {
            public string name;
            public Transform target;
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private Point[] points = new Point[0];

        public Point[] Points { get { return points; } }

#if UNITY_EDITOR || DEBUG
        private static readonly Key[] Keys = { Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9 };

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || player == null) return;
            for (int i = 0; i < Keys.Length && i < points.Length; i++)
            {
                if (!kb[Keys[i]].wasPressedThisFrame || points[i].target == null) continue;
                Transform t = points[i].target;
                float yaw = t.eulerAngles.y;
                player.SetSpawn(t.position, yaw);
                player.Movement.Teleport(t.position, yaw);
                player.View.SetView(yaw, 0f);
            }
        }
#endif
    }
}
