using UnityEngine;

namespace BlackHole.Unity
{
    // 적 하나의 화면 상태. 데이터만 갖고 아무것도 하지 않는다 — 처리는 EnemyView와 공용 처리기(EnemyShade·EnemyMoons…)가 매 프레임 모든 적을 같은 식으로 한다.
    internal sealed class EnemyVisual
    {
        // 위치·크기(출현 연출)·자전을 맡는 바깥 트랜스폼. 소행성/행성/별은 Renderer(스프라이트)를 감싸는
        // 별도 부모(EnemyView.Create 참고)라서, 자전 회전과 Hit의 펀치 회전(Renderer.transform)이 서로 다른
        // 트랜스폼을 써서 부딪히지 않는다. 그 외 종류는 Renderer.transform과 같은 객체다(구조 변화 없음).
        public Transform Container;
        public SpriteRenderer Renderer;
        public EnemyHitAnimation Hit;
        // 소행성/행성/별만 0이 아니다(출현 때 무작위로 정함). 공전(이동)과 별개로 제자리에서 도는 자전
        // 속도(도/초, 부호가 방향).
        public float SpinSpeed;
        // 달 성질이 붙은 적만 가진다(없으면 null). 적 스프라이트의 형제라 피격 흔들림·배율의 영향을 받지 않는다(EnemyMoons).
        public MeshRenderer Moon;
        // 달 공전의 위상(바퀴)과 속도(바퀴/초, 시계방향). 적마다 출현 때 한 번 정해진다.
        public float MoonPhase;
        public float MoonSpeed;
        // 은하만 가진다(없으면 null). 적 스프라이트(중심)의 자식이라 피격 흔들림과 크기를 함께 받고, 자기 자전만 따로 한다.
        public Transform Arms;
        // 나선팔 자전 속도(도/초, 음수 = 시계방향). 출현 때 한 번 정해진다.
        public float ArmSpin;
        // 출현 연출이 끝났을 때의 크기(적 스프라이트·달). 연출 중에는 여기에 배율을 곱해 키운다.
        public Vector3 BaseScale;
        public Vector3 MoonBaseScale;
        // 출현 연출이 흐른 시간(초). SpawnSeconds에 닿으면 연출이 끝난다.
        public float SpawnElapsed;
        // 색을 바꾸는 렌더러(은하는 실루엣 팔, 그 외는 Renderer)와 그 기본 색(색 등급).
        public SpriteRenderer Body;
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
