using System;

namespace BlackHole.Unity
{
    // UI 테마. UIContext.ThemeId(Presentation 변형 조건이 비교하는 글자)로 바꿔 쓴다.
    public enum UITheme
    {
        Light,
        Dark,
    }

    // UI 언어. UIContext.LocaleId(BCP 47 글자)로 바꿔 쓴다. UI 글자 표(UIText.csv)의 열 이름도 같은 글자다.
    // 저장하지 않는 값이다. 설정의 Language 선택지(번호)와의 대응은 GameSettings가 정한다.
    public enum UILocale
    {
        Korean = 0,
        English = 1,
    }

    // UI 테마·언어 → UIPresentationFlow가 쓰는 글자.
    public static class UIContextIds
    {
        public static string ToId(this UITheme theme) => theme switch
        {
            UITheme.Light => "Light",
            UITheme.Dark => "Dark",
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
        };

        public static string ToId(this UILocale locale) => locale switch
        {
            UILocale.Korean => "ko-KR",
            UILocale.English => "en-US",
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, null),
        };
    }
}
