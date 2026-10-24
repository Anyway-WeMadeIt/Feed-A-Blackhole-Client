using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 전기 별 번개(StarLightning 사망 효과)의 외형 설정 에셋. 화면(StarLightningBolts)만 읽는다. 전기 소행성의 LightningLook과 완전히 따로다.
    // 판정 수치(피해·옮겨 가는 거리·횟수)는 적 종류 에셋에 있다.
    // 소행성 번개는 두 자리를 한 줄기로 잇는다. 별 번개는 같은 셰이더로 그리되 한 홉을 나뭇가지처럼 그린다:
    // - 줄기(Trunk): From에서 To까지 마디(TrunkKinks개)에서 크게 꺾이며 가는 으뜸 가닥.
    // - 갈래(Fork): 줄기의 앞쪽 마디에서 갈라져 좌우로 벌어졌다가 To에서 다시 모이는 가닥(Forks개). 타겟 하나를 여러 줄기가 꿰는 모양이 여기서 난다.
    // - 잔가지(Twig): 줄기나 갈래의 마디에서 앞으로 비스듬히 뻗어 허공에서 끝나는 짧은 가닥(Twigs개).
    // 마디 사이 한 칸이 셰이더 사각형 하나다(가는 꺾임은 셰이더가, 큰 꺾임은 마디가 맡는다). 마디 자리는 깜빡임(FlickerRate)마다 다시 뽑아 나무 전체가 떤다.
    // 전장 배율(카메라가 넓어진 만큼)을 굵기·번짐·꺾임·벌어짐에 곱해 화면에서 같은 두께로 보이게 한다(ScaleWithField).
    [CreateAssetMenu(fileName = "StarLightningLook", menuName = "BlackHole/Star Lightning Look")]
    public sealed class StarLightningLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Lightning Bolt 셰이더의 머티리얼(소행성 번개와 같은 것을 써도 된다).")]
        [SerializeField] private Material _material;
        [Tooltip("줄기의 밝은 심의 굵기(월드 단위). 갈래·잔가지는 이 값에 배율을 곱한다.")]
        [Min(0.001f)]
        [SerializeField] private float _thickness = 0.1f;
        [Tooltip("번짐의 색.")]
        [SerializeField] private Color _color = new(0.5f, 0.78f, 1f, 1f);
        [Tooltip("심의 색.")]
        [SerializeField] private Color _coreColor = new(0.92f, 0.97f, 1f, 1f);
        [Tooltip("심 바깥으로 번지는 빛의 폭(월드 단위). 이만큼 멀어지면 약 37%로 옅어진다.")]
        [Min(0.001f)]
        [SerializeField] private float _glowWidth = 0.12f;
        [Tooltip("번짐의 세기. 0이면 번지지 않는다.")]
        [Range(0, 1)]
        [SerializeField] private float _glowStrength = 0.8f;

        [Header("가는 꺾임: 마디 사이 한 칸 안의 잔떨림(셰이더)")]
        [Tooltip("한 칸 안에서 선이 가운데에서 벗어나는 최대 폭(월드 단위). 큰 꺾임은 마디(Wander)가 맡으니 작게.")]
        [Min(0)]
        [SerializeField] private float _amplitude = 0.08f;
        [Tooltip("월드 1칸마다 꺾이는 횟수.")]
        [Min(0.1f)]
        [SerializeField] private float _kinksPerUnit = 4;
        [Tooltip("1초에 모양이 바뀌는 횟수(깜빡임). 마디 자리와 잔떨림을 함께 다시 뽑는다. 0이면 바뀌지 않는다.")]
        [Min(0)]
        [SerializeField] private float _flickerRate = 10;

        [Header("줄기: From에서 To까지의 으뜸 가닥")]
        [Tooltip("줄기 중간의 마디 수. 마디마다 크게 꺾인다. 칸 수 = 마디 + 1.")]
        [Range(1, 8)]
        [SerializeField] private int _trunkKinks = 3;
        [Tooltip("마디가 두 점을 잇는 직선에서 벗어나는 최대 폭(월드 단위, 전장 배율을 곱한다). 홉 길이의 30%를 넘지 않는다.")]
        [Min(0)]
        [SerializeField] private float _wander = 0.35f;

        [Header("갈래: 줄기에서 갈라져 To에서 다시 모이는 가닥")]
        [Tooltip("갈래의 수. 둘이면 좌우로 하나씩 벌어진다. 0이면 줄기만 그린다.")]
        [Range(0, 6)]
        [SerializeField] private int _forks = 2;
        [Tooltip("갈래 중간의 마디 수. 칸 수 = 마디 + 1.")]
        [Range(1, 6)]
        [SerializeField] private int _forkKinks = 1;
        [Tooltip("갈래가 줄기에서 가장 멀리 벌어지는 폭(월드 단위, 전장 배율을 곱한다). 갈래 길이의 45%를 넘지 않는다.")]
        [Min(0)]
        [SerializeField] private float _forkSpread = 0.6f;
        [Tooltip("갈래 굵기 = 줄기 굵기 × 이 값.")]
        [Range(0.1f, 1)]
        [SerializeField] private float _forkThickness = 0.65f;

        [Header("잔가지: 마디에서 비스듬히 뻗어 허공에서 끝나는 가닥")]
        [Tooltip("잔가지의 수. 줄기와 갈래의 마디 가운데서 무작위로 뿌리를 잡는다.")]
        [Range(0, 8)]
        [SerializeField] private int _twigs = 3;
        [Tooltip("잔가지 길이 = 홉 길이 × 이 값(절반 ~ 1배로 흔든다).")]
        [Range(0, 1)]
        [SerializeField] private float _twigLength = 0.3f;
        [Tooltip("잔가지가 뿌리 가닥의 진행 방향에서 꺾이는 각(도). 이 범위 안에서 뽑고 좌우는 무작위.")]
        [SerializeField] private Vector2 _twigTurnDegrees = new(25, 65);
        [Tooltip("잔가지 굵기 = 줄기 굵기 × 이 값.")]
        [Range(0.1f, 1)]
        [SerializeField] private float _twigThickness = 0.45f;
        [Tooltip("잔가지가 보이는 시간 = Duration × 이 값. 줄기보다 먼저 사라진다.")]
        [Range(0.1f, 1)]
        [SerializeField] private float _twigLife = 0.6f;

        [Header("움직임: 가로는 진행(0 ~ 1)")]
        [Tooltip("나무 안에서 From 쪽 칸부터 하나씩 늦게 나타나는 간격(초). 0이면 한꺼번에 나타난다(별 번개는 적중이 동시라 기본 0).")]
        [Min(0)]
        [SerializeField] private float _growSeconds = 0;
        [Tooltip("줄기 한 칸이 보이는 시간(초). 소행성(0.45)보다 조금 길게.")]
        [Min(0.01f)]
        [SerializeField] private float _duration = 0.6f;
        [Tooltip("전체 세기(0 ~ 1). 끝에서 0이어야 한다.")]
        [SerializeField]
        private AnimationCurve _fadeCurve = new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(1, 0));
        [Tooltip("굵기·번짐·꺾임·벌어짐에 전장 배율(Hq.FieldScale)을 곱한다. 카메라가 넓어져도 화면에서 같은 두께로 보인다.")]
        [SerializeField] private bool _scaleWithField = true;

        [Header("성능")]
        [Tooltip("동시에 보이는 사각형(칸)의 상한. 홉 하나가 BoltsPerHop개를 쓴다(기본 설정에서 11). 넘치면 가장 오래된 홉을 지운다.")]
        [Min(1)]
        [SerializeField] private int _maxBolts = 176;

        public Material Material => _material;
        public float Thickness => _thickness;
        public Color Color => _color;
        public Color CoreColor => _coreColor;
        public float GlowWidth => _glowWidth;
        public float GlowStrength => _glowStrength;
        public float Amplitude => _amplitude;
        public float KinksPerUnit => _kinksPerUnit;
        public float FlickerRate => _flickerRate;
        public int TrunkKinks => Mathf.Max(1, _trunkKinks);
        public float Wander => _wander;
        public int Forks => Mathf.Max(0, _forks);
        public int ForkKinks => Mathf.Max(1, _forkKinks);
        public float ForkSpread => _forkSpread;
        public float ForkThickness => _forkThickness;
        public int Twigs => Mathf.Max(0, _twigs);
        public float TwigLength => _twigLength;
        public Vector2 TwigTurnDegrees => _twigTurnDegrees;
        public float TwigThickness => _twigThickness;
        public float TwigLife => _twigLife;
        public float GrowSeconds => _growSeconds;
        public float Duration => _duration;
        public bool ScaleWithField => _scaleWithField;

        // 홉 하나가 쓰는 사각형(칸)의 수: 줄기 칸 + 갈래 칸 + 잔가지.
        public int BoltsPerHop => (TrunkKinks + 1) + Forks * (ForkKinks + 1) + Twigs;
        // 동시에 보이는 홉의 상한. 홉 하나는 통째로 사각형을 들고 있어 사각형 상한을 홉 단위로 나눈다. 적어도 하나.
        public int MaxHops => Mathf.Max(1, _maxBolts / BoltsPerHop);

        // 진행(0 ~ 1)에서의 세기. 곡선이 없으면 일정하게 줄어든다.
        public float Fade(float progress) => _fadeCurve == null ? 1 - progress : Mathf.Clamp01(_fadeCurve.Evaluate(progress));
    }
}
