using UnityEngine;

namespace BlackHole.Unity
{
    // 현재 아래 값은 모두 [임시].
    // 화면 전환 연출(블랙홀 아이리스)의 외형 설정 에셋. 화면 전환(ScreenTransition)만 읽는다.
    // 길이 값은 캔버스 단위다(기준 해상도 1920×1080에서 1 = 1픽셀).
    [CreateAssetMenu(fileName = "ScreenTransitionLook", menuName = "BlackHole/Screen Transition Look")]
    public sealed class ScreenTransitionLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/UI Screen Iris 셰이더의 머티리얼. 실행 중에는 복제해 쓴다.")]
        [SerializeField] private Material _material;
        [Tooltip("덮개의 색.")]
        [SerializeField] private Color _color = Color.black;
        [Tooltip("원 둘레의 빛(사건의 지평선) 색.")]
        [SerializeField] private Color _rimColor = new(1f, 0.62f, 0.3f, 1f);
        [Tooltip("원 둘레 빛의 폭(캔버스 단위). 이만큼 멀어지면 약 37%로 옅어진다.")]
        [Min(0.01f)]
        [SerializeField] private float _rimWidth = 28;
        [Tooltip("원 둘레 빛의 세기. 0이면 빛이 없다.")]
        [Range(0, 1)]
        [SerializeField] private float _rimStrength = 0.8f;

        [Header("움직임")]
        [Tooltip("다 열린 화면이 다 덮일 때까지의 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _coverSeconds = 0.35f;
        [Tooltip("다 덮인 뒤 새 화면을 열기 전에 덮인 채 두는 시간(초).")]
        [Min(0)]
        [SerializeField] private float _holdSeconds = 0.08f;
        [Tooltip("다 덮인 화면이 다 열릴 때까지의 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _revealSeconds = 0.45f;
        [Tooltip("진행(가로: 0 열림 ~ 1 덮임)에 따른 덮임 정도(세로). 덮을 때는 이 곡선을 따라가고, 열 때는 거꾸로 되짚는다.\n" +
                 "기본은 끝으로 갈수록 가파르다: 덮을 때 점점 빨라지며 빨려 들어가고, 열 때 빠르게 터져 나왔다가 느려진다.")]
        [SerializeField]
        private AnimationCurve _closeCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 0));

        public Material Material => _material;
        public Color Color => _color;
        public Color RimColor => _rimColor;
        public float RimWidth => _rimWidth;
        public float RimStrength => _rimStrength;
        public float CoverSeconds => _coverSeconds;
        public float HoldSeconds => _holdSeconds;
        public float RevealSeconds => _revealSeconds;

        // 진행(0 열림 ~ 1 덮임)에서의 덮임 정도. 곡선이 없으면 진행 그대로다.
        public float Close(float progress) => _closeCurve == null ? progress : Mathf.Clamp01(_closeCurve.Evaluate(progress));
    }
}
