#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 판의 조준을 맡는 봇. 사람 대신 매 프레임 조준점(마우스가 가리키는 판 좌표)을 정한다.
    // 판을 진행하기(GameSession.Advance) 전에 Aim을 부르고, 돌려받은 점을 GameSession.SetAimPoint로 넣는다 — 실제 게임의 AimInput과 같은 자리다.
    // 봇은 판을 읽기만 하고 바꾸지 않는다.
    internal interface IAimBot
    {
        // 결과 기록에 남기는 이름.
        string Name { get; }

        // 새 판을 시작할 때 부른다. seed: 봇의 흔들림(판단 간격·조준 오차)을 정하는 시드. 같은 시드면 같은 판에서 같게 움직인다.
        void Begin(int seed);

        // 이번 프레임의 조준점. delta: 지난 프레임에서 흐른 시간(초).
        Point2? Aim(World world, float delta);
    }
}
#endif
