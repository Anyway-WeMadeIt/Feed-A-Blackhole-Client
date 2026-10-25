using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 특수 성질에 붙는 부속물(달 성질의 달 같은 것)의 공용 처리기. 처리기 하나가 그 성질의 적 전부를 같은 식으로 다루고,
    // 적마다의 상태(렌더러·위상 등)는 EnemyVisual에 둔다. 어느 성질에 어느 처리기인지는 EnemyView가 만들 때 목록으로 등록한다 —
    // 부속물이 있는 성질을 추가하면 처리기 하나를 만들어 그 목록에 넣는다. 표식 색만 있는 성질(황금 등)은 처리기가 필요 없다.
    // 적 화면이 수명 단계마다 부른다: 출현(Create → BeginSpawn → 커지는 동안 SetScale) → 매 프레임 Follow → 붕괴(Place) → 지움(Destroy).
    internal interface IEnemyAttachment
    {
        EnemyTraitType Trait { get; }

        // 출현 때 한 번. fieldScale: 이 판의 전장 배율.
        void Create(EnemyVisual visual, Enemy enemy, float fieldScale);
        // 출현 연출은 점에서 시작한다: 제 크기를 기억해 두고 크기 0으로.
        void BeginSpawn(EnemyVisual visual);
        // 제 크기에 배율을 곱한다(출현·붕괴 연출).
        void SetScale(EnemyVisual visual, float scale);
        // 매 프레임: 적의 자리로 옮기고, 일시정지가 아니면 delta만큼 진행한다.
        void Follow(EnemyVisual visual, Point2 position, bool paused, float delta);
        // 붕괴(엔딩) 중: 적과 같은 자리·비율을 따르고, 블랙홀에 닿으면 숨긴다.
        void Place(EnemyVisual visual, Vector3 position, float scale, bool visible);
        void Destroy(EnemyVisual visual);
    }
}
