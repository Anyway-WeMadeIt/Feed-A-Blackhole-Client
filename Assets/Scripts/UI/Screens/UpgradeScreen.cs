using System;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class UpgradeScreen : UIRoot<UpgradeScreen.Refs>, IUIPageOwner, ILocalizedView, IStyleView, INumberFormatView
    {
        public enum Refs
        {
            Gold_Text,
            Hq_Text,
            StartBattleBtn_Button,
            NodeTooltip_Image,
            NodeTooltipTitle_Text,
            NodeTooltipBody_Text,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,
            // 배경 위의 별빛·궤도 문양(UIScreenAmbient). 테마에 따라 머티리얼만 바뀐다.
            Ambient,
            // 조작 안내. 코드는 쓰지 않고 UI 글자 표가 언어에 맞게 바꾼다(ViewTexts).
            Hint_Text,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            // 트리의 노드 그림은 여기 없다 — 트리 보기(NodeTreeView)의 NodeTreeLook이 정한다.
            Background_Image,
            GoldChip_Image,
            GoldIcon_Image,
            HqChip_Image,
            HqIcon_Image,
            StartBattleBtn_Image,
            StartBattleBtn_Text,
        }

        // 툴팁과 노드 사이의 틈.
        private const float TooltipGap = 8;

        public event Action StartBattleClicked;

        public RectTransform PageRoot => (RectTransform)transform;

        private TMP_Text _gold;
        private TMP_Text _hq;
        private RectTransform _tooltip;
        private TMP_Text _tooltipTitle;
        private TMP_Text _tooltipBody;
        private readonly Vector3[] _corners = new Vector3[4];

        // 언어가 바뀌면 다시 쓸 마지막 값. 아직 보이지 않았으면 null.
        private Localizer _localizer;
        private readonly StyledTexts _styled = new();
        // 테마 색: 화면 바탕색(배경 그림 뒤).
        private readonly ThemedImages _themed = new();
        // 노드 툴팁의 프리팹 폭. 글자 크기 설정만큼 넓힌다(원작처럼 상자가 글자를 따라 커진다).
        private float _tooltipWidth;
        private long? _shownGold;
        private (int Stage, int GoalLevel)? _shownHq;
        // 트리에서 보고 있는 구역의 열쇠(번역표 node.region.<열쇠>). 있으면 블랙홀 글자 자리에 구역 이름을 보인다.
        private string _shownRegion;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _gold = View.Text(Refs.Gold_Text);
            _hq = View.Text(Refs.Hq_Text);
            _tooltip = View.Rect(Refs.NodeTooltip_Image);
            _tooltipTitle = View.Text(Refs.NodeTooltipTitle_Text);
            _tooltipBody = View.Text(Refs.NodeTooltipBody_Text);
            _styled.Add(View.Text(Refs.Hint_Text), UITextRole.Caption);
            _themed.AddColor(GetComponent<Image>(), UIColorRole.Backdrop);
            _tooltipWidth = _tooltip != null ? _tooltip.sizeDelta.x : 0;
            HideNodeTooltip();

            BindEvent(View.Button(Refs.StartBattleBtn_Button), HandleStartBattleClicked);
        }

        private void HandleStartBattleClicked(PointerEventData _) => StartBattleClicked?.Invoke();

        // 화면이 닫힐 때 툴팁도 닫는다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable() => HideNodeTooltip();

        public void ShowGold(long gold)
        {
            _shownGold = gold;

            if (_gold != null)
                _gold.text = Localized("upgrade.gold", GoldText.Of(gold));
        }

        // 블랙홀(판 밖 진행): 성장도(도달한 이정표 수)와 다음 판의 목표 Level(다음 이정표의 Level, 0이면 마지막 이정표 뒤).
        // 산 노드 수는 결산 화면이 보여 준다.
        public void ShowHq(int stage, int goalLevel)
        {
            _shownHq = (stage, goalLevel);
            PaintHeader();
        }

        // 트리에서 보고 있는 구역(열쇠). null이면 블랙홀 글자로 돌아간다. 트리 보기가 열려 있는 동안 ScreenFlow가 넣는다.
        public void ShowRegion(string key)
        {
            _shownRegion = key;
            PaintHeader();
        }

        // 위쪽 가운데 글자: 보고 있는 구역이 있으면 그 이름, 없으면 블랙홀 성장도·목표 Level.
        private void PaintHeader()
        {
            if (_hq == null)
                return;

            if (!string.IsNullOrEmpty(_shownRegion))
            {
                _hq.text = Localized("node.region." + _shownRegion);
                return;
            }

            if (_shownHq is not (int stage, int goalLevel))
                return;

            string shownStage = stage.ToString(CultureInfo.InvariantCulture);
            _hq.text = goalLevel > 0
                ? Localized("upgrade.hq.goal", shownStage, goalLevel.ToString(CultureInfo.InvariantCulture))
                : Localized("upgrade.hq.last", shownStage);
        }

        // 마지막으로 보인 Gold·블랙홀 글자를 지금 언어로 다시 쓴다. 노드 툴팁은 닫는다(다시 올리면 새 언어로 뜬다).
        // Gold 표기 설정이 바뀌었다. 보이는 Gold를 다시 쓴다.
        public void RefreshNumbers()
        {
            if (_shownGold is long gold)
                ShowGold(gold);
        }

        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;
            HideNodeTooltip();

            if (_shownGold is long gold)
                ShowGold(gold);

            PaintHeader();
        }

        // 글자 크기·언어별 글꼴. TextBindings에 등록되지 않은 글자만 여기서 맞춘다(등록된 글자는 Theme가 맞춘다).
        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        public void ApplyStyle(in UIStyle style)
        {
            Ambient?.Apply(style.ThemeId);
            _styled.SetStyle(style);
            _themed.SetStyle(style);

            if (_tooltip != null)
                _tooltip.sizeDelta = new Vector2(_tooltipWidth * style.Scale, _tooltip.sizeDelta.y);
        }

        private string Localized(string key, params object[] args) =>
            _localizer != null ? _localizer.Format(key, args) : key;

        // 노드 위 가운데에 툴팁을 붙인다. 화면 위로 넘치면 노드 아래에 붙인다.
        // 툴팁은 레이캐스트를 받지 않아 노드의 올림을 가로채지 않는다.
        public void ShowNodeTooltip(RectTransform node, string title, string body)
        {
            if (_tooltip == null || node == null)
                return;

            if (_tooltipTitle != null)
                _tooltipTitle.text = title;

            if (_tooltipBody != null)
            {
                _tooltipBody.text = body;
                // 본문이 없으면(아직 닿지 않은 노드는 이름만) 글자 칸을 접어 상자가 제목만큼만 되게 한다.
                _tooltipBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);

            node.GetWorldCorners(_corners);
            Vector3 top = (_corners[1] + _corners[2]) * 0.5f;
            Vector3 bottom = (_corners[0] + _corners[3]) * 0.5f;

            _tooltip.pivot = new Vector2(0.5f, 0);
            _tooltip.position = top;
            _tooltip.anchoredPosition += new Vector2(0, TooltipGap);

            _tooltip.GetWorldCorners(_corners);
            float tooltipTop = _corners[1].y;
            ((RectTransform)transform).GetWorldCorners(_corners);

            if (tooltipTop > _corners[1].y)
            {
                _tooltip.pivot = new Vector2(0.5f, 1);
                _tooltip.position = bottom;
                _tooltip.anchoredPosition += new Vector2(0, -TooltipGap);
            }
        }

        public void HideNodeTooltip()
        {
            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
        }
    }
}
