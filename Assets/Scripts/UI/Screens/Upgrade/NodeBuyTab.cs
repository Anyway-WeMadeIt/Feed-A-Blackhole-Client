using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 업그레이드 화면의 구매 탭: 고정 패널의 오른쪽 위 모서리에 걸친 "구매 70". 비용 표시이자 버튼이다. 누르는 즉시 산다(꾹 누름은 노드 쪽이다).
    // 프리팹에 없고 툴팁 아래에 코드로 만든다(트리의 노드와 같은 방식). 그림 없이 단색 면이고, 테마 색과 글꼴(Styled)을 받는다.
    // 눌림·Gold 모자람·고정 상태는 UpgradeScreen이 갖고, 이 클래스는 받은 값으로 그린다. 버튼 이벤트도 화면이 붙인다(Button).
    internal sealed class NodeBuyTab
    {
        // 구매 탭(고정했을 때만 보인다): 패널 위쪽 오른쪽 모서리에 걸쳐 올라앉는 작은 상자 "구매 70". 비용 표시이자 구매 버튼이다.
        // 패널 위로 (높이 − 겹침)만큼 튀어나온다. 손가락 기준 높이.
        private const float Height = 56;
        private const float Overlap = 6;
        private const float PaddingX = 22;
        private const float MinWidth = 120;
        private const float FontSize = 28;
        // 패널 위로 튀어나오는 높이. 고정 패널은 이만큼 내려앉는다(골드 칩과 탭이 겹치지 않게).
        public const float Rise = Height - Overlap;
        private const float FlashSeconds = 0.35f;
        // 누르는 동안 바탕 밝기(기준 색에 곱한다).
        private const float PressedDarken = 0.72f;
        // 사려 했지만 Gold가 모자란 상태의 바탕: 어두운 붉은색.
        private static readonly Color ShortColor = new(0.66f, 0.31f, 0.31f, 0.96f);
        // 구매 탭 바탕: 살 수 있으면 강조색(초록), 아니면 패널과 같은 어두운 면. 팔레트가 없을 때의 기본값.
        private static readonly Color ReadyColor = new(0.34f, 0.59f, 0.41f, 0.96f);
        private static readonly Color BlockedColor = new(0.22f, 0.23f, 0.26f, 0.96f);

        // 번쩍임 코루틴을 돌리는 화면.
        private readonly MonoBehaviour _host;
        private readonly RectTransform _root;
        private readonly Button _button;
        private readonly ButtonAnimation _animation;
        private readonly Image _back;
        private readonly TMP_Text _label;

        // 마지막으로 들어온 테마. 바탕 색을 여기서 고른다.
        private UIAppliedTheme _theme;
        // 바탕의 기준 색(상태별). 누르는 동안은 어둡게, 샀을 때 번쩍인 뒤 기준 색으로 돌아온다.
        private Color _baseColor;
        private Coroutine _flash;
        // 마지막으로 받은 바탕 상태. 번쩍임이 끝나면 이 상태의 색으로 돌아온다.
        private bool _shortGold;
        private bool _pressed;

        // panel: 툴팁 상자(탭이 오른쪽 위 모서리에 걸친다). fontSource: 글꼴·글자색을 따를 툴팁 본문(없으면 기본값).
        public NodeBuyTab(RectTransform panel, TMP_Text fontSource, UIStyledElements styled, MonoBehaviour host)
        {
            _host = host;

            // 구매 탭: 패널의 오른쪽 위 모서리에 걸쳐 올라앉는다(배치 밖). 단색 면.
            var buy = new GameObject("NodeTooltipBuy_Button", typeof(RectTransform));
            _root = (RectTransform)buy.transform;
            _root.SetParent(panel, false);
            _root.anchorMin = new Vector2(1, 1);
            _root.anchorMax = new Vector2(1, 1);
            _root.pivot = new Vector2(1, 0);
            _root.anchoredPosition = new Vector2(0, -Overlap);
            _root.sizeDelta = new Vector2(MinWidth, Height);
            var layout = buy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            // 패널과 같은 단색 면(테두리 없음). 툴팁 테두리 그림은 외곽선이 진해 아트 톤에 안 맞는다.
            _back = buy.AddComponent<Image>();
            _back.color = BlockedColor;
            _back.raycastTarget = true;

            RectTransform label = UIParts.NewRect("Label", _root);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.sizeDelta = Vector2.zero;

            _label = label.gameObject.AddComponent<TextMeshProUGUI>();
            _label.fontSize = FontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            if (fontSource != null)
            {
                _label.font = fontSource.font;
                _label.color = fontSource.color;
            }
            styled.AddText(_label, UITextRole.Body, UITextSizePolicy.Prefab);

            _button = buy.AddComponent<Button>();
            _button.targetGraphic = _back;
            _button.transition = Selectable.Transition.None;
            _animation = ButtonAnimation.Of(_button);
        }

        // 화면이 누름·뗌·클릭 이벤트를 붙일 버튼.
        public Button Button => _button;

        public bool IsVisible => _root.gameObject.activeInHierarchy;

        public void ApplyTheme(in UIAppliedTheme theme) => _theme = theme;

        // 글자("구매 70" / "최대" / "잠김")·활성·폭과 기준 색을 정하고 보인다. 바탕 색은 PaintBack이 칠한다.
        public void Show(string text, bool ready)
        {
            _button.interactable = ready;
            _baseColor = BackColor(ready);
            _label.text = text;
            _label.alpha = ready ? 1 : 0.55f;
            // 탭 폭은 글자에 맞춘다(최소 폭 이상).
            float width = Mathf.Max(MinWidth, _label.GetPreferredValues(text).x + PaddingX * 2);
            _root.sizeDelta = new Vector2(width, Height);
            _root.gameObject.SetActive(true);
        }

        public void Hide() => _root.gameObject.SetActive(false);

        // 바탕 색: 번쩍이는 중이면 코루틴이 맡고, 아니면 Gold 모자람이면 붉게, 눌림이면 어둡게, 아니면 기준 색.
        public void PaintBack(bool shortGold, bool pressed)
        {
            _shortGold = shortGold;
            _pressed = pressed;

            if (_flash != null)
                return;

            if (shortGold)
            {
                _back.color = ShortColor;
                return;
            }

            _back.color = pressed ? Darkened(_baseColor) : _baseColor;
        }

        // 눌림 모양: 작아지고(스프링) 뗄 때 돌아온다.
        public void Press() => _animation.Press();

        public void Release() => _animation.Release();

        // 샀다: 튀어오르고 바탕이 밝게 번쩍였다가 상태 색으로 돌아온다.
        public void Flash()
        {
            _animation.Punch();

            if (_flash != null)
                _host.StopCoroutine(_flash);

            _flash = _host.StartCoroutine(FlashBack());
        }

        public void StopFlash()
        {
            if (_flash == null)
                return;

            _host.StopCoroutine(_flash);
            _flash = null;
        }

        private IEnumerator FlashBack()
        {
            float elapsed = 0;

            while (elapsed < FlashSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / FlashSeconds);
                // 흰색 쪽으로 섞였다가(처음 70%) 기준 색으로. 기준 색은 그 사이 상태가 바뀌어도 따라간다.
                Color flash = Color.Lerp(_baseColor, Color.white, 0.7f);
                flash.a = _baseColor.a;
                _back.color = Color.Lerp(flash, _baseColor, t * t);
                yield return null;
            }

            _flash = null;
            PaintBack(_shortGold, _pressed);
        }

        // 살 수 있으면 팔레트의 강조색(초록). 아니면 패널 면 색(Surface)을 조금 밝혀 — 글자색(TextMain)을 쓰면 다크 테마에서 흰 상자가 되어 흰 글자가 안 보인다.
        private Color BackColor(bool ready)
        {
            Color fallback = ready ? ReadyColor : BlockedColor;
            Color? themed = _theme.ColorFor(ready ? UIColorRole.Accent : UIColorRole.Surface);

            if (themed is not Color color)
                return fallback;

            if (!ready)
                color = Color.Lerp(color, _theme.ColorFor(UIColorRole.TextMain) ?? Color.white, 0.12f);

            color.a = fallback.a;
            return color;
        }

        private static Color Darkened(Color color) =>
            new(color.r * PressedDarken, color.g * PressedDarken, color.b * PressedDarken, color.a);
    }
}
