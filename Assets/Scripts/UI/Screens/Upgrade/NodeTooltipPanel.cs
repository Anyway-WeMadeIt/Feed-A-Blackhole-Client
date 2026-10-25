using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 업그레이드 화면의 노드 툴팁(프리팹의 NodeTooltip)을 놓고 모양을 바꾼다. 상태(고정 여부·노드 상태)는 UpgradeScreen이 갖고, 이 클래스는 받은 값으로 그린다.
    // - 올림 툴팁(마우스): 노드 위 → (위로 넘치면) 노드 오른쪽 → (오른쪽으로 넘치면) 노드 아래.
    // - 고정 패널(터치): 왼쪽 고정 영역(골드 칩 아래). 테두리 그림 대신 단색 면, 큰 글자, 제목 옆 노드 그림.
    internal sealed class NodeTooltipPanel
    {
        // 툴팁과 노드 사이의 틈.
        private const float TooltipGap = 8;
        // 고정 패널(터치): 노드 위가 아니라 왼쪽 고정 영역. 골드 칩 아래부터 안내 글자 위까지, 왼쪽을 칩에 맞춘다. 위아래 틈.
        private const float DockGap = 16;
        // 고정 패널의 글자 크기 배율(툴팁 글자 기준). 폰에서 읽히게 키운다. 본문 색은 흰색 쪽으로 이만큼 섞는다.
        private const float PanelTitleScale = 1.3f;
        private const float PanelBodyScale = 1.15f;
        private const float PanelBodyBrighten = 0.35f;
        // 고정 패널의 최소 높이. 짧은 노드도 이보다 작아지지 않아 노드마다 크기가 널뛰지 않는다. 최대는 안내 글자 위까지.
        private const float PanelMinHeight = 300;
        // 패널 제목 왼쪽의 노드 그림 크기와 글자와의 틈.
        private const float IconSize = 48;
        private const float IconGap = 10;
        // 고정 패널 바탕(팔레트가 없을 때의 기본값): 바탕보다 살짝 밝은 면.
        private static readonly Color PanelColor = new(0.16f, 0.17f, 0.19f, 0.94f);

        private readonly RectTransform _tooltip;
        private readonly Image _image;
        private readonly TMP_Text _title;
        private readonly TMP_Text _body;
        private readonly ContentSizeFitter _fitter;
        // 고정 패널일 때 숨기는 조작 안내. 고정 패널의 아래 끝이기도 하다.
        private readonly TMP_Text _hint;
        // 이 화면(UpgradeScreen)의 루트. 올림 툴팁이 넘치는지 재는 기준이다.
        private readonly RectTransform _screen;
        // 고정 패널의 위 끝·왼쪽 기준.
        private readonly RectTransform _goldChip;
        // 고정 패널에서만 보이는 것: 제목 옆 노드 그림.
        private readonly Image _icon;
        // 노드 툴팁의 프리팹 폭. 글자 크기 설정만큼 넓힌다(원작처럼 상자가 글자를 따라 커진다).
        private readonly float _width;
        private readonly Vector3[] _corners = new Vector3[4];

        // 마지막으로 들어온 테마. 고정 패널의 면 색을 여기서 고른다.
        private UIAppliedTheme _theme;
        // 올림 툴팁의 프리팹 모양(테두리 그림·색). 고정 패널은 그림을 떼고 단색 면으로 그리고, 풀면 되돌린다.
        private Sprite _tooltipSprite;
        private Color _tooltipColor;
        private float _titleMarginLeft;
        // 패널로 바꾸기 직전의 글자 크기·본문 색. 고정 패널은 키우고 밝히며, 풀면 되돌린다.
        private float _titleBaseSize;
        private float _bodyBaseSize;
        private Color _bodyBaseColor;
        private bool _panelLookOn;

        // 고정 패널에서만 쓰는 제목 옆 노드 그림을 프리팹 툴팁 아래에 만든다(트리의 노드와 같은 방식). 제목 글자의 왼쪽 여백에 놓는다.
        public NodeTooltipPanel(RectTransform tooltip, Image image, TMP_Text title, TMP_Text body, TMP_Text hint, RectTransform screen, RectTransform goldChip)
        {
            _tooltip = tooltip;
            _image = image;
            _title = title;
            _body = body;
            _hint = hint;
            _screen = screen;
            _goldChip = goldChip;
            _fitter = _tooltip.GetComponent<ContentSizeFitter>();

            if (_title != null)
            {
                _titleMarginLeft = _title.margin.x;

                RectTransform iconRect = Child((RectTransform)_title.transform, "Icon");
                iconRect.anchorMin = new Vector2(0, 0.5f);
                iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f);
                iconRect.anchoredPosition = new Vector2(_titleMarginLeft, 0);
                iconRect.sizeDelta = new Vector2(IconSize, IconSize);
                _icon = iconRect.gameObject.AddComponent<Image>();
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
                iconRect.gameObject.SetActive(false);
            }

            _width = _tooltip.sizeDelta.x;
        }

        // 구매 탭을 올려놓을 자리(툴팁 상자).
        public RectTransform Root => _tooltip;

        // 테마가 들어왔다: 고정 패널의 면 색에 쓸 테마를 받고, 툴팁 폭을 글자 배율만큼 넓힌다.
        public void ApplyTheme(in UIAppliedTheme theme)
        {
            _theme = theme;
            _tooltip.sizeDelta = new Vector2(_width * theme.TextScale, _tooltip.sizeDelta.y);
        }

        // 제목 옆 노드 그림(고정 패널). 트리에 그려진 노드 그림이다.
        public void SetIcon(Sprite icon)
        {
            if (_icon == null)
                return;

            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        // 글자를 넣고 올림 툴팁 ↔ 고정 패널의 모양을 고른다. 자리는 Place가 정한다.
        public void SetContent(string title, string body, bool pinned)
        {
            if (_title != null)
                _title.text = title;

            if (_body != null)
            {
                _body.text = body;
                // 본문이 없으면(아직 닿지 않은 노드는 이름만) 글자 칸을 접어 상자가 제목만큼만 되게 한다.
                _body.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }

            SetPanelLook(pinned);
        }

        // 내용에 맞춰 크기를 정하고 자리에 놓는다. 고정 패널은 왼쪽 고정 자리, 올림 툴팁은 노드 곁.
        public void Place(RectTransform node, bool pinned)
        {
            // 높이는 먼저 내용에 맞춘다. DockLeft가 최소·최대로 고정하면서 Unconstrained로 바꿔 두므로, 내용이 바뀔 때마다 되돌려야
            // 이전 높이에 새 내용이 욱여넣어지지(제목 칸이 찌그러져 본문과 겹침) 않는다.
            if (_fitter != null)
                _fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            // 글자 크기·여백·내용이 방금 바뀌었다. 한 번의 배치로는 TMP가 이전 폭·여백 기준의 선호 높이를 줄 수 있어(제목 세 줄이 두 줄 칸에 들어감)
            // 배치 → 글자 다시 재기 → 배치를 한 번 더 한다.
            RebuildLayout();
            RebuildLayout();

            if (pinned)
            {
                DockLeft();
                return;
            }

            // 올림 툴팁의 자리: 노드 위 → (위로 넘치면) 노드 오른쪽 → (오른쪽으로 넘치면) 노드 아래. 화면 안에 드는 첫 자리를 쓴다.
            node.GetWorldCorners(_corners);
            Vector3 top = (_corners[1] + _corners[2]) * 0.5f;
            Vector3 bottom = (_corners[0] + _corners[3]) * 0.5f;
            Vector3 right = (_corners[2] + _corners[3]) * 0.5f;

            Attach(new Vector2(0.5f, 0), top, new Vector2(0, TooltipGap));
            if (!Overflows(Edge.Top))
                return;

            Attach(new Vector2(0, 0.5f), right, new Vector2(TooltipGap, 0));
            if (!Overflows(Edge.Right))
                return;

            Attach(new Vector2(0.5f, 1), bottom, new Vector2(0, -TooltipGap));
        }

        public void Hide()
        {
            SetPanelLook(false);
            _tooltip.gameObject.SetActive(false);
        }

        // 왼쪽 고정 자리: 위는 골드 칩 아래(틈만큼), 아래는 안내 글자 위(틈만큼), 왼쪽은 칩에 맞춘다. 높이는 그 사이를 다 쓴다
        // — 설명 길이와 무관하게 패널 크기가 같다. 칩·안내가 없으면 화면 가장자리에서 틈만큼 안쪽.
        private void DockLeft()
        {
            var parent = (RectTransform)_tooltip.parent;

            _screen.GetWorldCorners(_corners);
            Vector3 topLeft = parent.InverseTransformPoint(_corners[1]) + new Vector3(DockGap, -DockGap, 0);
            float bottom = parent.InverseTransformPoint(_corners[0]).y + DockGap;

            if (_goldChip != null)
            {
                _goldChip.GetWorldCorners(_corners);
                topLeft = parent.InverseTransformPoint(_corners[0]) + new Vector3(0, -DockGap, 0);
            }

            // 구매 탭이 패널 위로 튀어나오는 만큼 패널을 내린다(골드 칩과 탭이 겹치지 않게).
            topLeft.y -= NodeBuyTab.Rise;

            if (_hint != null)
            {
                ((RectTransform)_hint.transform).GetWorldCorners(_corners);
                bottom = parent.InverseTransformPoint(_corners[1]).y + DockGap;
            }

            // 높이는 내용만큼(ContentSizeFitter). 안내 글자 위까지가 상한 — 넘으면 거기서 자른다.
            float available = topLeft.y - bottom;
            float preferred = LayoutUtility.GetPreferredHeight(_tooltip);

            _tooltip.pivot = new Vector2(0, 1);

            // 내용이 최소보다 짧거나 상한보다 길면 그 값으로 고정한다. 그 사이면 내용 높이 그대로.
            float clamped = Mathf.Clamp(preferred, Mathf.Min(PanelMinHeight, available), available);

            if (_fitter != null && !Mathf.Approximately(clamped, preferred))
            {
                _fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                _tooltip.sizeDelta = new Vector2(_tooltip.sizeDelta.x, clamped);
            }

            _tooltip.localPosition = new Vector3(topLeft.x, topLeft.y, 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);
        }

        private enum Edge { Top, Right }

        // 툴팁의 pivot을 두고 노드의 한 점에 붙인 뒤 틈만큼 띄운다.
        private void Attach(Vector2 pivot, Vector3 anchor, Vector2 gap)
        {
            _tooltip.pivot = pivot;
            _tooltip.position = anchor;
            _tooltip.anchoredPosition += gap;
        }

        // 툴팁이 화면(이 루트)의 그 가장자리 밖으로 나가는가.
        private bool Overflows(Edge edge)
        {
            _tooltip.GetWorldCorners(_corners);
            float tooltipTop = _corners[1].y;
            float tooltipRight = _corners[2].x;
            _screen.GetWorldCorners(_corners);

            return edge == Edge.Top ? tooltipTop > _corners[1].y : tooltipRight > _corners[2].x;
        }

        private void RebuildLayout()
        {
            _title?.ForceMeshUpdate(true, true);
            _body?.ForceMeshUpdate(true, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);
        }

        // 올림 툴팁 ↔ 고정 패널의 모양을 바꾼다. 패널: 테두리 그림 대신 단색 면, 레이캐스트 받음, 그림 켜고, 안내 글자 숨김,
        // 높이는 DockLeft가 정하므로 크기 맞춤을 끈다. 툴팁: 전부 되돌린다.
        // 되돌릴 값(툴팁 그림·색, 글자 크기·색, 제목 여백)은 패널로 바꾸기 직전의 것을 잡는다 — 테마가 초기화 뒤에 바꾼 값이 그대로 돌아오게.
        // 패널로 바꾼 적이 없으면 되돌릴 것도 없다(PC의 올림 툴팁은 건드리지 않는다).
        private void SetPanelLook(bool panel)
        {
            if (panel == _panelLookOn)
                return;

            _panelLookOn = panel;

            if (panel)
            {
                if (_image != null)
                {
                    _tooltipSprite = _image.sprite;
                    _tooltipColor = _image.color;
                }

                if (_title != null)
                {
                    _titleBaseSize = _title.fontSize;
                    _titleMarginLeft = _title.margin.x;
                }

                if (_body != null)
                {
                    _bodyBaseSize = _body.fontSize;
                    _bodyBaseColor = _body.color;
                }
            }

            if (_image != null)
            {
                _image.raycastTarget = panel;
                _image.sprite = panel ? null : _tooltipSprite;
                _image.color = panel ? PanelBackColor() : _tooltipColor;
            }

            if (_title != null)
            {
                Vector4 margin = _title.margin;
                margin.x = panel ? _titleMarginLeft + IconSize + IconGap : _titleMarginLeft;
                _title.margin = margin;
                _title.fontSize = panel ? _titleBaseSize * PanelTitleScale : _titleBaseSize;
            }

            if (_body != null)
            {
                _body.fontSize = panel ? _bodyBaseSize * PanelBodyScale : _bodyBaseSize;
                _body.color = panel ? Color.Lerp(_bodyBaseColor, Color.white, PanelBodyBrighten) : _bodyBaseColor;
            }

            if (_icon != null && _icon.gameObject.activeSelf != panel)
                _icon.gameObject.SetActive(panel);

            if (_hint != null && _hint.gameObject.activeSelf == panel)
                _hint.gameObject.SetActive(!panel);
        }

        private Color PanelBackColor()
        {
            Color? themed = _theme.ColorFor(UIColorRole.Surface);

            if (themed is not Color color)
                return PanelColor;

            color.a = PanelColor.a;
            return color;
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
