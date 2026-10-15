#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace BlackHole.Unity
{
    // 탐욕 봇(GreedyAimBot)의 "손과 눈" 수치. 기본값은 마우스로 하는 보통 사람을 흉내 낸 [임시] 값이다.
    // 거리·속도는 화면 높이에 대한 비율로 둔다 — 전장 배율(Hq.FieldScale)이 커지면 화면도 커져서, 사람에게는 같은 손놀림이다.
    internal sealed class AimBotProfile
    {
        // 화면 세로 반 폭(월드 단위, 전장 배율 1). GameScene 전투 카메라의 orthographic size와 같다.
        public const float ScreenHalfHeight = 10f;

        // 봇 이름(기록에 남는다).
        public string Name { get; set; } = "greedy";

        // 손가락처럼: 목표로 옮기는 동안은 화면에서 손을 뗀다(조준점 없음 → Breaker가 치지 않고 붙잡지도 않는다).
        // 목표에서 LandDistanceScreens(화면 높이 비율) 안에 들어오면 다시 누른다. 마우스(false)는 옮기는 동안에도 조준한다.
        public bool LiftWhileMoving { get; set; }
        public float LandDistanceScreens { get; set; } = 0.02f;

        // 다음 목표를 고르는 간격(초)과 그 흔들림(±초). 사람은 매 프레임 다시 고르지 않는다.
        public float DecisionSeconds { get; set; } = 0.3f;
        public float DecisionJitter { get; set; } = 0.1f;

        // 반응 지연(초): 목표를 고른 뒤 손이 그쪽으로 움직이기 시작할 때까지. 따라가던 적이 죽은 것을 알아채는 데도 쓴다.
        public float ReactionSeconds { get; set; } = 0.2f;

        // 커서의 최대 속도(화면 높이/초). 1.6이면 화면 위에서 아래까지 약 0.6초.
        public float MaxSpeedScreens { get; set; } = 1.6f;

        // 목표 가까이에서 느려지는 정도(1/초). 남은 거리 × 이 값이 그 순간 속도의 상한이다(목표에 부드럽게 멈춘다).
        public float ApproachGain { get; set; } = 10f;

        // 조준 오차의 표준편차(화면 높이 비율). 목표를 고를 때마다 새로 뽑는다. 0.005면 화면 높이의 0.5%.
        public float AimErrorScreens { get; set; } = 0.005f;

        // 목표를 바꾸는 문턱: 새 목표의 점수가 지금 목표보다 이 비율 이상 좋아야 옮긴다(작은 이득으로는 옮기지 않는다).
        public float SwitchMargin { get; set; } = 0.3f;

        // 공격이 터진 직후 다시 고르는 최소 간격(초). 공격이 이보다 잦아도 손은 이 간격보다 자주 꺾지 못한다.
        public float PulseRetargetSeconds { get; set; } = 0.2f;

        // 공격이 터진 직후 고른 목표로 손이 움직이기까지(초). 리듬을 알고 다음 무리를 봐 둔 손이라 반응 지연보다 짧다.
        public float PulseMoveSeconds { get; set; } = 0.05f;

        // 혜성(픽업) 하나의 값어치: 지금 가장 좋은 일반 목표 한 번 치기의 이 배수로 본다. 가까우면 먼저 줍는다.
        public float PickupWorth { get; set; } = 1.5f;

        // 점수를 매길 후보 수: 적 위치 후보 중 상위 몇 개를 무리 가운데로 한 번 다듬을지.
        public int RefineCount { get; set; } = 6;

        public static AimBotProfile Default => new AimBotProfile();

        // 휴대폰 터치를 흉내 낸 [임시] 값: 옮길 때 손을 떼고, 손가락은 마우스보다 빨리 자리를 옮기지만 반응이 조금 늦고 굵어서 오차가 크다.
        public static AimBotProfile Touch => new AimBotProfile
        {
            Name = "touch",
            LiftWhileMoving = true,
            ReactionSeconds = 0.25f,
            MaxSpeedScreens = 3f,
            AimErrorScreens = 0.015f,
        };

        // 값 검사. 잘못된 값이면 예외.
        public void Validate()
        {
            Positive(DecisionSeconds, nameof(DecisionSeconds));
            NotNegative(DecisionJitter, nameof(DecisionJitter));
            NotNegative(ReactionSeconds, nameof(ReactionSeconds));
            Positive(MaxSpeedScreens, nameof(MaxSpeedScreens));
            Positive(ApproachGain, nameof(ApproachGain));
            NotNegative(AimErrorScreens, nameof(AimErrorScreens));
            NotNegative(SwitchMargin, nameof(SwitchMargin));
            NotNegative(PickupWorth, nameof(PickupWorth));
            Positive(PulseRetargetSeconds, nameof(PulseRetargetSeconds));
            NotNegative(PulseMoveSeconds, nameof(PulseMoveSeconds));

            if (DecisionJitter >= DecisionSeconds)
                throw new ArgumentOutOfRangeException(nameof(DecisionJitter), "판단 간격보다 작아야 한다.");

            NotNegative(LandDistanceScreens, nameof(LandDistanceScreens));

            if (RefineCount < 0)
                throw new ArgumentOutOfRangeException(nameof(RefineCount), "0 이상이어야 한다.");
        }

        private static void Positive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name, $"양의 유한한 값이 필요하다. 받은 값: {value}.");
        }

        private static void NotNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, $"0 이상의 유한한 값이 필요하다. 받은 값: {value}.");
        }
    }
}
#endif
