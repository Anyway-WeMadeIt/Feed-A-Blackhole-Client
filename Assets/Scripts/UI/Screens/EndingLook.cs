using UnityEngine;

namespace BlackHole.Unity
{
    // 엔딩 연출(EndingOverlay)의 템포 설정 에셋. 흐름(단계·입력)은 EndingOverlay가, 무대(부품·색·글자 크기)는 프리팹이 가진다.
    // 시간은 실제 시간(초). 길이는 캔버스 단위(기준 해상도 1920×1080에서 1 = 1픽셀)이며 글자 배율만큼 늘어난다.
    [CreateAssetMenu(fileName = "EndingLook", menuName = "BlackHole/Ending Look")]
    public sealed class EndingLook : ScriptableObject
    {
        [Header("덮기")]
        [Tooltip("검은 화면이 다 덮일 때까지의 시간. 다 덮이면 판을 결산한다.")]
        [Min(0.01f)]
        [SerializeField] private float _coverSeconds = 1.6f;

        [Header("문구")]
        [Tooltip("다 덮인 뒤 첫 줄이 떠오르기 시작할 때까지.")]
        [Min(0)]
        [SerializeField] private float _lineDelay = 0.8f;
        [Tooltip("줄과 다음 줄이 떠오르기 시작하는 사이.")]
        [Min(0)]
        [SerializeField] private float _lineInterval = 2.6f;
        [Tooltip("한 줄이 다 보일 때까지.")]
        [Min(0.01f)]
        [SerializeField] private float _lineFadeSeconds = 1f;
        [Tooltip("떠오르는 거리.")]
        [Min(0)]
        [SerializeField] private float _lineRise = 18f;
        [Tooltip("마지막 줄이 다 보인 뒤 머무는 시간.")]
        [Min(0)]
        [SerializeField] private float _linesHoldSeconds = 3f;
        [Tooltip("문구 전체가 사라질 때까지.")]
        [Min(0.01f)]
        [SerializeField] private float _linesOutSeconds = 1.2f;

        [Header("크레딧")]
        [Tooltip("크레딧이 올라가는 빠르기(초당 거리).")]
        [Min(1)]
        [SerializeField] private float _creditsSpeed = 110f;
        [Tooltip("제목과 첫 역할 사이.")]
        [Min(0)]
        [SerializeField] private float _titleGap = 140f;
        [Tooltip("역할과 첫 이름 사이.")]
        [Min(0)]
        [SerializeField] private float _roleGap = 14f;
        [Tooltip("이름과 다음 이름 사이.")]
        [Min(0)]
        [SerializeField] private float _nameGap = 8f;
        [Tooltip("한 묶음(역할 + 이름들)과 다음 묶음 사이.")]
        [Min(0)]
        [SerializeField] private float _sectionGap = 96f;

        [Header("감사 문구")]
        [Tooltip("감사 문구가 다 보일 때까지.")]
        [Min(0.01f)]
        [SerializeField] private float _thanksFadeSeconds = 1.4f;
        [Tooltip("감사 문구 뒤 안내가 나올 때까지. 이 절반이 지나야 누름을 받는다.")]
        [Min(0.01f)]
        [SerializeField] private float _promptDelay = 1f;
        [Tooltip("안내 글자가 깜빡이는 빠르기(초당 횟수).")]
        [Min(0)]
        [SerializeField] private float _promptPulseHz = 0.6f;

        [Header("빨리 감기·건너뛰기")]
        [Tooltip("누르고 있을 때 문구·크레딧이 흐르는 빠르기 배율.")]
        [Min(1)]
        [SerializeField] private float _fastForwardRate = 5f;
        [Tooltip("1배 ↔ 빨리 감기 배율로 바뀌는 데 걸리는 시간.")]
        [Min(0.01f)]
        [SerializeField] private float _fastForwardRampSeconds = 0.2f;
        [Tooltip("건너뛸 때 보이던 문구·크레딧이 흐려지는 시간.")]
        [Min(0.01f)]
        [SerializeField] private float _skipFadeSeconds = 0.5f;

        public float CoverSeconds => _coverSeconds;
        public float LineDelay => _lineDelay;
        public float LineInterval => _lineInterval;
        public float LineFadeSeconds => _lineFadeSeconds;
        public float LineRise => _lineRise;
        public float LinesHoldSeconds => _linesHoldSeconds;
        public float LinesOutSeconds => _linesOutSeconds;
        public float CreditsSpeed => _creditsSpeed;
        public float TitleGap => _titleGap;
        public float RoleGap => _roleGap;
        public float NameGap => _nameGap;
        public float SectionGap => _sectionGap;
        public float ThanksFadeSeconds => _thanksFadeSeconds;
        public float PromptDelay => _promptDelay;
        public float PromptPulseHz => _promptPulseHz;
        public float FastForwardRate => _fastForwardRate;
        public float FastForwardRampSeconds => _fastForwardRampSeconds;
        public float SkipFadeSeconds => _skipFadeSeconds;
    }
}
