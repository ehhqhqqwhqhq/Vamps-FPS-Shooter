using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vamp.Audio;

namespace Vamp.UI
{
    /// <summary>Hover/selected animation for buttons: label nudge + accent bar + hover sound.</summary>
    public sealed class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Text Label;
        public Image Accent;
        public bool Slide;
        private bool _hover;
        private bool _selected;
        private float _t;

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; Refresh(); }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            var sel = GetComponent<Selectable>();
            if (sel == null || sel.interactable) AudioController.PlayUI(SfxId.UIHover, 0.35f);
            Refresh();
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Refresh();
        }

        private void OnDisable()
        {
            _hover = false;
            _t = 0f;
            Refresh();
        }

        private void Refresh()
        {
            if (Accent != null) Accent.enabled = _hover || _selected;
            if (Label != null && !Slide) Label.color = _selected ? UIKit.Text : (_hover ? UIKit.Text : new Color(0.9f, 0.9f, 0.9f, 1f));
        }

        private void Update()
        {
            if (!Slide || Label == null) return;
            float target = (_hover || _selected) ? 1f : 0f;
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime * 8f);
            var rt = Label.rectTransform;
            rt.offsetMin = new Vector2(18f + _t * 10f, rt.offsetMin.y);
        }
    }
}
