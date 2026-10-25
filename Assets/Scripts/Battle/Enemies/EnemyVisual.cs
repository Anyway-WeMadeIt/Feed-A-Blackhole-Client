using UnityEngine;

namespace BlackHole.Unity
{
    // 적 하나의 화면 상태. 데이터만 갖고 아무것도 하지 않는다 — 처리는 EnemyView와 공용 처리기(EnemyShade·EnemyMoons…)가 매 프레임 모든 적을 같은 식으로 한다.
    internal sealed class EnemyVisual
    {
        // 위치·크기(출현 연출)·자전을 맡는 바깥 트랜스폼. 모든 종류가 같다: Container > Renderer(몸체 스프라이트) > [Fill].
        // 자전 회전(Container)과 Hit의 펀치 회전(Renderer.transform)이 서로 다른 트랜스폼이라 부딪히지 않는다.
        public Transform Container;
        // 몸체 스프라이트. 색·체력 음영·피격 펀치를 받는다.
        public SpriteRenderer Renderer;
        public EnemyHitAnimation Hit;
        // 공전(이동)과 별개로 제자리에서 도는 자전 속도(도/초, 부호가 방향). 출현 때 정한다. 0이면 돌지 않는다.
        public float SpinSpeed;
        // 달 성질이 붙은 적만 가진다(없으면 null). 적 스프라이트의 형제라 피격 흔들림·배율의 영향을 받지 않는다(EnemyMoons).
        public MeshRenderer Moon;
        // 달 공전의 위상(바퀴)과 속도(바퀴/초, 시계방향). 적마다 출현 때 한 번 정해진다.
        public float MoonPhase;
        public float MoonSpeed;
        // 출현 연출이 끝났을 때의 크기(적 스프라이트·달). 연출 중에는 여기에 배율을 곱해 키운다.
        public Vector3 BaseScale;
        public Vector3 MoonBaseScale;
        // 출현 연출이 흐른 시간(초). SpawnSeconds에 닿으면 연출이 끝난다.
        public float SpawnElapsed;
        // 몸체의 기본 색(색 등급). 붙잡힘 색은 여기서 섞는다.
        public Color BaseColor;
        // 붙잡힘 표시의 세기(0 = 없음, 1 = 완전히 붙잡힘). HoldFadeSeconds에 걸쳐 목표로 간다.
        public float Hold;
        // 떨림의 위상(적마다 다르게).
        public float HoldPhase;
        // 성질 속 채움(황금 등). 없으면 null. 체력 음영을 몸체와 같이 받는다.
        public SpriteRenderer Fill;
        // 마지막으로 음영 문턱값을 계산한 체력 비율(처음엔 −1 = 아직 계산 안 함).
        public float ShadeRatio = -1;
    }
}
