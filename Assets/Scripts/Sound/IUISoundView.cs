namespace BlackHole.Unity
{
    // UI 소리를 내는 프리팹 화면. GameBootstrap이 화면을 등록한 뒤 한 번 넣는다(ILocalizedView와 같은 방식).
    public interface IUISoundView
    {
        void SetSounds(UISounds sounds);
    }
}
