using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // Breaker 링의 외형 설정 에셋. 화면(BreakerView)만 읽는다. 판정 수치(반지름·주기 등)는 스킬 설정 에셋(SkillSetup)에 있다.
    // 링의 기본 반지름은 늘 판정 반지름이다. 타격 튐으로 커지는 반지름은 화면에서만 커지고 판정에 영향을 주지 않는다.
    [CreateAssetMenu(fileName = "BreakerLook", menuName = "BlackHole/Breaker Look")]
    public sealed class BreakerLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Breaker Ring 셰이더의 머티리얼.")]
        [SerializeField] private Material _material;
        [Tooltip("선 굵기(반지름 대비). 링 굵기 = 반지름 × 이 값이라 범위가 커지면 같이 굵어진다. 원작 점선은 링 지름의 약 7%(반지름의 0.15배).")]
        [Min(0.001f)]
        [SerializeField] private float _thicknessRatio = 0.15f;
        [Tooltip("점선 개수. 반지름이 바뀌어도 그대로다. 대기·공격 링이 같다.")]
        [Min(1)]
        [SerializeField] private int _dashCount = 8;
        [Tooltip("한 칸에서 대시가 차지하는 비율(0 ~ 1). 나머지가 빈칸이다. 1이면 이어진 원이다.")]
        [Range(0, 1)]
        [SerializeField] private float _dashRatio = 0.6f;
        [SerializeField] private Color _color = new(0.92f, 0.92f, 0.92f, 0.9f);
        [Tooltip("기본 회전 속도(바퀴/초). 음수면 반대로 돈다.")]
        [SerializeField] private float _rotationSpeed = 0.05f;

        [Header("타격 튘: 클릭처럼 빠르게 커졌다 돌아온다")]
        [Tooltip("튐 한 번의 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _punchDuration = 0.14f;
        [Tooltip("튐의 모양. 가로는 진행(0 ~ 1), 세로는 세기(0 ~ 1)다. 0에서 시작해 0으로 끝나야 한다.")]
        [SerializeField]
        private AnimationCurve _punchCurve = new AnimationCurve(
            new Keyframe(0, 0), new Keyframe(0.25f, 1), new Keyframe(1, 0));
        [Tooltip("튐의 정점에서 반지름이 커지는 비율. 화면에서만 커진다.")]
        [Min(0)]
        [SerializeField] private float _punchRadiusScale = 0.1f;
        [Tooltip("튐의 정점에서 굵기가 커지는 비율.")]
        [Min(0)]
        [SerializeField] private float _punchThicknessScale = 0.6f;
        [Tooltip("헛친 Tick(맞은 적 없음)의 튀는 세기. 적을 친 Tick이 1이다.")]
        [Range(0, 1)]
        [SerializeField] private float _missStrength = 0.5f;
        [Tooltip("적을 친 Tick의 튐 정점에서 섞는 색.")]
        [SerializeField] private Color _hitFlashColor = Color.white;
        [Tooltip("치명타 Tick의 튐 정점에서 섞는 색.")]
        [SerializeField] private Color _criticalFlashColor = new(1f, 0.85f, 0.2f, 1f);

        [Header("버프 구체: 중첩 하나마다 구체 하나가 링 바깥 궤도를 시계방향으로 돈다(달·혜성이 한 궤도를 나눠 쓴다)")]
        [Tooltip("BlackHole/Breaker Orbs 셰이더의 머티리얼. 혜성의 무지개(흐름 속도·채도)는 이 머티리얼의 속성이다.")]
        [SerializeField] private Material _orbMaterial;
        [Tooltip("구체의 반지름(전장 배율 1에서의 월드 단위). 화면 크기가 고정이라 판의 전장 배율을 곱한다(원작 달은 카메라와 무관하게 약 37px). 링 크기와는 관계없다.")]
        [Min(0.001f)]
        [SerializeField] private float _orbRadius = 0.3f;
        [Tooltip("링 바깥 가장자리와 구체 사이의 간격(전장 배율 1에서의 월드 단위, 전장 배율을 곱한다). 궤도 반지름 = 링 반지름 + 링 굵기/2 + 구체 반지름 + 이 값.")]
        [Min(0)]
        [SerializeField] private float _orbitOffset = 0.1f;
        [Tooltip("공전 속도(바퀴/초, 시계방향).")]
        [SerializeField] private float _orbitSpeed = 0.15f;
        [Tooltip("달 구체의 채움 색.")]
        [SerializeField] private Color _moonFill = Color.white;
        [Tooltip("달 구체의 테두리 색.")]
        [SerializeField] private Color _moonOutline = new(0.25f, 0.27f, 0.32f, 1f);
        [Tooltip("달 구체의 테두리 두께(전장 배율 1에서의 월드 단위, 전장 배율을 곱한다).")]
        [Min(0)]
        [SerializeField] private float _moonOutlineWidth = 0.01f;

        [Header("혜성 배경 원: 혜성 중첩이 있는 동안 링을 덮는 반투명 무지개 원(적 위, 링 아래)")]
        [Tooltip("BlackHole/Breaker Comet Aura 셰이더의 머티리얼. 무지개(흐름·방향 회전 속도·채도)와 불투명도는 이 머티리얼의 속성이다.")]
        [SerializeField] private Material _cometAuraMaterial;
        [Tooltip("원이 링 바깥 가장자리보다 더 나가는 거리(월드 단위). 원 반지름 = 링 반지름 + 링 굵기/2 + 이 값.")]
        [Min(0)]
        [SerializeField] private float _cometAuraOffset = 0.15f;

        [Header("붙잡힌 적: 조준점이 있는 동안 링 안에 들어 거의 멈춘 적의 표시(EnemyView)")]
        [Tooltip("붙잡힌 적의 색을 이 색 쪽으로 섞는다. 알파는 쓰지 않는다.")]
        [SerializeField] private Color _holdTint = new(1f, 1f, 1f, 1f);
        [Tooltip("색을 섞는 비율(0 = 안 섞음, 1 = 완전히 이 색).")]
        [Range(0, 1)]
        [SerializeField] private float _holdTintStrength = 0.4f;
        [Tooltip("붙잡힌 적이 제자리에서 떠는 폭(적 반지름에 대한 비율).")]
        [Range(0, 0.5f)]
        [SerializeField] private float _holdJitter = 0.06f;
        [Tooltip("떨림의 빠르기(초당 왕복 수).")]
        [Min(0)]
        [SerializeField] private float _holdJitterHz = 18f;
        [Tooltip("붙잡힘 표시가 켜지고 꺼지는 데 걸리는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _holdFadeSeconds = 0.08f;

        [Header("대기 링: 마우스·펜이 가리키는 자리에 누르면 칠 범위를 보이는 정적 링(BreakerView)")]
        [Tooltip("대기 링의 색. 공격 중인 링(Color)보다 눌려 보이도록 차가운 회색·낮은 알파. 반지름은 공격 링과 같다.")]
        [SerializeField] private Color _hoverColor = new(0.45f, 0.47f, 0.52f, 0.35f);
        [Tooltip("대기 링의 선 굵기(반지름 대비). 공격 링(ThicknessRatio)보다 가늘게 둔다. 누르면 바깥 가장자리는 그대로 두고 이 굵기에서 공격 굵기로 안쪽으로 조여든다.")]
        [Min(0.001f)]
        [SerializeField] private float _hoverThicknessRatio = 0.07f;
        [Tooltip("대기 링의 회전 속도(바퀴/초). 공격 링(RotationSpeed)보다 느리게 둔다.")]
        [SerializeField] private float _hoverRotationSpeed = 0.015f;
        [Tooltip("대기 → 공격 링으로 조여드는(또 풀리는) 데 걸리는 시간(초).")]
        [Min(0)]
        [SerializeField] private float _gripSeconds = 0.1f;

        public Material Material => _material;
        public Color HoverColor => _hoverColor;
        public float HoverThicknessRatio => _hoverThicknessRatio;
        public float HoverRotationSpeed => _hoverRotationSpeed;
        public float GripSeconds => _gripSeconds;
        public float ThicknessRatio => _thicknessRatio;
        public int DashCount => _dashCount;
        public float DashRatio => _dashRatio;
        public Color Color => _color;
        public float RotationSpeed => _rotationSpeed;
        public float PunchDuration => _punchDuration;
        public float PunchRadiusScale => _punchRadiusScale;
        public float PunchThicknessScale => _punchThicknessScale;
        public float MissStrength => _missStrength;
        public Color HitFlashColor => _hitFlashColor;
        public Color CriticalFlashColor => _criticalFlashColor;
        public Material OrbMaterial => _orbMaterial;
        public float OrbRadius => _orbRadius;
        public float OrbitOffset => _orbitOffset;
        public float OrbitSpeed => _orbitSpeed;
        public Color MoonFill => _moonFill;
        public Color MoonOutline => _moonOutline;
        public float MoonOutlineWidth => _moonOutlineWidth;
        public Material CometAuraMaterial => _cometAuraMaterial;
        public float CometAuraOffset => _cometAuraOffset;
        public Color HoldTint => _holdTint;
        public float HoldTintStrength => _holdTintStrength;
        public float HoldJitter => _holdJitter;
        public float HoldJitterHz => _holdJitterHz;
        public float HoldFadeSeconds => _holdFadeSeconds;

        // 진행(0 ~ 1)에서의 튐 세기. 곡선이 없으면 튀지 않는다.
        public float Punch(float progress) => _punchCurve == null ? 0 : _punchCurve.Evaluate(progress);
    }
}
