using UnityEngine;

namespace BlackHole.Unity
{
    // 웹 빌드의 전체 화면 전환. 브라우저에서는 게임이 페이지를 닫을 수 없어 종료 버튼 자리에 이 버튼을 둔다.
    // 전체 화면은 사용자 입력(클릭) 안에서만 브라우저가 허용하므로 버튼 클릭에서만 부른다.
    // 전체 화면에서 Esc는 브라우저가 가져가 게임 모르게 풀리므로, 글자는 매 프레임 Screen.fullScreen을 보고 맞춘다.
    public static class WebFullscreen
    {
#if UNITY_WEBGL
        public const bool Available = true;
#else
        public const bool Available = false;
#endif

        public static bool IsOn => Screen.fullScreen;

        public static void Toggle() => Screen.fullScreen = !Screen.fullScreen;

        // 지금 상태에서 버튼이 할 일. 전체 화면이면 "창 모드", 아니면 "전체 화면".
        public static string LabelKey => IsOn ? "web.fullscreen.exit" : "web.fullscreen.enter";
    }
}
