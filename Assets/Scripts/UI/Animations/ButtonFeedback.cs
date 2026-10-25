using System;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 버튼 피드백의 소리. 게임이 ButtonFeedback.Sounds에 넣는다(UISounds). 없으면 소리 없이 연출만 한다.
    public interface IButtonSounds
    {
        void PlayHover();
        void PlayClick();
    }

    // 버튼 하나의 입력 배선을 한 번에: 클릭 → onClick, 올림·벗어남·누름·뗌 → 크기 연출(ButtonAnimation)과 소리(올림·누름).
    // 화면은 OnInitialize에서 버튼마다 한 줄로 부른다. 올림·벗어남·누름·뗌에 더 할 일(툴팁·눌림 상태 등)이 있으면 on*으로 넘긴다.
    // 연출은 프리팹에 없으면 기본값으로 붙는다. 다른 코드가 크기를 움직이는 버튼(모드 선택 카드 등)은 animate: false로 소리만 받는다.
    // 막힌 버튼은 연출도 소리도 없다. 게임을 모른다 — 소리는 Sounds로 받는다.
    internal static class ButtonFeedback
    {
        // 버튼 소리의 출처. 게임이 시작할 때 한 번 넣는다(GameBootstrap). 비어 있으면 소리 없이.
        public static IButtonSounds Sounds { get; set; }

        public static ButtonAnimation Bind(
            Button button,
            Action onClick = null,
            Action onHover = null,
            Action onLeave = null,
            Action onPress = null,
            Action onRelease = null,
            bool animate = true)
        {
            if (button == null)
                return null;

            ButtonAnimation animation = animate ? ButtonAnimation.Of(button) : null;

            if (onClick != null)
                UIEvents.Bind(button, _ => onClick());

            UIEvents.Bind(button, _ =>
            {
                if (button.IsInteractable())
                    Sounds?.PlayHover();

                animation?.Hover();
                onHover?.Invoke();
            }, ETouchEvent.PointerEnter);

            UIEvents.Bind(button, _ =>
            {
                animation?.Leave();
                onLeave?.Invoke();
            }, ETouchEvent.PointerExit);

            UIEvents.Bind(button, _ =>
            {
                if (button.IsInteractable())
                    Sounds?.PlayClick();

                animation?.Press();
                onPress?.Invoke();
            }, ETouchEvent.PointerDown);

            UIEvents.Bind(button, _ =>
            {
                animation?.Release();
                onRelease?.Invoke();
            }, ETouchEvent.PointerUp);

            return animation;
        }
    }
}
