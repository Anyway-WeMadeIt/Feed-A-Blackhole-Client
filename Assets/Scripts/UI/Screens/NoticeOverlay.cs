using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 화면 위쪽에 잠깐 뜨는 알림 글자 목록(서버 연결 결과, 동기화 거부; 계약 2.8, 5.9). Overlay 층의 프리팹(NoticeOverlay.prefab)으로, ScreenFlow가 ShowOverlay로 연다.
    // 알림은 먼저 온 것이 위, 나중에 온 것이 아래인 세로 목록으로 한꺼번에 보인다. 글자는 호출하는 쪽이 번역해서 넘긴다.
    // - 알림마다 제 수명을 가진다: 나타남 → 머묾(Show의 holdSeconds) → 사라짐. 혼자 끝나고 입력을 가로채지 않는다. 실제 시간으로 흐른다(timeScale과 무관).
    // - 일부가 사라지면 그 아래 알림이 위로 부드럽게 올라온다. 사라지는 동안에는 자리를 지키다가 다 사라진 뒤에 올라온다.
    // - 이미 보이는 알림과 같은 글자가 다시 오면 쌓지 않고 그 알림의 수명만 새로 시작한다(사라지는 중이어도 다시 선명해진다).
    // - 한꺼번에 MaxVisible개까지 보인다. 넘치면 가장 오래된 알림부터 사라지기 시작한다.
    // 높이는 글자 줄 수에 맞춰 알림마다 정한다(글자 크기 설정이 커져 줄이 늘어도 넘치지 않는다).
    // 배치(너비·위 여백·최소 높이·글자 여백·글자 크기)는 프리팹의 본보기(Notice)에서 읽고 글자 배율(TextScale)만큼 늘린다. 글꼴은 테마의 Body.
    // 판·글자 색은 테마와 무관하게 프리팹 값 그대로다(어두운 판 위 밝은 글자).
    public sealed class NoticeOverlay : UIOverlay<NoticeOverlay.Refs>
    {
        public enum Refs
        {
            // 알림이 쌓이는 자리(화면 전체).
            NoticeList,
            // 알림 하나의 본보기(꺼 둔다). 복제해 쓴다. 자리·크기가 배치의 기준이다.
            Notice,
            // 본보기의 글자. 좌우·상하 여백과 크기가 기준이다.
            Notice_Text,
        }

        private const float FadeInSeconds = 0.3f;
        private const float FadeOutSeconds = 0.8f;
        // 알림 하나가 목록에서 새 자리로 움직이는 빠르기. 클수록 빨리 닿는다(지수 보간).
        private const float SlideSharpness = 14f;
        private const int MaxVisible = 4;
        private const float Gap = 8f;

        // 알림 하나. 쓰고 나면 풀로 돌아가 다시 쓴다.
        private sealed class Entry
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public RectTransform TextRect;
            public TMP_Text Text;
            public string Message;
            public float Hold;
            public float Elapsed;
            public float Height;
            public float Y;        // 목록 맨 위에서 이 알림 윗변까지의 거리(아래로 +). 지금 값.
            public float TargetY;  // 같은 값의 목표. 앞의 알림이 사라지면 줄어든다.

            public float FadeOutStart => FadeInSeconds + Hold;
            public bool IsFadingOut => Elapsed >= FadeOutStart;
        }

        private readonly List<Entry> _active = new();
        private readonly Stack<Entry> _pool = new();
        private RectTransform _list;
        private RectTransform _template;

        // 프리팹 본보기의 배치(배율 1 기준).
        private float _width;
        private float _topMargin;
        private float _minHeight;
        private Vector2 _textPadding;   // x: 좌우, y: 상하
        private float _textSize;
        private TMP_FontAsset _baseFont;

        // 지금 테마. OnPresented가 넘긴다.
        private TMP_FontAsset _font;
        private float _scale = 1f;

        // 보이는 알림이 있는가.
        public bool IsBusy => _active.Count > 0;

        protected override void OnInitialize()
        {
            _list = View.Rect(Refs.NoticeList);
            _template = View.Rect(Refs.Notice);
            TMP_Text templateText = View.Text(Refs.Notice_Text);

            if (_template != null)
            {
                _width = _template.sizeDelta.x;
                _minHeight = _template.sizeDelta.y;
                _topMargin = -_template.anchoredPosition.y;
                _template.gameObject.SetActive(false);
            }

            if (templateText != null)
            {
                _textPadding = templateText.rectTransform.offsetMin;
                _textSize = templateText.fontSize;
                _baseFont = templateText.font;
                _font = _baseFont;
            }
        }

        // 글꼴·글자 배율. 보이는 알림은 글자와 높이를 다시 맞춘다.
        protected override void OnPresented(in UIAppliedTheme theme)
        {
            TMP_FontAsset font = theme.FontFor(UITextRole.Body);
            _font = font != null ? font : _baseFont;
            _scale = theme.TextScale;

            foreach (Entry entry in _active)
            {
                ApplyTheme(entry);
                entry.Height = HeightOf(entry.Text, entry.Message);
                entry.Rect.sizeDelta = new Vector2(_width * _scale, entry.Height);
            }

            foreach (Entry entry in _pool)
                ApplyTheme(entry);

            if (_active.Count == 0)
                return;

            Relayout();

            foreach (Entry entry in _active)
            {
                entry.Y = entry.TargetY;
                Place(entry);
            }
        }

        // 알림을 목록 맨 아래에 더한다. holdSeconds는 다 나타난 뒤 머무는 시간이다.
        public void Show(string message, float holdSeconds)
        {
            if (_template == null)
                return;

            message ??= string.Empty;
            holdSeconds = Mathf.Max(0f, holdSeconds);

            Entry same = _active.Find(entry => entry.Message == message);

            if (same != null)
            {
                // 수명을 새로 시작한다. 사라지는 중이었다면 다시 선명해진다.
                same.Hold = holdSeconds;
                same.Elapsed = same.Elapsed < FadeInSeconds ? same.Elapsed : FadeInSeconds;
                return;
            }

            Entry entry = Take();
            entry.Message = message;
            entry.Hold = holdSeconds;
            entry.Elapsed = 0f;
            entry.Text.text = message;
            entry.Height = HeightOf(entry.Text, message);
            entry.Rect.sizeDelta = new Vector2(_width * _scale, entry.Height);
            entry.Group.alpha = 0f;

            // 새 알림은 제 자리에서 나타난다(미끄러져 오지 않는다).
            entry.Y = entry.TargetY = EndOfList();
            Place(entry);
            entry.Rect.gameObject.SetActive(true);
            _active.Add(entry);

            // 넘치면 가장 오래된 알림부터 사라지게 한다.
            int visible = 0;
            foreach (Entry other in _active)
            {
                if (!other.IsFadingOut)
                    visible++;
            }

            for (int i = 0; visible > MaxVisible && i < _active.Count; i++)
            {
                if (_active[i].IsFadingOut)
                    continue;

                _active[i].Elapsed = _active[i].FadeOutStart;
                visible--;
            }
        }

        // 보이는 알림을 모두 치운다.
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Release(i);
        }

        private void Update()
        {
            if (_active.Count == 0)
                return;

            float delta = Time.unscaledDeltaTime;
            bool removed = false;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Entry entry = _active[i];
                entry.Elapsed += delta;

                if (entry.Elapsed >= entry.FadeOutStart + FadeOutSeconds)
                {
                    Release(i);
                    removed = true;
                    continue;
                }

                entry.Group.alpha = entry.Elapsed < FadeInSeconds
                    ? entry.Elapsed / FadeInSeconds
                    : entry.IsFadingOut ? 1f - (entry.Elapsed - entry.FadeOutStart) / FadeOutSeconds : 1f;
            }

            if (removed)
                Relayout();

            // 목표 자리로 부드럽게 움직인다.
            float follow = UIMotion.Follow(SlideSharpness, delta);

            foreach (Entry entry in _active)
            {
                if (entry.Y == entry.TargetY)
                    continue;

                entry.Y = Mathf.Abs(entry.TargetY - entry.Y) < 0.5f
                    ? entry.TargetY
                    : Mathf.Lerp(entry.Y, entry.TargetY, follow);

                Place(entry);
            }
        }

        // 남은 알림을 위에서부터 차례로 쌓은 자리를 목표로 정한다.
        private void Relayout()
        {
            float y = 0f;

            foreach (Entry entry in _active)
            {
                entry.TargetY = y;
                y += entry.Height + Gap * _scale;
            }
        }

        private float EndOfList()
        {
            float y = 0f;

            foreach (Entry entry in _active)
                y = entry.TargetY + entry.Height + Gap * _scale;

            return y;
        }

        private void Place(Entry entry) =>
            entry.Rect.anchoredPosition = new Vector2(0f, -(_topMargin * _scale + entry.Y));

        private void Release(int index)
        {
            Entry entry = _active[index];
            _active.RemoveAt(index);
            entry.Rect.gameObject.SetActive(false);
            _pool.Push(entry);
        }

        private float HeightOf(TMP_Text text, string message)
        {
            float textWidth = (_width - 2f * _textPadding.x) * _scale;
            float preferred = text.GetPreferredValues(message, textWidth, 0f).y;
            return Mathf.Max(_minHeight * _scale, preferred + 2f * _textPadding.y * _scale);
        }

        // 글꼴·글자 크기·글자 여백을 지금 테마에 맞춘다.
        private void ApplyTheme(Entry entry)
        {
            entry.Text.font = _font;
            entry.Text.fontSize = _textSize * _scale;
            entry.TextRect.offsetMin = _textPadding * _scale;
            entry.TextRect.offsetMax = -_textPadding * _scale;
        }

        private Entry Take()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            GameObject view = Instantiate(_template.gameObject, _list, false);
            view.name = "Notice";
            var rect = (RectTransform)view.transform;

            CanvasGroup group = view.GetComponent<CanvasGroup>() ?? view.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            TMP_Text text = view.GetComponentInChildren<TMP_Text>(true);
            var entry = new Entry { Rect = rect, Group = group, Text = text, TextRect = text.rectTransform };
            ApplyTheme(entry);

            view.SetActive(false);
            return entry;
        }
    }
}
