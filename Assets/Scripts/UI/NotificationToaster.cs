using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Audio;
using Vamp.Core;

namespace Vamp.UI
{
    /// <summary>Slide-in toasts for social / progression / system notifications (persistent across scenes).</summary>
    public sealed class NotificationToaster : MonoBehaviour
    {
        private sealed class Toast
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public float Age;
            public float Life;
        }

        private Canvas _canvas;
        private RectTransform _stack;
        private readonly List<Toast> _toasts = new List<Toast>();
        private bool _subscribed;

        private void Awake()
        {
            _canvas = UIFactory.CreateCanvas("Notification Canvas", transform, 950);
            _stack = UIKit.Node("Stack", _canvas.transform);
            _stack.anchorMin = _stack.anchorMax = new Vector2(1f, 1f);
            _stack.pivot = new Vector2(1f, 1f);
            _stack.anchoredPosition = new Vector2(-28f, -230f);
            _stack.sizeDelta = new Vector2(400f, 400f);
            var v = UIKit.VList(_stack, 8f);
            v.childAlignment = TextAnchor.UpperRight;
        }

        private void Update()
        {
            if (!_subscribed && Game.Notifications != null)
            {
                Game.Notifications.Posted += OnPosted;
                _subscribed = true;
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.Age += dt;
                float a = t.Age < 0.2f ? t.Age / 0.2f : t.Age > t.Life - 0.4f ? Mathf.Clamp01((t.Life - t.Age) / 0.4f) : 1f;
                t.Group.alpha = a;
                if (t.Age >= t.Life)
                {
                    Destroy(t.Root.gameObject);
                    _toasts.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && Game.Notifications != null) Game.Notifications.Posted -= OnPosted;
        }

        private void OnPosted(GameNotification n)
        {
            if (_toasts.Count >= 4)
            {
                Destroy(_toasts[0].Root.gameObject);
                _toasts.RemoveAt(0);
            }

            var panel = UIKit.Panel(_stack, "Toast", new Color(0.05f, 0.05f, 0.06f, 0.95f));
            var rt = panel.rectTransform;
            UIKit.Size(rt, 70f, 400f);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            bool bad = n.Kind == NotificationKind.Error || n.Kind == NotificationKind.Warning;
            var accent = UIKit.Image(rt, "Accent", bad ? UIKit.Red : (n.Kind == NotificationKind.LevelUp || n.Kind == NotificationKind.Unlock ? UIKit.Red : UIKit.Text));
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(3f, 0f);

            var title = UIKit.Label(rt, n.Title, 17, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.Stretch(title.rectTransform, 16f, 12f, 10f, 34f);
            var msg = UIKit.Label(rt, n.Message, 14, UIKit.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            UIKit.Stretch(msg.rectTransform, 16f, 12f, 36f, 6f);

            _toasts.Add(new Toast { Root = rt, Group = group, Life = bad ? 6f : 4f });

            switch (n.Kind)
            {
                case NotificationKind.LevelUp: AudioController.PlayUI(SfxId.LevelUp, 0.6f); break;
                case NotificationKind.Unlock:
                case NotificationKind.Challenge:
                case NotificationKind.Achievement: AudioController.PlayUI(SfxId.Unlock, 0.7f); break;
                case NotificationKind.Error: AudioController.PlayUI(SfxId.UIError); break;
                default: AudioController.PlayUI(SfxId.UIOpen, 0.6f); break;
            }
        }
    }
}
