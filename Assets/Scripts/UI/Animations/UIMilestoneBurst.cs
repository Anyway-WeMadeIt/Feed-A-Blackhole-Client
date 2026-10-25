using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 이정표 달성 때 행 주변으로 퍼지는 작은 금색 파편(캔버스 Image 몇 장). 화면 효과일 뿐 판·결산·저장을 건드리지 않는다.
    // - 행과 같은 크기·중심의 별도 RectTransform에 붙인다. 레이아웃 그룹의 자식이나 Mask 안에 두지 않는다.
    // - Awake에서 조각 Image를 한 번 만들고 다시 쓴다. 모든 조각은 Raycast Target이 꺼져 있어 터치를 가로채지 않는다.
    // - Play는 재생 중이면 무시한다. 한 이정표에 한 번만 부르는 것은 부르는 쪽(BattleScreen)이 맡는다.
    // - 실제 시간(unscaledDeltaTime)으로 흐른다. 판이 끝난 뒤에도 재생된다. 꺼지면 조각을 모두 지운다.
    // 변화는 i로 정해진다(전투 난수·전역 Random을 쓰지 않는다). 크기·시간은 Inspector 값 하나로 조절한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIMilestoneBurst : MonoBehaviour
    {
        private const int MaxPieces = 24;

        [SerializeField] private Sprite _shard;
        [SerializeField] private Sprite _spark;
        [SerializeField] private Color _tint = new Color(0.914f, 0.714f, 0.235f, 1f);
        [SerializeField, Range(1, MaxPieces)] private int _count = 12;
        [Tooltip("전체 재생 시간(초).")]
        [SerializeField, Min(0.1f)] private float _duration = 0.65f;
        [Tooltip("조각이 날아가는 거리(캔버스 단위).")]
        [SerializeField, Min(1f)] private float _travel = 30f;
        [Tooltip("조각 크기(캔버스 단위).")]
        [SerializeField, Min(1f)] private float _particleSize = 12f;

        private struct Piece
        {
            public Image Image;
            public Vector2 Origin;
            public Vector2 Direction;
            public float Distance;
            public float Rotation;
            public float Spin;
        }

        private Piece[] _pieces;
        private RectTransform _root;
        private float _elapsed;

        public bool IsPlaying { get; private set; }

        public float Duration => _duration;

        private void Awake()
        {
            _root = (RectTransform)transform;
            _pieces = new Piece[Mathf.Clamp(_count, 1, MaxPieces)];

            for (int i = 0; i < _pieces.Length; i++)
            {
                var piece = new GameObject("Burst " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                piece.layer = gameObject.layer;

                var rect = (RectTransform)piece.transform;
                rect.SetParent(_root, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var image = piece.GetComponent<Image>();
                image.raycastTarget = false;
                image.maskable = false;
                image.preserveAspect = true;
                image.enabled = false;
                _pieces[i].Image = image;
            }
        }

        // 한 번 재생한다. 재생 중이거나 꺼져 있으면 무시한다.
        public void Play()
        {
            if (!isActiveAndEnabled || IsPlaying || _pieces == null)
                return;

            if (_shard == null && _spark == null)
            {
                Debug.LogWarning("[이정표] 파편 스프라이트(shard·spark)를 연결해야 한다.", this);
                return;
            }

            _elapsed = 0f;
            IsPlaying = true;
            float width = _root.rect.width;

            for (int i = 0; i < _pieces.Length; i++)
            {
                ref Piece p = ref _pieces[i];
                float spread = (i + 0.5f) / _pieces.Length;
                float angle = (35f + (i * 47 % 110)) * Mathf.Deg2Rad;
                p.Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * (i % 2 == 0 ? 1f : -1f));
                p.Origin = new Vector2((spread - 0.5f) * width * 0.9f, 0f);
                p.Distance = _travel * (0.7f + (i * 13 % 7) * 0.1f);
                p.Rotation = i * 37f;
                p.Spin = i % 2 == 0 ? 70f : -70f;

                Sprite selected = i % 3 == 0 ? _spark : _shard;
                p.Image.sprite = selected != null ? selected : (_shard != null ? _shard : _spark);
                p.Image.rectTransform.sizeDelta = Vector2.one * (_particleSize * (0.7f + (i % 4) * 0.15f));
                p.Image.enabled = true;
            }

            Draw(0f);
        }

        private void Update()
        {
            if (!IsPlaying)
                return;

            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(_duration, 0.1f));
            Draw(t);

            if (t >= 1f)
                StopAndClear();
        }

        private void Draw(float t)
        {
            float distanceT = UIMotion.EaseOut(t);
            float alpha = 1f - Mathf.SmoothStep(0.2f, 1f, t);

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece p = _pieces[i];
                RectTransform rect = p.Image.rectTransform;
                rect.anchoredPosition = p.Origin + p.Direction * (p.Distance * distanceT);
                rect.localRotation = Quaternion.Euler(0f, 0f, p.Rotation + p.Spin * t);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);

                Color color = _tint;
                color.a *= alpha;
                p.Image.color = color;
            }
        }

        // 재생을 멈추고 조각을 모두 숨긴다. 화면이 닫히거나 다시 열릴 때 부른다.
        public void StopAndClear()
        {
            IsPlaying = false;
            _elapsed = 0f;

            if (_pieces == null)
                return;

            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i].Image != null)
                    _pieces[i].Image.enabled = false;
            }
        }

        private void OnDisable() => StopAndClear();
    }
}
