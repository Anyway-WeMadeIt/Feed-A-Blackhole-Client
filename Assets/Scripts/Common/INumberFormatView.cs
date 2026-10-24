using BlackHole.Systems.Numbers;

namespace BlackHole.Unity
{
    // Gold 숫자를 보여 주는 화면. UISettingsSync가 시작할 때 한 번, 표기 설정이 바뀔 때마다 부른다(닫힌 화면도).
    // 화면은 표기를 간직하고, 마지막으로 보인 값을 새 표기로 다시 쓴다.
    public interface INumberFormatView
    {
        void SetNumberFormat(NumberFormat format);
    }
}
