using System;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class UpgradeScreen : UIRoot<UpgradeScreen.Refs>, IUIPageOwner, ILocalizedView, INumberFormatView
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
        // 고정 패널(터치): 노드 위가 아니라 왼쪽 고정 영역. 골드 칩 아래부터 안내 글자 위까지, 왼쪽을 칩에 맞춘다. 위아래 틈.
        private const float DockGap = 16;
        // 구매 탭(고정했을 때만 보인다): 패널 위쪽 오른쪽 모서리에 걸쳐 올라앉는 작은 상자 "구매 70". 비용 표시이자 구매 버튼이다.
        // 패널 위로 (높이 − 겹침)만큼 튀어나온다. 손가락 기준 높이.
        private const float BuyTabHeight = 56;
        private const float BuyTabOverlap = 6;
        private const float BuyTabPaddingX = 22;
        private const float BuyTabMinWidth = 120;
        private const float BuyFontSize = 28;
        // 고정 패널의 글자 크기 배율(툴팁 글자 기준). 폰에서 읽히게 키운다. 본문 색은 흰색 쪽으로 이만큼 섞는다.
        private const float PanelTitleScale = 1.3f;
        private const float PanelBodyScale = 1.15f;
        private const float PanelBodyBrighten = 0.35f;
        // 고정 패널의 최소 높이. 짧은 노드도 이보다 작아지지 않아 노드마다 크기가 널뛰지 않는다. 최대는 안내 글자 위까지.
        private const float PanelMinHeight = 300;
        // 패널 제목 왼쪽의 노드 그림 크기와 글자와의 틈.
        private const float IconSize = 48;
        private const float IconGap = 10;

        public event Action StartBattleClicked;
        // 고정한 툴팁의 구매 버튼을 눌렀다.
        public event Action BuyConfirmed;

        public RectTransform PageRoot => (RectTransform)transform;

        private TMP_Text _gold;
        private TMP_Text _hq;
        private RectTransform _tooltip;
        private TMP_Text _tooltipTitle;
        private TMP_Text _tooltipBody;
        private Image _tooltipImage;
        private ContentSizeFitter _tooltipFitter;
        private TMP_Text _hint;
        // 올림 툴팁의 프리팹 모양(테두리 그림·색). 고정 패널은 그림을 떼고 단색 면으로 그리고, 풀면 되돌린다.
        private Sprite _tooltipSprite;
        private Color _tooltipColor;
        // 고정 패널에서만 보이는 것: 제목 옆 노드 그림.
        private Image _icon;
        // 구매 탭. 프리팹에 없고 코드가 툴팁 아래에 만든다(트리의 노드와 같은 방식). 누르는 즉시 산다.
        private RectTransform _buy;
        private Button _buyButton;
        private ButtonAnimation _buyAnimation;
        private Image _buyBack;
        private TMP_Text _buyLabel;
        // 고른 노드의 다음 Rank 비용(구매 탭 글자 "구매 70"). 없으면(최대·잠김) 탭 글자는 상태만.
        // 구매 준비(탭을 누르는 중, 노드를 꾹 눌러 시간이 참)면 골드 칩이 산 뒤의 값을 황금색으로 미리 보인다.
        private long? _pendingCost;
        // 구매 준비 중 골드 칩에 미리 보이는 "산 뒤 골드"의 색: 은은한 황금색. 확정값(테마 글자색)과 구분되되 경고색은 아니다.
        private const string PreviewGoldColor = "#E3C985";
        // 구매 준비에 들어갈 때 골드가 산 뒤 값으로 줄어드는 시간(초).
        private const float GoldCountSeconds = 0.3f;
        private bool _goldPreviewing;
        private Coroutine _goldCount;
        // 사려 했지만 Gold가 모자란 상태(노드를 꾹 눌러 시간이 참 / 그런 노드의 구매 탭을 누르는 중): 골드 숫자는 부드러운 붉은색으로 살짝 커졌다
        // 돌아오고, 구매 탭은 어두운 붉은색. 떼면 원래 색으로 되돌아온다(0.15초). 점멸하지 않는다.
        private static readonly Color ShortGoldColor = new(0.9f, 0.5f, 0.5f, 1);
        private static readonly Color ShortBuyColor = new(0.66f, 0.31f, 0.31f, 0.96f);
        private const float ShortBumpScale = 1.05f;
        private const float ShortBumpSeconds = 0.1f;
        private const float ShortRestoreSeconds = 0.15f;
        private bool _goldShort;
        // 숫자 색의 섞임(0 = 평소, 1 = 붉음). 들어갈 땐 바로 1, 나올 땐 0.15초에 걸쳐 0으로.
        private float _goldShortMix;
        private Coroutine _goldShortRoutine;
        // 구매 탭 바탕의 기준 색(상태별). 누르는 동안은 어둡게, 샀을 때 번쩍인 뒤 기준 색으로 돌아온다.
        private Color _buyBaseColor;
        private bool _buyPressed;
        private Coroutine _buyFlash;
        private const float BuyFlashSeconds = 0.35f;
        // 누르는 동안 바탕 밝기(기준 색에 곱한다).
        private const float BuyPressedDarken = 0.72f;
        private float _titleMarginLeft;
        // 패널로 바꾸기 직전의 글자 크기·본문 색. 고정 패널은 키우고 밝히며, 풀면 되돌린다.
        private float _titleBaseSize;
        private float _bodyBaseSize;
        private Color _bodyBaseColor;
        private bool _panelLookOn;
        // 고정 패널 바탕(팔레트가 없을 때의 기본값): 바탕보다 살짝 밝은 면.
        private static readonly Color PanelColor = new(0.16f, 0.17f, 0.19f, 0.94f);
        // 구매 탭 바탕: 살 수 있으면 강조색(초록), 아니면 패널과 같은 어두운 면. 팔레트가 없을 때의 기본값.
        private static readonly Color BuyReadyColor = new(0.34f, 0.59f, 0.41f, 0.96f);
        private static readonly Color BuyBlockedColor = new(0.22f, 0.23f, 0.26f, 0.96f);
        // 툴팁을 터치로 고정했는가. 고정 중에는 올림으로 바뀌지 않고 레이캐스트를 받아 뒤의 노드를 가린다.
        public bool IsTooltipPinned { get; private set; }
        private NodeState _pinnedState;
        private readonly Vector3[] _corners = new Vector3[4];

        // 언어가 바뀌면 다시 쓸 마지막 값. 아직 보이지 않았으면 null.
        private Localizer _localizer;
        // 마지막으로 들어온 테마. 코드로 칠하는 패널·구매 탭의 색을 여기서 고른다(OnPresented).
        private UIAppliedTheme _theme;
        // 노드 툴팁의 프리팹 폭. 글자 크기 설정만큼 넓힌다(원작처럼 상자가 글자를 따라 커진다).
        private float _tooltipWidth;
        private long? _shownGold;
        // Gold 표기(설정). UISettingsSync가 넣는다.
        private NumberFormat _numberFormat;
        private (int Stage, int GoalLevel)? _shownHq;
        // 트리에서 보고 있는 구역의 열쇠(번역표 node.region.<열쇠>). 있으면 블랙홀 글자 자리에 구역 이름을 보인다.
        private string _shownRegion;

        protected override void OnInitialize()
        {
            _gold = View.Text(Refs.Gold_Text);
            _hq = View.Text(Refs.Hq_Text);
            _tooltip = View.Rect(Refs.NodeTooltip_Image);
            _tooltipImage = View.Image(Refs.NodeTooltip_Image);
            _tooltipTitle = View.Text(Refs.NodeTooltipTitle_Text);
            _tooltipBody = View.Text(Refs.NodeTooltipBody_Text);
            _hint = View.Text(Refs.Hint_Text);
            CreatePanelParts();
            // 안내 글자와 화면 바탕색(배경 그림 뒤)은 Styled가 테마에 맞춘다. TextBindings에 등록된 글자는 Theme가 맞춘다.
            Styled.AddText(_hint, UITextRole.Caption, UITextSizePolicy.Prefab);
            Styled.AddImageColor(GetComponent<Image>(), UIColorRole.Backdrop);
            _tooltipWidth = _tooltip != null ? _tooltip.sizeDelta.x : 0;
            HideNodeTooltip();

            BindEvent(View.Button(Refs.StartBattleBtn_Button), HandleStartBattleClicked);
        }

        private void HandleStartBattleClicked(PointerEventData _) => StartBattleClicked?.Invoke();

        // 화면이 닫힐 때 툴팁도 닫는다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable()
        {
            HideNodeTooltip();
            // 꺼지면 코루틴이 죽는다. 붉은 색·크기가 남지 않게.
            _goldShort = false;
            _goldShortMix = 0;
            _goldShortRoutine = null;
            _goldCount = null;

            if (_gold != null)
                _gold.transform.localScale = Vector3.one;
        }

        public void ShowGold(long gold)
        {
            _shownGold = gold;
            PaintGold();
        }

        // 고른 노드의 다음 Rank 비용. 구매 탭 글자와 골드 미리보기에 쓴다. null이면 없다.
        public void ShowPendingCost(long? cost)
        {
            _pendingCost = cost;
            PaintGold();
            PaintBuyButton();
        }

        // 골드 글자. 구매 준비에 "들어가는 순간"에만 보유 골드에서 산 뒤 골드로 줄어드는 연출을 하고, 그 밖의 모든 갱신(뗌·구매·테마)은
        // 즉시 쓰며 진행 중인 연출을 끊는다 — 산 뒤 오는 ShowGold(실제 골드)는 연출의 목표값과 같아 숫자가 튀지 않는다.
        private void PaintGold()
        {
            if (_gold == null || !(_shownGold is long gold))
                return;

            bool preview = _buyPressed && _pendingCost is long spend && spend <= gold;
            long target = preview ? gold - _pendingCost.Value : gold;
            bool entering = preview && !_goldPreviewing;
            _goldPreviewing = preview;

            StopGoldCount();

            if (entering)
            {
                _goldCount = StartCoroutine(CountGold(gold, target));
                return;
            }

            WriteGold(target, preview);
        }

        // 글자색은 테마가 정하므로 숫자만 리치 텍스트로 칠한다. 모자람(붉게, 섞임만큼) > 미리보기(황금) > 평소.
        private void WriteGold(long amount, bool preview)
        {
            string number = GoldText.Of(amount, _numberFormat);
            string text;

            if (_goldShortMix > 0)
            {
                Color mixed = Color.Lerp(_gold.color, ShortGoldColor, _goldShortMix);
                text = $"<color=#{ColorUtility.ToHtmlStringRGB(mixed)}>{number}</color>";
            }
            else
            {
                text = preview ? $"<color={PreviewGoldColor}>{number}</color>" : number;
            }

            _gold.text = Localized(UIText.Upgrade.Gold, text);
        }

        // 사려 했지만 Gold가 모자란 상태로 들어가거나(on) 나온다(off). 골드 숫자 색·살짝 커짐, 구매 탭 색을 함께 바꾼다.
        public void SetGoldShort(bool on)
        {
            if (_goldShort == on || _gold == null)
                return;

            _goldShort = on;

            if (_goldShortRoutine != null)
                StopCoroutine(_goldShortRoutine);

            _goldShortRoutine = isActiveAndEnabled ? StartCoroutine(on ? EnterGoldShort() : LeaveGoldShort()) : null;

            if (!isActiveAndEnabled)
            {
                _goldShortMix = on ? 1 : 0;
                PaintGold();
            }

            PaintBuyBack();
        }

        private System.Collections.IEnumerator EnterGoldShort()
        {
            _goldShortMix = 1;
            PaintGold();
            Transform scaled = _gold.transform;
            float elapsed = 0;

            // 살짝 커졌다 돌아온다.
            while (elapsed < ShortBumpSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ShortBumpSeconds);
                float scale = 1 + (ShortBumpScale - 1) * Mathf.Sin(t * Mathf.PI);
                scaled.localScale = new Vector3(scale, scale, 1);
                yield return null;
            }

            scaled.localScale = Vector3.one;
            _goldShortRoutine = null;
        }

        private System.Collections.IEnumerator LeaveGoldShort()
        {
            _gold.transform.localScale = Vector3.one;
            float elapsed = 0;

            while (elapsed < ShortRestoreSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _goldShortMix = 1 - Mathf.Clamp01(elapsed / ShortRestoreSeconds);
                PaintGold();
                yield return null;
            }

            _goldShortMix = 0;
            PaintGold();
            _goldShortRoutine = null;
        }

        private System.Collections.IEnumerator CountGold(long from, long to)
        {
            float elapsed = 0;

            while (elapsed < GoldCountSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / GoldCountSeconds);
                t = 1 - (1 - t) * (1 - t);
                WriteGold(from + (long)((to - from) * (double)t), preview: true);
                yield return null;
            }

            WriteGold(to, preview: true);
            _goldCount = null;
        }

        private void StopGoldCount()
        {
            if (_goldCount == null)
                return;

            StopCoroutine(_goldCount);
            _goldCount = null;
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
                _hq.text = Localized(UIText.Node.Region(_shownRegion));
                return;
            }

            if (_shownHq is not (int stage, int goalLevel))
                return;

            // 이정표 이름으로: "소행성대 → 행성계 (목표 Lv 10)". 마지막 이정표 뒤면 이름 하나.
            string current = Localized(UIText.Milestone.Name(stage));
            _hq.text = goalLevel > 0
                ? Localized(UIText.Upgrade.HqGoal, current, Localized(UIText.Milestone.Name(stage + 1)), goalLevel.ToString(CultureInfo.InvariantCulture))
                : Localized(UIText.Upgrade.HqLast, current);
        }

        // 마지막으로 보인 Gold·블랙홀 글자를 지금 언어로 다시 쓴다. 노드 툴팁은 닫는다(다시 올리면 새 언어로 뜬다).
        // Gold 표기 설정. 보이는 Gold와 구매 탭을 새 표기로 다시 쓴다.
        public void SetNumberFormat(NumberFormat format)
        {
            _numberFormat = format;

            if (_shownGold is long gold)
                ShowGold(gold);

            PaintBuyButton();
        }

        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;
            HideNodeTooltip();

            if (_shownGold is long gold)
                ShowGold(gold);

            PaintHeader();
        }

        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        // 테마가 들어왔다: 문양 머티리얼, 코드로 칠하는 구매 탭·Gold 색을 다시 칠하고 툴팁 폭을 글자 배율만큼 넓힌다.
        protected override void OnPresented(in UIAppliedTheme theme)
        {
            _theme = theme;
            Ambient?.Apply(theme.ThemeId);
            PaintBuyButton();
            PaintGold();

            if (_tooltip != null)
                _tooltip.sizeDelta = new Vector2(_tooltipWidth * theme.TextScale, _tooltip.sizeDelta.y);
        }

        private string Localized(string key, params object[] args) =>
            _localizer != null ? _localizer.Format(key, args) : key;

        // 올림 툴팁: 노드 위 가운데에 붙인다. 화면 위로 넘치면 노드 아래에 붙인다.
        // 레이캐스트를 받지 않아 노드의 올림을 가로채지 않는다. 구매 버튼은 없다(PC는 노드를 눌러 산다). 고정 중이면 무시한다.
        public void ShowNodeTooltip(RectTransform node, string title, string body)
        {
            if (IsTooltipPinned)
                return;

            Place(node, title, body, pinned: false);
        }

        // 고정 툴팁(터치): 왼쪽 고정 영역(골드 칩 아래)에 두고 다른 곳을 누를 때까지 남는다. 레이캐스트를 받아 뒤의 노드를 가리며, 구매 버튼이 보인다.
        // 손가락이 노드 주변을 가리는 터치에서는 노드 위에 띄우는 것보다 늘 같은 자리에 있는 편이 읽고 누르기 쉽다.
        // state: 버튼 글자·활성을 정한다(Purchasable 구매 / Revealed 구매(흐림) / Owned 최대 / Hidden 잠김).
        // icon: 트리에 그려진 노드 그림(제목 옆). state: 버튼 글자·활성. 비용은 골드 칩 옆에 따로 보인다(ShowPendingCost).
        public void PinNodeTooltip(RectTransform node, Sprite icon, string title, string body, NodeState state)
        {
            _pinnedState = state;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            Place(node, title, body, pinned: true);
        }

        private void Place(RectTransform node, string title, string body, bool pinned)
        {
            if (_tooltip == null || node == null)
                return;

            IsTooltipPinned = pinned;

            if (_tooltipTitle != null)
                _tooltipTitle.text = title;

            if (_tooltipBody != null)
            {
                _tooltipBody.text = body;
                // 본문이 없으면(아직 닿지 않은 노드는 이름만) 글자 칸을 접어 상자가 제목만큼만 되게 한다.
                _tooltipBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }

            SetPanelLook(pinned);
            PaintBuyButton();
            PaintGold();

            // 높이는 먼저 내용에 맞춘다. DockLeft가 최소·최대로 고정하면서 Unconstrained로 바꿔 두므로, 내용이 바뀔 때마다 되돌려야
            // 이전 높이에 새 내용이 욱여넣어지지(제목 칸이 찌그러져 본문과 겹침) 않는다.
            if (_tooltipFitter != null)
                _tooltipFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            // 글자 크기·여백·내용이 방금 바뀌었다. 한 번의 배치로는 TMP가 이전 폭·여백 기준의 선호 높이를 줄 수 있어(제목 세 줄이 두 줄 칸에 들어감)
            // 배치 → 글자 다시 재기 → 배치를 한 번 더 한다.
            RebuildTooltipLayout();
            RebuildTooltipLayout();

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

            var screen = (RectTransform)transform;

            Attach(new Vector2(0.5f, 0), top, new Vector2(0, TooltipGap));
            if (!Overflows(screen, Edge.Top))
                return;

            Attach(new Vector2(0, 0.5f), right, new Vector2(TooltipGap, 0));
            if (!Overflows(screen, Edge.Right))
                return;

            Attach(new Vector2(0.5f, 1), bottom, new Vector2(0, -TooltipGap));
        }

        // 왼쪽 고정 자리: 위는 골드 칩 아래(틈만큼), 아래는 안내 글자 위(틈만큼), 왼쪽은 칩에 맞춘다. 높이는 그 사이를 다 쓴다
        // — 설명 길이와 무관하게 패널 크기가 같다. 칩·안내가 없으면 화면 가장자리에서 틈만큼 안쪽.
        private void DockLeft()
        {
            var parent = (RectTransform)_tooltip.parent;
            var screen = (RectTransform)transform;
            RectTransform chip = View.Rect(Refs.GoldChip_Image);

            screen.GetWorldCorners(_corners);
            Vector3 topLeft = parent.InverseTransformPoint(_corners[1]) + new Vector3(DockGap, -DockGap, 0);
            float bottom = parent.InverseTransformPoint(_corners[0]).y + DockGap;

            if (chip != null)
            {
                chip.GetWorldCorners(_corners);
                topLeft = parent.InverseTransformPoint(_corners[0]) + new Vector3(0, -DockGap, 0);
            }

            // 구매 탭이 패널 위로 튀어나오는 만큼 패널을 내린다(골드 칩과 탭이 겹치지 않게).
            topLeft.y -= BuyTabHeight - BuyTabOverlap;

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

            if (_tooltipFitter != null && !Mathf.Approximately(clamped, preferred))
            {
                _tooltipFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
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
        private bool Overflows(RectTransform screen, Edge edge)
        {
            _tooltip.GetWorldCorners(_corners);
            float tooltipTop = _corners[1].y;
            float tooltipRight = _corners[2].x;
            screen.GetWorldCorners(_corners);

            return edge == Edge.Top ? tooltipTop > _corners[1].y : tooltipRight > _corners[2].x;
        }

        private void RebuildTooltipLayout()
        {
            _tooltipTitle?.ForceMeshUpdate(true, true);
            _tooltipBody?.ForceMeshUpdate(true, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);
        }

        public void HideNodeTooltip()
        {
            IsTooltipPinned = false;

            if (_buyFlash != null)
            {
                StopCoroutine(_buyFlash);
                _buyFlash = null;
            }

            _buyPressed = false;
            SetGoldShort(false);
            PaintGold();
            SetPanelLook(false);

            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
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
                if (_tooltipImage != null)
                {
                    _tooltipSprite = _tooltipImage.sprite;
                    _tooltipColor = _tooltipImage.color;
                }

                if (_tooltipTitle != null)
                {
                    _titleBaseSize = _tooltipTitle.fontSize;
                    _titleMarginLeft = _tooltipTitle.margin.x;
                }

                if (_tooltipBody != null)
                {
                    _bodyBaseSize = _tooltipBody.fontSize;
                    _bodyBaseColor = _tooltipBody.color;
                }
            }

            if (_tooltipImage != null)
            {
                _tooltipImage.raycastTarget = panel;
                _tooltipImage.sprite = panel ? null : _tooltipSprite;
                _tooltipImage.color = panel ? PanelBackColor() : _tooltipColor;
            }

            if (_tooltipTitle != null)
            {
                Vector4 margin = _tooltipTitle.margin;
                margin.x = panel ? _titleMarginLeft + IconSize + IconGap : _titleMarginLeft;
                _tooltipTitle.margin = margin;
                _tooltipTitle.fontSize = panel ? _titleBaseSize * PanelTitleScale : _titleBaseSize;
            }

            if (_tooltipBody != null)
            {
                _tooltipBody.fontSize = panel ? _bodyBaseSize * PanelBodyScale : _bodyBaseSize;
                _tooltipBody.color = panel ? Color.Lerp(_bodyBaseColor, Color.white, PanelBodyBrighten) : _bodyBaseColor;
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

        private void HandleBuyClicked(PointerEventData _) => BuyConfirmed?.Invoke();
        private void HandleBuyPressed(PointerEventData _)
        {
            // Gold가 모자란 노드의 탭을 누르면 누르는 동안 이유를 보인다. 최대·잠김은 탭 글자가 이미 말한다.
            if (_pinnedState == NodeState.Revealed)
                SetGoldShort(true);

            SetBuyPressed(true);
        }

        private void HandleBuyReleased(PointerEventData _)
        {
            SetBuyPressed(false);
            SetGoldShort(false);
        }

        private void HandleBuyLeft(PointerEventData _)
        {
            SetBuyPressed(false);
            SetGoldShort(false);
        }

        // 구매 버튼의 눌림 모양: 작아지고(스프링) 어두워진다. 손가락으로 직접 누를 때와, 노드를 꾹 눌러 살 수 있게 된 순간(호스트가 부른다) 둘 다.
        public void SetBuyPressed(bool pressed)
        {
            if (_buy == null || _buyPressed == pressed)
                return;

            _buyPressed = pressed;

            if (pressed)
            {
                UISoundSource.For(this)?.PlayClick();
                _buyAnimation.Press();
            }
            else
            {
                _buyAnimation.Release();
            }

            PaintBuyBack();
            PaintGold();
        }

        // 바탕 색: 번쩍이는 중이면 코루틴이 맡고, 아니면 눌림이면 어둡게, 아니면 기준 색.
        private void PaintBuyBack()
        {
            if (_buyBack == null || _buyFlash != null)
                return;

            if (_goldShort && _pinnedState == NodeState.Revealed)
            {
                _buyBack.color = ShortBuyColor;
                return;
            }

            _buyBack.color = _buyPressed ? Darkened(_buyBaseColor) : _buyBaseColor;
        }

        private static Color Darkened(Color color) =>
            new(color.r * BuyPressedDarken, color.g * BuyPressedDarken, color.b * BuyPressedDarken, color.a);

        // 샀다: 구매 버튼이 튀어오르고 바탕이 밝게 번쩍였다가 상태 색으로 돌아온다. 버튼을 눌러 샀든 노드를 꾹 눌러 샀든 같다.
        // 패널이 고정되어 있을 때만 보인다(올림 툴팁에는 버튼이 없다).
        public void PlayBuyFeedback()
        {
            if (_buy == null || !IsTooltipPinned || !_buy.gameObject.activeInHierarchy)
                return;

            _buyAnimation.Punch();

            if (_buyFlash != null)
                StopCoroutine(_buyFlash);

            _buyFlash = StartCoroutine(FlashBuy());
        }

        private System.Collections.IEnumerator FlashBuy()
        {
            float elapsed = 0;

            while (elapsed < BuyFlashSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BuyFlashSeconds);
                // 흰색 쪽으로 섞였다가(처음 70%) 기준 색으로. 기준 색은 그 사이 상태가 바뀌어도 따라간다.
                Color flash = Color.Lerp(_buyBaseColor, Color.white, 0.7f);
                flash.a = _buyBaseColor.a;
                _buyBack.color = Color.Lerp(flash, _buyBaseColor, t * t);
                yield return null;
            }

            _buyFlash = null;
            PaintBuyBack();
        }

        // 고정 패널에서만 쓰는 부품을 프리팹 툴팁 아래에 만든다(트리의 노드와 같은 방식). 그림 없이 단색으로 만들고
        // 테마 색(_theme)과 글꼴(Styled)을 받는다. 올림 툴팁에서는 모두 숨긴다.
        // - 제목 옆 노드 그림(제목 글자의 왼쪽 여백에 놓는다)
        
        // - 구매 탭: 패널 오른쪽 위 모서리에 걸친 "구매 70". 비용 표시이자 버튼이다. 누르는 즉시 산다(꾹 누름은 노드 쪽이다).
        private void CreatePanelParts()
        {
            if (_tooltip == null)
                return;

            _tooltipFitter = _tooltip.GetComponent<ContentSizeFitter>();

            if (_tooltipTitle != null)
            {
                _titleMarginLeft = _tooltipTitle.margin.x;

                RectTransform iconRect = Child((RectTransform)_tooltipTitle.transform, "Icon");
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

            // 구매 탭: 패널의 오른쪽 위 모서리에 걸쳐 올라앉는다(배치 밖). 단색 면.
            var buy = new GameObject("NodeTooltipBuy_Button", typeof(RectTransform));
            _buy = (RectTransform)buy.transform;
            _buy.SetParent(_tooltip, false);
            _buy.anchorMin = new Vector2(1, 1);
            _buy.anchorMax = new Vector2(1, 1);
            _buy.pivot = new Vector2(1, 0);
            _buy.anchoredPosition = new Vector2(0, -BuyTabOverlap);
            _buy.sizeDelta = new Vector2(BuyTabMinWidth, BuyTabHeight);
            var buyLayout = buy.AddComponent<LayoutElement>();
            buyLayout.ignoreLayout = true;

            // 패널과 같은 단색 면(테두리 없음). 툴팁 테두리 그림은 외곽선이 진해 아트 톤에 안 맞는다.
            _buyBack = buy.AddComponent<Image>();
            _buyBack.color = BuyBlockedColor;
            _buyBack.raycastTarget = true;

            RectTransform label = Child(_buy, "Label");
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.sizeDelta = Vector2.zero;

            _buyLabel = label.gameObject.AddComponent<TextMeshProUGUI>();
            _buyLabel.fontSize = BuyFontSize;
            _buyLabel.alignment = TextAlignmentOptions.Center;
            _buyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _buyLabel.raycastTarget = false;
            if (_tooltipBody != null)
            {
                _buyLabel.font = _tooltipBody.font;
                _buyLabel.color = _tooltipBody.color;
            }
            Styled.AddText(_buyLabel, UITextRole.Body, UITextSizePolicy.Prefab);

            _buyButton = buy.AddComponent<Button>();
            _buyButton.targetGraphic = _buyBack;
            _buyButton.transition = Selectable.Transition.None;
            _buyAnimation = ButtonAnimation.Of(_buyButton);
            BindEvent(_buyButton, HandleBuyClicked);
            BindEvent(_buyButton, HandleBuyPressed, ETouchEvent.PointerDown);
            BindEvent(_buyButton, HandleBuyReleased, ETouchEvent.PointerUp);
            BindEvent(_buyButton, HandleBuyLeft, ETouchEvent.PointerExit);

            _buy.gameObject.SetActive(false);
        }

        // 고정 상태에 맞춰 구매 탭을 보이고 글자("구매 70" / "최대" / "잠김")·활성·폭을 정한다. 올림 툴팁이면 숨긴다.
        private void PaintBuyButton()
        {
            if (_buy == null)
                return;

            if (!IsTooltipPinned)
            {
                _buy.gameObject.SetActive(false);
                return;
            }

            bool interactable = _pinnedState == NodeState.Purchasable;
            string text = _pinnedState switch
            {
                NodeState.Owned => Localized(UIText.Node.Maxed),
                NodeState.Hidden => Localized(UIText.Node.Locked),
                _ => _pendingCost is long cost ? Localized(UIText.Node.Buy) + " " + GoldText.Of(cost, _numberFormat) : Localized(UIText.Node.Buy),
            };

            _buyButton.interactable = interactable;
            _buyBaseColor = BuyBackColor(interactable);
            PaintBuyBack();
            _buyLabel.text = text;
            _buyLabel.alpha = interactable ? 1 : 0.55f;
            // 탭 폭은 글자에 맞춘다(최소 폭 이상).
            float width = Mathf.Max(BuyTabMinWidth, _buyLabel.GetPreferredValues(text).x + BuyTabPaddingX * 2);
            _buy.sizeDelta = new Vector2(width, BuyTabHeight);
            _buy.gameObject.SetActive(true);
        }

        // 살 수 있으면 팔레트의 강조색(알파는 기본값), 아니면 글자색을 엷게(회색).
        // 살 수 있으면 팔레트의 강조색(초록). 아니면 패널 면 색(Surface)을 조금 밝혀 — 글자색(TextMain)을 쓰면 다크 테마에서 흰 상자가 되어 흰 글자가 안 보인다.
        private Color BuyBackColor(bool ready)
        {
            Color fallback = ready ? BuyReadyColor : BuyBlockedColor;
            Color? themed = _theme.ColorFor(ready ? UIColorRole.Accent : UIColorRole.Surface);

            if (themed is not Color color)
                return fallback;

            if (!ready)
                color = Color.Lerp(color, _theme.ColorFor(UIColorRole.TextMain) ?? Color.white, 0.12f);

            color.a = fallback.a;
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
