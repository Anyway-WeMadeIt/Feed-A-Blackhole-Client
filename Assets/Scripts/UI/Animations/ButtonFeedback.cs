using System;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 버튼 하나의 입력 배선을 한 번에: 클릭 → onClick, 올림·벗어남·누름·뗌 → 크기 연출(ButtonAnimation)과 소리(올림·누름).
    // 화면은 OnInitialize에서 버튼마다 한 줄로 부른다. 올림·벗어남에 더 할 일(툴팁 등)이 있으면 onHover·onLeave로 넘긴다.
    // 연출은 프리팹에 없으면 기본값으로 붙는다. 소리는 캔버스의 UISoundSource에서 처음 쓸 때 찾는다. 막힌 버튼은 연출도 소리도 없다.
    internal static class ButtonFeedback
    {
        public static ButtonAnimation Bind(Button button, Action onClick = null, Action onHover = null, Action onLeave = null)
        {
            if (button == null)
                return null;

            ButtonAnimation animation = ButtonAnimation.Of(button);
            UISounds sounds = null;

            if (onClick != null)
                UIEvents.Bind(button, _ => onClick());

            UIEvents.Bind(button, _ =>
            {
                if (button.IsInteractable())
                    (sounds ??= UISoundSource.For(button))?.PlayHover();

                animation.Hover();
                onHover?.Invoke();
            }, ETouchEvent.PointerEnter);

            UIEvents.Bind(button, _ =>
            {
                animation.Leave();
                onLeave?.Invoke();
            }, ETouchEvent.PointerExit);

            UIEvents.Bind(button, _ =>
            {
                if (button.IsInteractable())
                    (sounds ??= UISoundSource.For(button))?.PlayClick();

                animation.Press();
            }, ETouchEvent.PointerDown);

            UIEvents.Bind(button, _ => animation.Release(), ETouchEvent.PointerUp);

            return animation;
        }
    }
}
