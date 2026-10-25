using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 화면 위쪽에 잠깐 뜨는 알림 글자 목록(서버 연결 결과, 동기화 거부; 계약 2.8, 5.9). 프리팹 없이 코드로 만든다.
    // 알림은 먼저 온 것이 위, 나중에 온 것이 아래인 세로 목록으로 한꺼번에 보인다. 글자는 호출하는 쪽이 번역해서 넘긴다.
    // - 알림마다 제 수명을 가진다: 나타남 → 머묾(Show의 holdSeconds) → 사라짐. 혼자 끝나고 입력을 가로채지 않는다. 실제 시간으로 흐른다(timeScale과 무관).
    // - 일부가 사라지면 그 아래 알림이 위로 부드럽게 올라온다. 사라지는 동안에는 자리를 지키다가 다 사라진 뒤에 올라온다.
    // - 이미 보이는 알림과 같은 글자가 다시 오면 쌓지 않고 그 알림의 수명만 새로 시작한다(사라지는 중이어도 다시 선명해진다).
    // - 한꺼번에 MaxVisible개까지 보인다. 넘치면 가장 오래된 알림부터 사라지기 시작한다.
    // 높이는 글자 줄 수에 맞춰 알림마다 정한다(글자 크기 설정이 커져 줄이 늘어도 넘치지 않는다).
    [DisallowMultipleComponent]
    internal sealed class FloatingNotice : MonoBehaviour
    {
        private const float FadeInSeconds = 0.3f;
        private const float FadeOutSeconds = 0.8f;
        // 알림 하나가 목록에서 새 자리로 움직이는 빠르기. 클수록 빨리 닿는다(지수 보간).
        private const float SlideSharpness = 14f;
        private const int MaxVisible = 4;

        // 배치(기준 해상도 1920×1080).
        private const float Width = 1500f;
        // 업그레이드 화면 위쪽의 Gold 표시·구역 이름 아래, 노드 트리 위에서 시작한다. 타이틀에서는 로고 위다.
        private const float TopMargin = 125f;
        private const float MinHeight = 50f;
        private const float VerticalPadding = 10f;
        private const float Gap = 8f;
        private const float TextSize = 30f;
        private const float TextPadding = 28f;

        private static readonly Color PanelColor = new(0.04f, 0.03f, 0.08f, 0.82f);
        private static readonly Color TextColor = new(1f, 0.9f, 0.82f, 1f);

        // 알림 하나. 쓰고 나면 풀로 돌아가 다시 쓴다.
        private sealed class Entry
        {
            public RectTransform Rect;
            public CanvasGroup Group;
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
        private RectTransform _root;
        private TMP_FontAsset _font;
        private float _scale = 1f;

        // 보이는 알림이 있는가.
        public bool IsBusy => _active.Count > 0;

        public static FloatingNotice Create(Transform canvas, int siblingIndex, TMP_FontAsset font, float scale)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            var view = new GameObject("Floating Notices", typeof(RectTransform));
            view.layer = canvas.gameObject.layer;
            var rect = (RectTransform)view.transform;
            rect.SetParent(canvas, false);
            rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, canvas.childCount - 1));
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var notice = view.AddComponent<FloatingNotice>();
            notice._root = rect;
            notice._font = font;
            notice._scale = scale > 0 ? scale : 1f;
            return notice;
        }

        // 알림을 목록 맨 아래에 더한다. holdSeconds는 다 나타난 뒤 머무는 시간이다.
        public void Show(string message, float holdSeconds)
        {
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
            entry.Rect.sizeDelta = new Vector2(Width * _scale, entry.Height);
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
            entry.Rect.anchoredPosition = new Vector2(0f, -(TopMargin * _scale + entry.Y));

        private void Release(int index)
        {
            Entry entry = _active[index];
            _active.RemoveAt(index);
            entry.Rect.gameObject.SetActive(false);
            _pool.Push(entry);
        }

        private float HeightOf(TMP_Text text, string message)
        {
            float textWidth = (Width - 2f * TextPadding) * _scale;
            float preferred = text.GetPreferredValues(message, textWidth, 0f).y;
            return Mathf.Max(MinHeight * _scale, preferred + 2f * VerticalPadding * _scale);
        }

        private Entry Take()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            var view = new GameObject("Notice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            view.layer = _root.gameObject.layer;
            var rect = (RectTransform)view.transform;
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);

            var panel = view.GetComponent<Image>();
            panel.color = PanelColor;
            panel.raycastTarget = false;

            var group = view.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = view.layer;
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TextPadding * _scale, 0f);
            textRect.offsetMax = new Vector2(-TextPadding * _scale, 0f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = TextSize * _scale;
            text.color = TextColor;

            if (_font != null)
                text.font = _font;

            view.SetActive(false);
            return new Entry { Rect = rect, Group = group, Text = text };
        }
    }
}
