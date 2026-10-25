using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 행(버튼) 위 가운데에 붙는 이름 툴팁. 툴팁은 레이캐스트를 받지 않아 행의 올림을 가로채지 않는다.
    // 지금 붙은 행을 기억해, 다른 행으로 막 옮겨 간 툴팁은 앞 행의 벗어남으로 닫지 않는다. 순수 C#.
    internal sealed class RowTooltip
    {
        private readonly RectTransform _root;
        private readonly TMP_Text _text;
        // 툴팁과 행 사이의 틈.
        private readonly float _gap;
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _owner;

        // root: 툴팁 상자(없으면 아무것도 띄우지 않는다). text: 이름을 쓸 글자.
        public RowTooltip(RectTransform root, TMP_Text text, float gap)
        {
            _root = root;
            _text = text;
            _gap = gap;
        }

        public void Show(Component row, string text)
        {
            if (_root == null || row == null)
                return;

            _owner = (RectTransform)row.transform;

            if (_text != null)
                _text.text = text;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            _owner.GetWorldCorners(_corners);
            _root.pivot = new Vector2(0.5f, 0);
            _root.position = (_corners[1] + _corners[2]) * 0.5f;
            _root.anchoredPosition += new Vector2(0, _gap);
        }

        // 그 행에 붙어 있을 때만 닫는다.
        public void Hide(Component row)
        {
            if (row != null && row.transform == _owner)
                Hide();
        }

        public void Hide()
        {
            _owner = null;

            if (_root != null)
                _root.gameObject.SetActive(false);
        }
    }
}
