namespace BlackHole.Unity
{
    // 판의 콘텐츠 표시. 개발 HUD·패널과 느낌 메모가 보여 준다.
    // - 밸런스 프로필 이름: 원본 콘텐츠면 "".
    // - 테스트 표시(+test): 시나리오로 진행 상태를 만든 실행이거나, 이 판에서 테스트 도구로 판을 조작했다.
    // 프로필과 테스트 도구는 에디터·개발 빌드에만 있어서 릴리스 빌드에서는 늘 ""다.
    internal sealed class ContentTag
    {
        public const string TestSuffix = "+test";

        // 이번 실행 전체가 테스트다(시나리오). 앱을 다시 켤 때까지 유지한다.
        private bool _testSession;
        // 이번 판에서 테스트 도구를 썼다. 판을 시작할 때 지운다.
        private bool _testBattle;

        public string ProfileName { get; private set; } = "";

        public bool IsTest => _testSession || _testBattle;

        public string ContentVersion => IsTest ? ProfileName + TestSuffix : ProfileName;

        public void SetProfile(string name) => ProfileName = name ?? "";

        public void MarkTestSession() => _testSession = true;

        public void MarkTestBattle() => _testBattle = true;

        public void BeginBattle() => _testBattle = false;
    }
}
