using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 레이저 별(LaserBurst 사망 효과)의 외형 설정 에셋. 화면(LaserBeams)만 읽는다. 판정 수치(피해·너비)와 시간(예고 DeathEffects.LaserTelegraphSeconds)은 규칙의 것이다.
    // 연출은 세 동작으로 나뉜다 — ① 표식: 별이 터지며 X 표식이 튀어나온다(섬광 + 가로 플레어 + 빔 방향의 짧은 세로 줄기, 튕기듯 한 번 커졌다 자리 잡음).
    // ② 조준: 표식이 자라는 동안 빔의 씨앗은 별 자리에 둥근 빛(LineDotRadius)으로 뭉쳐 있다가, 플레어가 회전하며 줄어들기 시작할 때 가는 실선으로 늘어나
    //    화면 밖으로 뻗어 경로를 조준한다(LineGrow). 플레어는 거의 일직선이 되는 순간 발사된다(예비 동작, FlareTurn 곡선).
    // ③ 관통: 실선이 판정 너비를 넘겼다가 자리 잡으며 화면을 관통하고, 표식은 파도처럼 가라앉는다. 섬광은 그리지 않는다 — 빔 위에 원이 남지 않게.
    //    사라지는 모양: 색 띠가 바깥 띠부터 꺼져 심만 남고(BeamBandFade) → 심이 가는 한 줄로 납작해져(BeamFade) 잠깐 머문 뒤 → 길이는 그대로 둔 채
    //    전체가 고르게 점점 가늘어져 안 보이게 된다. 어느 쪽이 먼저 사라지지 않는다.
    // 요소: 섬광(Flash) · 플레어(Flare, 빔 방향에서 FlareAngle만큼 꺾인 가로 줄기) · 표식 세로 줄기(Mark, 빔 방향의 짧은 줄기) · 선(Line) → 빔(Beam).
    // 플레어와 표식 줄기는 같은 FlareGrow 곡선으로 자라고 발사 순간 FlareCollapseSeconds 동안 가라앉는다. 빔 굵기는 BeamWidth 곡선 × 판정 너비(1 = 맞는 범위).
    // 아트 톤은 플랫(색종이): 번짐 대신 단단한 가장자리의 색 띠(Bands)로 그리고, 사라질 때는 옅어지지 않고 크기가 줄어든다(세기 곡선은 크기 배율이다). 조준 실선만 알파를 쓴다.
    // 선과 빔의 길이는 화면(카메라)의 가장 먼 모서리까지다 — 규칙의 양 끝(±300)을 그대로 그리면 중심에서 자라는 느낌이 나지 않는다.
    // 전장 배율(카메라가 넓어진 만큼)을 굵기·번짐·플레어 길이·섬광 크기에 곱해 화면에서 같은 크기로 보이게 한다(ScaleWithField). 빔의 판정 너비는 곱하지 않는다.
    [CreateAssetMenu(fileName = "LaserLook", menuName = "BlackHole/Laser Look")]
    public sealed class LaserLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Laser Beam 셰이더의 머티리얼.")]
        [SerializeField] private Material _material;
        [Tooltip("색 띠의 색. 적 팔레트처럼 채도를 낮춘 붉은색(레이저 별 몸통 색 계열).")]
        [SerializeField] private Color _color = new(0.78f, 0.28f, 0.3f, 1f);
        [Tooltip("심의 색. 순백이 아니라 연한 살구색 — 플랫 톤에서 흰 면은 눈을 찌른다.")]
        [SerializeField] private Color _coreColor = new(0.95f, 0.62f, 0.55f, 1f);
        [Tooltip("심 밖 색 띠의 단 수. 바깥 띠일수록 투명해 어두운 배경 위에서 한 톤씩 어두워진다(2면 두 톤).")]
        [Range(1, 4)]
        [SerializeField] private int _bands = 2;
        [Tooltip("가장 안쪽 띠의 세기(알파). 바깥 띠는 단마다 같은 폭으로 줄어든다. 0이면 심만 그린다.")]
        [Range(0, 1)]
        [SerializeField] private float _glowStrength = 0.75f;
        [Tooltip("치명타 발사일 때 빔의 번짐 폭과 섬광 크기에 곱하는 배율.")]
        [Min(1)]
        [SerializeField] private float _criticalBoost = 1.3f;
        [Tooltip("굵기·번짐·플레어 길이·섬광 크기에 전장 배율(Hq.FieldScale)을 곱한다. 판정 너비(빔 굵기)는 곱하지 않는다.")]
        [SerializeField] private bool _scaleWithField = true;

        [Header("섬광: 죽은 자리의 둥근 빛")]
        [Tooltip("예고 시작 섬광의 반지름(월드 단위).")]
        [Min(0)]
        [SerializeField] private float _flashRadius = 0.9f;
        [Tooltip("섬광 바깥 색 띠 전체의 폭(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _flashGlowWidth = 1.2f;
        [Tooltip("예고 진행(0 ~ 1)에서 시작 섬광의 크기 배율(1 = 설정 크기, 0 = 없음). 처음에 터졌다 곧 줄어들고, 발사 직전 표식이 점으로 뭉치는 동안 다시 커진다(발사와 함께 사라진다).")]
        [SerializeField]
        private AnimationCurve _flashCurve = new AnimationCurve(
            new Keyframe(0, 1, 0, -4), new Keyframe(0.3f, 0, 0, 0), new Keyframe(0.6f, 0, 0, 0), new Keyframe(1, 0.6f, 0, 0));

        [Header("플레어: 빔 방향에서 꺾인 가로 줄기")]
        [Tooltip("빔 방향에서 꺾이는 각(도)의 범위. 레이저마다 이 안에서 뽑고 좌우는 무작위(같은 레이저는 예고와 발사가 같은 각을 쓴다).")]
        [SerializeField] private Vector2 _flareAngleDegrees = new(70, 80);
        [Tooltip("다 자랐을 때 중심에서 한쪽 끝까지의 길이(월드 단위, 전장 배율을 곱한다).")]
        [Min(0)]
        [SerializeField] private float _flareReach = 5f;
        [Tooltip("X 표식의 세로 줄기(빔 방향)가 다 자랐을 때 중심에서 한쪽 끝까지의 길이(월드 단위, 전장 배율을 곱한다). 플레어와 같은 곡선으로 자란다.")]
        [Min(0)]
        [SerializeField] private float _markReach = 3f;
        [Tooltip("플레어 가운데의 심 굵기(월드 단위). 끝으로 갈수록 뾰족해진다.")]
        [Min(0.001f)]
        [SerializeField] private float _flareThickness = 0.16f;
        [Tooltip("플레어 바깥 색 띠 전체의 폭(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _flareGlowWidth = 0.6f;
        [Tooltip("예고 진행(0 ~ 1)에서 플레어·표식 길이의 비율. 튕기듯 1을 넘겼다가(표식 등장) 자리 잡고, 발사 직전 회전하며 점으로 뭉치듯 줄어든다(예비 동작). 1을 넘어도 된다.")]
        [SerializeField]
        private AnimationCurve _flareGrow = new AnimationCurve(
            new Keyframe(0, 0, 0, 12), new Keyframe(0.12f, 1.15f, 0, 0), new Keyframe(0.25f, 1, 0, 0), new Keyframe(0.55f, 1, 0, 0), new Keyframe(1, 0.12f, 0, 0));
        [Tooltip("예고 진행(0 ~ 1)에서 플레어가 빔 방향에서 꺾인 각의 비율(1 = FlareAngle, 0 = 빔과 일직선). 발사 직전 실선 쪽으로 살짝 돈다 — " +
                 "두 키의 접선이 0이면 천천히 돌기 시작해 빨라졌다가 천천히 멈춘다(ease in-out). 끝값만큼 꺾인 채 발사되고 가라앉는다(0.8이면 75°의 20%인 15°쯤 돈다).")]
        [SerializeField]
        private AnimationCurve _flareTurn = new AnimationCurve(
            new Keyframe(0, 1, 0, 0), new Keyframe(0.55f, 1, 0, 0), new Keyframe(1, 0.8f, 0, 0));
        [Tooltip("예고 진행(0 ~ 1)에서 플레어·표식 심 굵기의 비율(번짐 폭도 같이 줄인다). 회전하며 조금씩 얇아진다.")]
        [SerializeField]
        private AnimationCurve _flareThin = new AnimationCurve(
            new Keyframe(0, 1, 0, 0), new Keyframe(0.55f, 1, 0, 0), new Keyframe(1, 0.35f, 0, 0));
        [Tooltip("예고 진행(0 ~ 1)에서 플레어·표식 굵기의 배율(1 = 설정 굵기). 등장할 때 가늘게 시작해 굵어진다.")]
        [SerializeField]
        private AnimationCurve _flareFade = new AnimationCurve(
            new Keyframe(0, 0.6f, 0, 3), new Keyframe(0.2f, 1, 0, 0), new Keyframe(1, 1, 0, 0));
        [Tooltip("발사 뒤 플레어·표식이 가라앉는 시간(초). 길이가 줄고 옅어진다.")]
        [Min(0.01f)]
        [SerializeField] private float _flareCollapseSeconds = 0.12f;

        [Header("선: 예고 동안 빔 방향으로 자라는 실선")]
        [Tooltip("실선의 굵기(월드 단위, 전장 배율을 곱한다).")]
        [Min(0.001f)]
        [SerializeField] private float _lineThickness = 0.05f;
        [Tooltip("실선이 뻗기 전 별 자리에 뭉쳐 있는 둥근 빛의 반지름(월드 단위, 전장 배율을 곱한다). 실선이 자라는 만큼 이 둥근 빛이 실선 굵기로 가늘어진다.")]
        [Min(0)]
        [SerializeField] private float _lineDotRadius = 0.3f;
        [Tooltip("실선 바깥 색 띠 전체의 폭(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _lineGlowWidth = 0.15f;
        [Tooltip("예고 진행(0 ~ 1)에서 실선 길이의 비율(0 = 중심의 둥근 빛, 1 = 화면 밖). 플레어가 회전하며 줄어들기 시작하고(FlareTurn·FlareGrow의 0.55 키) " +
                 "한 박자(예고 0.6초 기준 약 0.1초 = 진행 0.17) 쉰 뒤 출발해 빠르게 뻗는다(조준).")]
        [SerializeField]
        private AnimationCurve _lineGrow = new AnimationCurve(
            new Keyframe(0.72f, 0, 0, 8), new Keyframe(0.9f, 1, 0, 0), new Keyframe(1, 1, 0, 0));
        [Tooltip("예고 진행(0 ~ 1)에서 둥근 빛·실선의 세기(0 ~ 1). 조준 중엔 옅게 머물다 발사 직전 빠르게 진해진다.")]
        [SerializeField]
        private AnimationCurve _lineFade = new AnimationCurve(
            new Keyframe(0, 0.5f, 0, 0), new Keyframe(0.72f, 0.5f, 0, 0), new Keyframe(0.9f, 0.5f, 0, 0), new Keyframe(1, 1, 5, 0));

        [Header("빔: 발사. 가로는 발사 뒤 시간의 진행(0 ~ 1, BurstSeconds)")]
        [Tooltip("발사 연출이 보이는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _burstSeconds = 0.45f;
        [Tooltip("빔 굵기 = 판정 너비 × 이 값(BeamFade를 곱하기 전). 순식간에 최대로 굵어진 뒤 그 굵기를 유지한다 — 줄어드는 건 BeamFade가 맡는다. 1 근처가 맞는 범위다.")]
        [SerializeField]
        private AnimationCurve _beamWidth = new AnimationCurve(
            new Keyframe(0, 0.05f, 0, 40), new Keyframe(0.053f, 1.4f, 0, 0), new Keyframe(1, 1.4f, 0, 0));
        [Tooltip("빔 바깥 색 띠 전체의 폭(월드 단위, 전장 배율을 곱한다). 판정 너비 밖으로 색 띠가 이만큼 더 보인다.")]
        [Min(0.001f)]
        [SerializeField] private float _beamGlowWidth = 0.8f;
        [Tooltip("발사 진행(0 ~ 1)에서 색 띠가 바깥 띠부터 차례로 꺼지는 진행(0 = 다 보임, 1 = 심만 남음). 심이 납작해지기 전에 끝나야 순서가 산다.")]
        [SerializeField]
        private AnimationCurve _beamBandFade = new AnimationCurve(
            new Keyframe(0, 0, 0, 0), new Keyframe(0.276f, 0, 0, 0), new Keyframe(0.467f, 1, 0, 0), new Keyframe(1, 1, 0, 0));
        [Tooltip("발사 진행(0 ~ 1)에서 심 굵기의 배율(1 = 설정 굵기). 최대 굵기를 잠깐(0.45초 기준 6프레임 = 0.1초) 유지한 뒤 가는 한 줄로 납작해지고(세로 수축), " +
                 "그 굵기로 잠깐 머문 뒤(약 0.1초) 선형으로 고르게 0까지 가늘어져 사라진다. 끝에서 0이어야 한다.")]
        [SerializeField]
        private AnimationCurve _beamFade = new AnimationCurve(
            new Keyframe(0, 1, 0, 0), new Keyframe(0.276f, 1, 0, 0), new Keyframe(0.507f, 0.08f, 0, 0), new Keyframe(0.729f, 0.08f, 0, -0.295f),
            new Keyframe(1, 0, -0.295f, 0));

        [Header("성능")]
        [Tooltip("동시에 보이는 발사의 상한. 넘치면 가장 오래된 발사를 다시 쓴다. 예고는 규칙이 주는 만큼 그린다.")]
        [Min(1)]
        [SerializeField] private int _maxBursts = 8;

        public Material Material => _material;
        public Color Color => _color;
        public Color CoreColor => _coreColor;
        public int Bands => Mathf.Clamp(_bands, 1, 4);
        public float GlowStrength => _glowStrength;
        public float CriticalBoost => _criticalBoost;
        public bool ScaleWithField => _scaleWithField;
        public float FlashRadius => _flashRadius;
        public float FlashGlowWidth => _flashGlowWidth;
        public Vector2 FlareAngleDegrees => _flareAngleDegrees;
        public float FlareReach => _flareReach;
        public float MarkReach => _markReach;
        public float FlareThickness => _flareThickness;
        public float FlareGlowWidth => _flareGlowWidth;
        public float FlareCollapseSeconds => _flareCollapseSeconds;
        public float LineThickness => _lineThickness;
        public float LineDotRadius => _lineDotRadius;
        public float LineGlowWidth => _lineGlowWidth;
        public float BurstSeconds => _burstSeconds;
        public float BeamGlowWidth => _beamGlowWidth;
        public int MaxBursts => _maxBursts;

        public float Flash(float progress) => Evaluate(_flashCurve, progress, 0);
        // 플레어·표식 길이의 비율. 튕김을 위해 1을 넘을 수 있다.
        public float FlareGrowth(float progress) => _flareGrow == null ? 1 : Mathf.Max(0, _flareGrow.Evaluate(Mathf.Clamp01(progress)));
        public float FlareStrength(float progress) => Evaluate(_flareFade, progress, 1);
        // 플레어가 꺾인 각의 비율(0 ~ 1). 곡선이 없으면 끝까지 꺾인 채다.
        public float FlareTurnFraction(float progress) => Evaluate(_flareTurn, progress, 1);
        // 플레어·표식 굵기의 비율(0 ~ 1). 곡선이 없으면 그대로다.
        public float FlareThinness(float progress) => Evaluate(_flareThin, progress, 1);
        public float LineGrowth(float progress) => Evaluate(_lineGrow, progress, 1);
        public float LineStrength(float progress) => Evaluate(_lineFade, progress, 1);
        public float BeamStrength(float progress) => Evaluate(_beamFade, progress, 1 - progress);
        public float BeamBandFade(float progress) => Evaluate(_beamBandFade, progress, 0);
        // 판정 너비에 곱하는 배율. 곡선이 없으면 1.
        public float BeamWidthScale(float progress) => _beamWidth == null ? 1 : Mathf.Max(0, _beamWidth.Evaluate(Mathf.Clamp01(progress)));

        private static float Evaluate(AnimationCurve curve, float progress, float fallback) =>
            curve == null ? fallback : Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(progress)));
    }
}
