using UnityEngine;
using Vamp.Audio;
using Vamp.Movement;

namespace Vamp.Player
{
    /// <summary>
    /// Movement audio (and later movement VFX: speed lines, dash trail, wall-run sparks, landing dust).
    /// Purely presentational: listens to MovementController events, never changes gameplay.
    /// </summary>
    [RequireComponent(typeof(MovementController))]
    public sealed class PlayerFeedback : MonoBehaviour
    {
        [SerializeField] private float minLandingSoundSpeed = 4f;

        private MovementController _motor;

        private void Awake()
        {
            _motor = GetComponent<MovementController>();
        }

        private void OnEnable() { _motor.MovementEventRaised += OnMovementEvent; }
        private void OnDisable() { _motor.MovementEventRaised -= OnMovementEvent; }

        private void OnMovementEvent(MovementEvent e)
        {
            switch (e.Type)
            {
                case MovementEventType.Jumped:
                    AudioController.Play2D(SfxId.Jump, 0.5f, Random.Range(0.95f, 1.05f));
                    break;
                case MovementEventType.Landed:
                    if (e.Magnitude >= minLandingSoundSpeed)
                        AudioController.Play2D(SfxId.Land, Mathf.Clamp01(e.Magnitude / 20f), Random.Range(0.9f, 1.05f));
                    break;
                case MovementEventType.Dashed:
                    AudioController.Play2D(SfxId.Dash, 0.8f);
                    break;
                case MovementEventType.SlideStarted:
                    AudioController.Play2D(SfxId.Slide, 0.7f);
                    break;
                case MovementEventType.WallJumped:
                    AudioController.Play2D(SfxId.WallJump, 0.7f);
                    break;
                case MovementEventType.WallRunStarted:
                    AudioController.Play2D(SfxId.Slide, 0.4f, 1.4f);
                    break;
            }
        }
    }
}
