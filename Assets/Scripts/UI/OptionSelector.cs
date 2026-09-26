using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vamp.Audio;

namespace Vamp.UI
{
    /// <summary>Backing component for UIKit.Selector.</summary>
    public sealed class OptionSelector : MonoBehaviour
    {
        private IList<string> _options;
        private int _index;
        private Text _value;
        private Action<int> _onChanged;

        public int Index { get { return _index; } }

        public void Init(IList<string> options, int index, Text value, Action<int> onChanged)
        {
            _options = options;
            _value = value;
            _onChanged = onChanged;
            SetIndexWithoutNotify(index);
        }

        public void SetOptions(IList<string> options, int index)
        {
            _options = options;
            SetIndexWithoutNotify(index);
        }

        public void SetIndexWithoutNotify(int index)
        {
            if (_options == null || _options.Count == 0) { _index = 0; if (_value != null) _value.text = "-"; return; }
            _index = Mathf.Clamp(index, 0, _options.Count - 1);
            if (_value != null) _value.text = _options[_index];
        }

        public void Step(int dir)
        {
            if (_options == null || _options.Count == 0) return;
            _index = (_index + dir + _options.Count) % _options.Count;
            _value.text = _options[_index];
            if (_onChanged != null) _onChanged(_index);
        }
    }
}
