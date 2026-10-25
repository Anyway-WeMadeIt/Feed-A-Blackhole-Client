using System;
using System.Globalization;
using BlackHole.Core;
using BlackHole.Systems.Numbers;
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

        public event Action StartBattleClicked;
        // 고정한 툴팁의 구매 버튼을 눌렀다.
        public event Action BuyConfirmed;

        public RectTransform PageRoot => (RectTransform)transform;

        private TMP_Text _gold;
        private TMP_Text _hq;
        // 노드 툴팁(올림 툴팁·고정 패널)의 자리와 모양. 프리팹에 툴팁이 없으면 null.
        private NodeTooltipPanel _panel;
        // 구매 탭. 프리팹에 없고 코드가 툴팁 아래에 만든다(트리의 노드와 같은 방식). 누르는 즉시 산다. 툴팁이 없으면 null.
        private NodeBuyTab _buyTab;
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
        private const float ShortBumpScale = 1.05f;
        private const float ShortBumpSeconds = 0.1f;
        private const float ShortRestoreSeconds = 0.15f;
        private bool _goldShort;
        // 숫자 색의 섞임(0 = 평소, 1 = 붉음). 들어갈 땐 바로 1, 나올 땐 0.15초에 걸쳐 0으로.
        private float _goldShortMix;
        private Coroutine _goldShortRoutine;
        // 구매 탭을 누르고 있는가(손가락으로 누르는 중, 또는 노드를 꾹 눌러 살 수 있게 된 순간). 골드 칩이 산 뒤의 값을 미리 보인다.
        private bool _buyPressed;
        // 툴팁을 터치로 고정했는가. 고정 중에는 올림으로 바뀌지 않고 레이캐스트를 받아 뒤의 노드를 가린다.
        public bool IsTooltipPinned { get; private set; }
        private NodeState _pinnedState;

        // 언어가 바뀌면 다시 쓸 마지막 값. 아직 보이지 않았으면 null.
        private Localizer _localizer;
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
            RectTransform tooltip = View.Rect(Refs.NodeTooltip_Image);
            TMP_Text tooltipBody = View.Text(Refs.NodeTooltipBody_Text);
            TMP_Text hint = View.Text(Refs.Hint_Text);

            // 고정 패널에서만 쓰는 부품(제목 옆 노드 그림, 구매 탭)은 프리팹 툴팁 아래에 코드로 만든다. 올림 툴팁에서는 모두 숨긴다.
            if (tooltip != null)
            {
                _panel = new NodeTooltipPanel(tooltip, View.Image(Refs.NodeTooltip_Image), View.Text(Refs.NodeTooltipTitle_Text), tooltipBody, hint,
                    (RectTransform)transform, View.Rect(Refs.GoldChip_Image));
                _buyTab = new NodeBuyTab(tooltip, tooltipBody, Styled, this);
                BindEvent(_buyTab.Button, HandleBuyClicked);
                BindEvent(_buyTab.Button, HandleBuyPressed, ETouchEvent.PointerDown);
                BindEvent(_buyTab.Button, HandleBuyReleased, ETouchEvent.PointerUp);
                BindEvent(_buyTab.Button, HandleBuyLeft, ETouchEvent.PointerExit);
                _buyTab.Hide();
            }

            // 안내 글자와 화면 바탕색(배경 그림 뒤)은 Styled가 테마에 맞춘다. TextBindings에 등록된 글자는 Theme가 맞춘다.
            Styled.AddText(hint, UITextRole.Caption, UITextSizePolicy.Prefab);
            Styled.AddImageColor(GetComponent<Image>(), UIColorRole.Backdrop);
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
            string number = NumberText.Of(amount, _numberFormat);
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
                float scale = UIMotion.Pop(t, ShortBumpScale - 1);
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
                t = UIMotion.EaseOut(t);
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

        // Gold 표기 설정. 보이는 Gold와 구매 탭을 새 표기로 다시 쓴다.
        public void SetNumberFormat(NumberFormat format)
        {
            _numberFormat = format;

            if (_shownGold is long gold)
                ShowGold(gold);

            PaintBuyButton();
        }

        // 마지막으로 보인 Gold·블랙홀 글자를 지금 언어로 다시 쓴다. 노드 툴팁은 닫는다(다시 올리면 새 언어로 뜬다).
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
            Ambient?.Apply(theme.ThemeId);
            _buyTab?.ApplyTheme(theme);
            PaintBuyButton();
            PaintGold();
            _panel?.ApplyTheme(theme);
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
            _panel?.SetIcon(icon);
            Place(node, title, body, pinned: true);
        }

        // 글자와 모양(올림 툴팁 ↔ 고정 패널)을 바꾸고, 구매 탭·Gold를 지금 상태로 칠한 뒤 자리에 놓는다.
        private void Place(RectTransform node, string title, string body, bool pinned)
        {
            if (_panel == null || node == null)
                return;

            IsTooltipPinned = pinned;
            _panel.SetContent(title, body, pinned);
            PaintBuyButton();
            PaintGold();
            _panel.Place(node, pinned);
        }

        public void HideNodeTooltip()
        {
            IsTooltipPinned = false;
            _buyTab?.StopFlash();
            _buyPressed = false;
            SetGoldShort(false);
            PaintGold();
            _panel?.Hide();
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
            if (_buyTab == null || _buyPressed == pressed)
                return;

            _buyPressed = pressed;

            if (pressed)
            {
                UISoundSource.For(this)?.PlayClick();
                _buyTab.Press();
            }
            else
            {
                _buyTab.Release();
            }

            PaintBuyBack();
            PaintGold();
        }

        // 구매 탭 바탕: Gold가 모자란 노드(Revealed)를 사려 하면 붉게, 누르는 동안 어둡게. 번쩍이는 중이면 탭이 맡는다.
        private void PaintBuyBack() => _buyTab?.PaintBack(_goldShort && _pinnedState == NodeState.Revealed, _buyPressed);

        // 샀다: 구매 버튼이 튀어오르고 바탕이 밝게 번쩍였다가 상태 색으로 돌아온다. 버튼을 눌러 샀든 노드를 꾹 눌러 샀든 같다.
        // 패널이 고정되어 있을 때만 보인다(올림 툴팁에는 버튼이 없다).
        public void PlayBuyFeedback()
        {
            if (_buyTab == null || !IsTooltipPinned || !_buyTab.IsVisible)
                return;

            _buyTab.Flash();
        }

        // 고정 상태에 맞춰 구매 탭을 보이고 글자("구매 70" / "최대" / "잠김")·활성·폭을 정한다. 올림 툴팁이면 숨긴다.
        private void PaintBuyButton()
        {
            if (_buyTab == null)
                return;

            if (!IsTooltipPinned)
            {
                _buyTab.Hide();
                return;
            }

            bool interactable = _pinnedState == NodeState.Purchasable;
            string text = _pinnedState switch
            {
                NodeState.Owned => Localized(UIText.Node.Maxed),
                NodeState.Hidden => Localized(UIText.Node.Locked),
                _ => _pendingCost is long cost ? Localized(UIText.Node.Buy) + " " + NumberText.Of(cost, _numberFormat) : Localized(UIText.Node.Buy),
            };

            _buyTab.Show(text, interactable);
            PaintBuyBack();
        }
    }
}
