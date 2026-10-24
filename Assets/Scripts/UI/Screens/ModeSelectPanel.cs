using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class ModeSelectPanel : UIPanel<ModeSelectPanel.Refs>, IStyleView
    {
        public enum Refs
        {
            CardTrack,
            CardTemplate,
            PrevBtn_Button,
            NextBtn_Button,
            ContinueBtn_Button,
            NewGameBtn_Button,
            BackBtn_Button,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,
            // 배경 위의 별빛·궤도 문양(UIScreenAmbient). 테마에 따라 머티리얼만 바뀐다.
            Ambient,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            // 카드(ModeCard)는 위젯이라 Presentation이 닿지 않는다 — 카드의 모양은 프리팹의 카드 틀에서 정한다.
            Title_Text,
            BackBtn_Image,
            PrevBtn_Image,
            NextBtn_Image,
            ContinueBtn_Image,
            ContinueBtn_Text,
            NewGameBtn_Image,
            NewGameBtn_Text,
            // 화면 전체를 덮는 배경(안전 영역 밖까지). 기본은 호 무늬 배경.
            Background_Image,
        }

        // 카드 하나에 보일 모드.
        public readonly struct ModeItem
        {
            public string Id { get; }
            public string Title { get; }
            public string Description { get; }
            public Color Color { get; } // 카드 머리의 색.
            public string Record { get; } // 카드 아래에 보일 한 줄(최고 기록 등). 비면 숨긴다.
            public bool CanContinue { get; } // 이어 할 진행이 있는가. 없으면 계속 버튼을 막는다.

            public ModeItem(
                string id,
                string title,
                string description,
                Color color,
                string record = null,
                bool canContinue = false)
            {
                Id = id;
                Title = title;
                Description = description;
                Color = color;
                Record = record;
                CanContinue = canContinue;
            }
        }

        private const float CardGap = 24;
        private const float UnselectedScale = 0.84f;
        private const float SlideSharpness = 14; // 카드가 제자리로 따라가는 빠르기. 클수록 빨리 붙는다.

        private readonly List<ModeItem> _modes = new();
        private readonly List<ModeCard> _cards = new();
        // 글자 크기·언어별 글꼴. 카드는 복제로 만들므로 만들 때 넣고 지울 때 비운다(틀은 넣지 않는다).
        // 창 제목·버튼 글자는 TextBindings에 있어 Theme가 맞춘다.
        private readonly StyledTexts _cardTexts = new();
        // 테마 그림·색: 카드 바탕, 넘김 버튼 아이콘, 창의 바탕색.
        private readonly ThemedImages _themed = new();
        private readonly List<Vector2> _targetPositions = new();
        private readonly List<float> _targetScales = new();

        private RectTransform _track;
        private ModeCard _template;
        private Button _prev;
        private Button _next;
        private Button _continue;
        private Button _newGame;
        private int _selected;

        public event Action<string> ContinueClicked;
        public event Action<string> NewGameClicked;
        public event Action BackClicked;

        // 고른 모드의 ID. 모드가 없으면 null.
        public string SelectedId => _selected < _modes.Count ? _modes[_selected].Id : null;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _track = View.Rect(Refs.CardTrack);
            _template = View.Widget<ModeCard>(Refs.CardTemplate);
            ThemeFixedImages();
            _prev = View.Button(Refs.PrevBtn_Button);
            _next = View.Button(Refs.NextBtn_Button);
            _continue = View.Button(Refs.ContinueBtn_Button);
            _newGame = View.Button(Refs.NewGameBtn_Button);

            // 틀은 복제용이다. 화면에 보이지 않는다.
            if (_template != null)
                _template.gameObject.SetActive(false);

            BindEvent(_prev, HandlePrevClicked);
            BindEvent(_next, HandleNextClicked);
            BindEvent(_continue, HandleContinueClicked);
            BindEvent(_newGame, HandleNewGameClicked);
            BindEvent(View.Button(Refs.BackBtn_Button), HandleBackClicked);
        }

        // 모드 카드를 새로 만든다. 창을 열 때마다 불러도 된다. selectedId가 목록에 없으면 첫 모드를 고른다.
        public void Build(IReadOnlyList<ModeItem> modes, string selectedId = null)
        {
            Clear();
            _modes.AddRange(modes);

            if (_template != null && _track != null)
            {
                for (int i = 0; i < _modes.Count; i++)
                    _cards.Add(CreateCard(_modes[i], i));
            }

            _selected = Math.Max(0, IndexOf(selectedId));
            Refresh();
            Slide(1);
        }

        // 매 프레임: 카드를 제자리로 조금씩 옮긴다. 일시정지(timeScale 0)와 관계없이 움직인다.
        private void Update() => Slide(1 - Mathf.Exp(-SlideSharpness * Time.unscaledDeltaTime));

        // 막힌 버튼(interactable=false)의 클릭은 UI_EventHandler가 보내지 않는다.
        private void HandlePrevClicked(PointerEventData _) => Select(_selected - 1);
        private void HandleNextClicked(PointerEventData _) => Select(_selected + 1);
        private void HandleBackClicked(PointerEventData _) => BackClicked?.Invoke();

        private void HandleContinueClicked(PointerEventData _)
        {
            if (_modes.Count > 0)
                ContinueClicked?.Invoke(SelectedId);
        }

        private void HandleNewGameClicked(PointerEventData _)
        {
            if (_modes.Count > 0)
                NewGameClicked?.Invoke(SelectedId);
        }

        // 목록 밖의 번호는 무시한다(처음·끝에서 멈춘다).
        private void Select(int index)
        {
            if (index < 0 || index >= _modes.Count || index == _selected)
                return;

            _selected = index;
            Refresh();
        }

        // 고른 모드에 맞춰 버튼을 켜고 끄고, 카드마다 설 자리를 정한다.
        private void Refresh()
        {
            bool any = _modes.Count > 0;

            // 모드가 하나뿐이면 넘길 곳이 없으니 양쪽 화살표를 숨긴다. 모드가 늘면 다시 보인다.
            bool paging = _modes.Count > 1;
            SetShown(_prev, paging);
            SetShown(_next, paging);

            SetOn(_prev, any && _selected > 0);
            SetOn(_next, any && _selected < _modes.Count - 1);
            SetOn(_continue, any && _modes[_selected].CanContinue);
            SetOn(_newGame, any);

            _targetPositions.Clear();
            _targetScales.Clear();

            for (int i = 0; i < _cards.Count; i++)
            {
                _targetPositions.Add(new Vector2(OffsetOf(i, _cards[i].Rect.rect.width), 0));
                _targetScales.Add(i == _selected ? 1 : UnselectedScale);
            }
        }

        // 고른 카드가 가운데(0), 나머지는 양옆으로 줄지어 선다.
        // 고른 카드와 첫 이웃 사이는 두 카드의 반 폭 + 간격, 그 뒤로는 작은 카드 폭 + 간격씩 벌어진다.
        private float OffsetOf(int index, float width)
        {
            if (index == _selected)
                return 0;

            int steps = Math.Abs(index - _selected);
            float small = width * UnselectedScale;
            float offset = (width + small) * 0.5f + CardGap + (steps - 1) * (small + CardGap);
            return index > _selected ? offset : -offset;
        }

        // t = 1이면 제자리로 바로 옮긴다.
        private void Slide(float t)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                RectTransform rect = _cards[i].Rect;
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, _targetPositions[i], t);

                float scale = Mathf.Lerp(rect.localScale.x, _targetScales[i], t);
                rect.localScale = new Vector3(scale, scale, 1);
            }
        }

        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        public void ApplyStyle(in UIStyle style)
        {
            Ambient?.Apply(style.ThemeId);
            _cardTexts.SetStyle(style);
            _themed.SetStyle(style);
        }

        // 카드 밖의 고정 그림: 창 바탕색(단색)과 넘김 버튼의 아이콘(ink → ivory). 버튼 자체 그림은 ImageBindings가 바꾼다.
        private void ThemeFixedImages()
        {
            _themed.AddColor(GetComponent<Image>(), UIColorRole.Backdrop);
            _themed.AddSprite(ChildImage(Refs.PrevBtn_Button, "PrevBtn_Icon"));
            _themed.AddSprite(ChildImage(Refs.NextBtn_Button, "NextBtn_Icon"));
        }

        private Image ChildImage(Refs parent, string name)
        {
            RectTransform rect = View.Rect(parent);
            Transform child = rect != null ? rect.Find(name) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        private ModeCard CreateCard(ModeItem mode, int index)
        {
            ModeCard card = Instantiate(_template, _track);
            card.name = "Card " + mode.Id;
            card.EnsureInitialized();
            card.Show(mode);
            card.Clicked += () => Select(index);

            foreach ((TMPro.TMP_Text text, UITextRole role) in card.StyledTexts)
                _cardTexts.Add(text, role);

            _themed.AddSprites(card);

            card.gameObject.SetActive(true);
            return card;
        }

        private void Clear()
        {
            foreach (ModeCard card in _cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _cards.Clear();
            _cardTexts.Clear();
            _themed.Clear();
            ThemeFixedImages();
            _modes.Clear();
            _targetPositions.Clear();
            _targetScales.Clear();
            _selected = 0;
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _modes.Count; i++)
            {
                if (string.Equals(_modes[i].Id, id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static void SetOn(Button button, bool on)
        {
            if (button != null)
                button.interactable = on;
        }

        private static void SetShown(Button button, bool shown)
        {
            if (button != null && button.gameObject.activeSelf != shown)
                button.gameObject.SetActive(shown);
        }
    }
}
