using System;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 종류 하나의 외형(적 종류 에셋 EnemyKind의 한 칸). 화면(EnemyView·EnemyLooks·BattleHud)만 읽고 Core는 모른다.
    // 종류마다의 화면 결정이 모두 여기 있다 — 새 종류를 추가할 때 화면 코드를 고치지 않고 이 칸만 채운다.
    [Serializable]
    public struct EnemyLook
    {
        [Tooltip("몸체 스프라이트. 여럿이면 적 번호로 하나를 고른다(불규칙 다각형처럼 변형을 섞을 때). 비우면 아래 임시 도형으로 그린다. 모든 색 등급이 같은 윤곽을 쓴다.")]
        public Sprite[] sprites;
        [Tooltip("스프라이트가 비었을 때 코드가 그리는 임시 도형(EnemyShapes). None이면 불규칙 다각형.")]
        public EnemyShape shape;
        [Tooltip("특수 성질 표식(몸체 안의 작은 속 채움)의 모양. 비우면 몸체와 같은 스프라이트.")]
        public Sprite markerSprite;
        [Tooltip("표식 스프라이트가 비었을 때의 임시 도형. None이면 몸체와 같은 모양. 은하는 실루엣 대신 중심 원.")]
        public EnemyShape markerShape;

        [Header("자전 (공전과 별개로 제자리에서 돈다)")]
        [Tooltip("자전 속도 범위(도/초, 양수). 출현 때 이 사이에서 하나를 고른다. 둘 다 0이면 돌지 않는다.")]
        public float spinSpeedMin;
        public float spinSpeedMax;
        [Tooltip("자전 방향. Random은 적마다 반반.")]
        public SpinDirection spinDirection;
        [Tooltip("출현 때 시작 각도를 무작위로 돌려 놓는다(은하처럼 방향이 있는 모양).")]
        public bool randomStartAngle;

        [Header("연출")]
        [Tooltip("죽을 때: None은 바로 지운다. Spiral은 자전이 빨라지며 중심으로 말려 들어가 사라진다(EnemyVanishes).")]
        public DeathMotion deathMotion;
        [Tooltip("사망 파편이 블랙홀로 빨려 드는 속도 배율(EnemyHitParticles). 소행성 1, 행성 0.7, 별 0.5, 은하 0.35.")]
        public float debrisSuction;
        [Tooltip("전투 화면 아래에 이 적의 체력바를 보인다(보스). 여럿 살아 있으면 첫 마리.")]
        public bool healthBar;
    }

    // 코드가 그리는 임시 도형(EnemyShapes). 스프라이트 에셋이 생기면 쓰지 않는다.
    public enum EnemyShape
    {
        None = 0,
        // 불규칙 다각형 5종. 적 번호로 하나를 고른다(소행성).
        Polygons = 1,
        Circle = 2,
        StarBurst = 3,
        // 승인 시안의 중심과 세 팔을 한 면으로 채운 실루엣(은하 몸체).
        GalaxySilhouette = 4,
        // 은하 몸체 위의 작은 중심 원(성질 표식 모양).
        GalaxyCore = 5,
    }

    public enum SpinDirection
    {
        Random = 0,
        Clockwise = 1,
        CounterClockwise = 2,
    }

    public enum DeathMotion
    {
        None = 0,
        Spiral = 1,
    }
}
