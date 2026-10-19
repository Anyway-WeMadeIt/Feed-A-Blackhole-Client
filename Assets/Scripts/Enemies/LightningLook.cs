using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 번개(전기 소행성 등 연쇄 번개 사망 효과)의 외형 설정 에셋. 화면(LightningBolts)만 읽는다.
    // 판정 수치(피해·옮겨 가는 거리·횟수)는 적 종류 에셋에 있다. 번개는 판정이 옮겨 간 두 자리를 그대로 잇는다.
    [CreateAssetMenu(fileName = "LightningLook", menuName = "BlackHole/Lightning Look")]
    public sealed class LightningLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Lightning Bolt 셰이더의 머티리얼.")]
        [SerializeField] private Material _material;
        [Tooltip("밝은 심의 굵기(월드 단위).")]
        [Min(0.001f)]
        [SerializeField] private float _thickness = 0.1f;
        [Tooltip("번짐의 색.")]
        [SerializeField] private Color _color = new(0.55f, 0.8f, 1f, 1f);
        [Tooltip("심의 색.")]
        [SerializeField] private Color _coreColor = new(0.95f, 0.98f, 1f, 1f);
        [Tooltip("심 바깥으로 번지는 빛의 폭(월드 단위). 이만큼 멀어지면 약 37%로 옅어진다.")]
        [Min(0.001f)]
        [SerializeField] private float _glowWidth = 0.18f;
        [Tooltip("번짐의 세기. 0이면 번지지 않는다.")]
        [Range(0, 1)]
        [SerializeField] private float _glowStrength = 1f;

        [Header("꺾임")]
        [Tooltip("가운데에서 꺾임이 벗어나는 최대 폭(월드 단위). 양 끝으로 갈수록 줄어 끝점에 붙는다.")]
        [Min(0)]
        [SerializeField] private float _amplitude = 0.18f;
        [Tooltip("월드 1칸마다 꺾이는 횟수. 길이가 달라도 꺾임 간격이 같다.")]
        [Min(0.1f)]
        [SerializeField] private float _kinksPerUnit = 3;
        [Tooltip("1초에 꺾임 모양이 바뀌는 횟수(깜빡임). 0이면 바뀌지 않는다.")]
        [Min(0)]
        [SerializeField] private float _flickerRate = 12;

        [Header("움직임: 가로는 진행(0 ~ 1)")]
        [Tooltip("번개 한 줄기가 보이는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _duration = 0.45f;
        [Tooltip("전체 세기(0 ~ 1). 끝에서 0이어야 한다.")]
        [SerializeField]
        private AnimationCurve _fadeCurve = new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(1, 0));

        [Header("성능")]
        [Tooltip("동시에 보이는 번개 줄기의 상한. 넘치면 가장 오래된 줄기를 다시 쓴다.")]
        [Min(1)]
        [SerializeField] private int _maxBolts = 32;

        public Material Material => _material;
        public float Thickness => _thickness;
        public Color Color => _color;
        public Color CoreColor => _coreColor;
        public float GlowWidth => _glowWidth;
        public float GlowStrength => _glowStrength;
        public float Amplitude => _amplitude;
        public float KinksPerUnit => _kinksPerUnit;
        public float FlickerRate => _flickerRate;
        public float Duration => _duration;
        public int MaxBolts => _maxBolts;

        // 진행(0 ~ 1)에서의 세기. 곡선이 없으면 일정하게 줄어든다.
        public float Fade(float progress) => _fadeCurve == null ? 1 - progress : Mathf.Clamp01(_fadeCurve.Evaluate(progress));
    }
}
