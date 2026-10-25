using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 엔딩 화면에 보일 글자. ScreenFlow가 번역표(ending.*)에서 모은다.
    internal sealed class EndingTexts
    {
        public IReadOnlyList<string> Lines = Array.Empty<string>();
        public string CreditsTitle = "";
        public IReadOnlyList<EndingCredit> Credits = Array.Empty<EndingCredit>();
        public string Thanks = "";
        public string Prompt = "";
    }

    // 크레딧 한 묶음: 역할 한 줄과 이름 여러 줄.
    internal readonly struct EndingCredit
    {
        public readonly string Role;
        public readonly IReadOnlyList<string> Names;

        public EndingCredit(string role, IReadOnlyList<string> names)
        {
            Role = role;
            Names = names;
        }
    }

    // 엔딩 화면. Overlay 층의 프리팹(EndingOverlay.prefab)으로, ScreenFlow가 ShowOverlay로 열고 Present → Play로 시작한다.
    // 순서: 검은 화면이 천천히 덮는다 → 다 덮이면 Covered(이때 판을 결산한다) → 엔딩 문구가 한 줄씩 떠오른다 → 크레딧이 아래에서 위로 흐른다 →
    //       감사 문구와 안내("눌러서 타이틀로") → 누르면 Finished(타이틀로 간다).
    // - 빨리 감기: 화면을 누르고 있는 동안(클릭·터치, Space) 문구·크레딧이 Look.FastForwardRate배로 흐른다. 떼면 원래 빠르기로 돌아온다. 건너뛰지는 않는다.
    // - 감사 문구에서 누르면 끝낸다. 감사 문구가 나온 뒤에 새로 누른 것만 받는다(크레딧을 빨리 감다가 뗀 것으로는 끝나지 않는다).
    // - 건너뛰기(Skip: Esc): 문구·크레딧 중이면 감사 문구로 바로 간다. 감사 문구에서는 끝낸다. 다 덮이기 전에는 받지 않는다.
    // - 모든 화면·창을 가리고, 타이틀로 가는 전환은 이 위에서 닫힌다(Overlay 층은 화면 전환 덮개 아래).
    // - 실제 시간으로 흐른다(timeScale과 무관).
    // 세 겹: 무대(프리팹 — 부품·색·글자 크기·너비) / 템포(EndingLook — 시간·간격) / 흐름(이 클래스 — 단계·입력·복제).
    // 문구·크레딧 줄은 본보기(Line_Text·CreditsTitle_Text·CreditRole_Text·CreditName_Text)를 글자 수만큼 복제한다. 색은 본보기 값 그대로(테마 무관).
    // 글꼴(제목은 Title, 나머지는 Body)과 글자 배율은 OnPresented로 받고, 바뀌면 복제된 줄의 크기·자리를 다시 놓는다.
    public sealed class EndingOverlay : UIOverlay<EndingOverlay.Refs>, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public enum Refs
        {
            // 검은 바탕. 덮기 연출이 알파를 올린다. 레이캐스트를 받아 아래 화면의 입력을 막고, 누름을 이 뷰에 올린다.
            Background_Image,
            // 엔딩 문구 묶음(CanvasGroup)과 줄 본보기(꺼 둠). 본보기의 높이가 줄 간격이다.
            Lines,
            Line_Text,
            // 크레딧 묶음(화면 아래 기준, pivot 위; CanvasGroup)과 제목·역할·이름 본보기(꺼 둠).
            Credits,
            CreditsTitle_Text,
            CreditRole_Text,
            CreditName_Text,
            // 감사 문구 묶음(CanvasGroup)과 글자, 안내 글자.
            Thanks,
            Thanks_Text,
            Prompt_Text,
        }

        private enum Phase { Idle, Covering, Lines, Credits, Thanks, Done }

        // 복제한 글자 한 줄. 본보기(크기·색)와 글꼴 역할을 기억해 테마가 바뀌면 다시 맞춘다.
        private sealed class Block
        {
            public TMP_Text Text;
            public RectTransform Rect;
            public TMP_Text Template;
            public UITextRole Role;
            public float Height;
        }

        [Tooltip("연출 템포. 비우면 기본값(EndingLook의 초기값)으로 흐른다.")]
        [SerializeField] private EndingLook _look;

        private RectTransform _rect;
        private Image _background;
        private Color _backgroundColor;
        private RectTransform _linesRoot;
        private CanvasGroup _linesGroup;
        private TMP_Text _lineTemplate;
        private RectTransform _credits;
        private CanvasGroup _creditsGroup;
        private TMP_Text _creditsTitleTemplate;
        private TMP_Text _creditRoleTemplate;
        private TMP_Text _creditNameTemplate;
        private CanvasGroup _thanksGroup;
        private TMP_Text _thanksText;
        private TMP_Text _prompt;
        private float _thanksBaseSize;
        private float _promptBaseSize;
        private Vector2 _thanksBasePosition;
        private Vector2 _promptBasePosition;
        private float _thanksBaseHeight;
        private float _promptBaseHeight;

        // 복제한 줄. 문구는 순서대로, 크레딧은 제목 → (역할 → 이름들)… 순서대로.
        private readonly List<Block> _lines = new();
        private readonly List<Block> _creditBlocks = new();
        private readonly Dictionary<TMP_Text, Stack<Block>> _pool = new();
        private EndingTexts _texts;
        private float _creditsHeight;

        // 지금 테마. OnPresented가 넘긴다.
        private TMP_FontAsset _titleFont;
        private TMP_FontAsset _bodyFont;
        private float _scale = 1f;

        private Phase _phase = Phase.Idle;
        private float _elapsed;         // 지금 단계가 흐른 시간
        private float _skipFade = -1;   // 건너뛰어 문구·크레딧을 흐리는 중이면 0 이상
        private float _skipFrom = 1;
        private float _rate = 1;            // 문구·크레딧이 흐르는 빠르기 배율(1 ~ FastForwardRate)
        private int _pointersDown;          // 화면을 누르고 있는 손가락·버튼 수
        private bool _keyHeld;              // 빨리 감기 키(Space)를 누르고 있는가
        private bool _pressedInThanks;      // 지금 누른 것이 감사 문구가 나온 뒤에 누른 것인가

        // 검은 화면이 다 덮였다(한 번). 이 뒤에는 아래 화면이 보이지 않는다.
        public event Action Covered;

        // 끝났다(한 번). 받는 쪽이 이 화면을 닫는다.
        public event Action Finished;

        public bool IsCovered => _phase != Phase.Idle && _phase != Phase.Covering;

        // 빨리 감기 키(Space)를 누르고 있는가. ScreenFlow가 매 프레임 넣는다.
        public bool FastForwardKeyHeld { set => _keyHeld = value; }

        private bool IsHeld => _pointersDown > 0 || _keyHeld;

        private EndingLook Look => _look != null ? _look : _look = ScriptableObject.CreateInstance<EndingLook>();

        protected override void OnInitialize()
        {
            _rect = (RectTransform)transform;
            _background = View.Image(Refs.Background_Image);
            _backgroundColor = _background != null ? _background.color : Color.black;

            _linesRoot = View.Rect(Refs.Lines);
            _linesGroup = Group(_linesRoot);
            _lineTemplate = Template(Refs.Line_Text);

            _credits = View.Rect(Refs.Credits);
            _creditsGroup = Group(_credits);
            _creditsTitleTemplate = Template(Refs.CreditsTitle_Text);
            _creditRoleTemplate = Template(Refs.CreditRole_Text);
            _creditNameTemplate = Template(Refs.CreditName_Text);

            _thanksGroup = Group(View.Rect(Refs.Thanks));
            _thanksText = View.Text(Refs.Thanks_Text);
            _prompt = View.Text(Refs.Prompt_Text);

            if (_thanksText != null)
            {
                _thanksBaseSize = _thanksText.fontSize;
                _thanksBasePosition = _thanksText.rectTransform.anchoredPosition;
                _thanksBaseHeight = _thanksText.rectTransform.sizeDelta.y;
                _titleFont = _thanksText.font;
            }

            if (_prompt != null)
            {
                _promptBaseSize = _prompt.fontSize;
                _promptBasePosition = _prompt.rectTransform.anchoredPosition;
                _promptBaseHeight = _prompt.rectTransform.sizeDelta.y;
                _bodyFont = _prompt.font;
            }

            Reset();
        }

        // 글꼴·글자 배율. 이미 글자가 놓여 있으면 크기·자리를 다시 놓는다(단계는 그대로).
        protected override void OnPresented(in UIAppliedTheme theme)
        {
            TMP_FontAsset title = theme.FontFor(UITextRole.Title);
            TMP_FontAsset body = theme.FontFor(UITextRole.Body);
            _titleFont = title != null ? title : _titleFont;
            _bodyFont = body != null ? body : _bodyFont;
            _scale = theme.TextScale;

            ApplyTheme(_thanksText, _titleFont, _thanksBaseSize, _thanksBasePosition, _thanksBaseHeight);
            ApplyTheme(_prompt, _bodyFont, _promptBaseSize, _promptBasePosition, _promptBaseHeight);

            if (_texts != null)
                Layout();
        }

        // 글자를 놓고 처음(덮기 전)으로 되돌린다. 다시 부르면 지난 줄을 풀로 돌려 다시 쓴다. Play로 시작한다.
        internal void Present(EndingTexts texts)
        {
            _texts = texts ?? new EndingTexts();

            ReleaseAll(_lines);
            ReleaseAll(_creditBlocks);

            if (_linesRoot != null)
            {
                foreach (string line in _texts.Lines)
                    _lines.Add(Take(_lineTemplate, _linesRoot, UITextRole.Body, line));
            }

            if (_credits != null)
            {
                AddCreditBlock(_creditsTitleTemplate, UITextRole.Title, _texts.CreditsTitle);

                foreach (EndingCredit credit in _texts.Credits)
                {
                    AddCreditBlock(_creditRoleTemplate, UITextRole.Body, credit.Role);

                    foreach (string name in credit.Names)
                        AddCreditBlock(_creditNameTemplate, UITextRole.Body, name);
                }
            }

            if (_thanksText != null)
                _thanksText.text = _texts.Thanks;

            if (_prompt != null)
                _prompt.text = _texts.Prompt;

            Layout();
            Reset();
        }

        // 덮기부터 흐르기 시작한다.
        public void Play()
        {
            if (_phase != Phase.Idle)
                Reset();

            Enter(Phase.Covering);
        }

        // 건너뛰기(Esc). 다 덮이기 전에는 받지 않는다(그때는 아직 판을 결산하지 않았다).
        public void Skip()
        {
            switch (_phase)
            {
                case Phase.Lines:
                case Phase.Credits:
                    if (_skipFade < 0)
                    {
                        _skipFade = 0;
                        _skipFrom = Mathf.Max(_linesGroup.alpha, _creditsGroup.alpha);
                    }
                    break;
                case Phase.Thanks:
                    TryFinish();
                    break;
            }
        }

        // 계속 키(Space)를 눌렀다: 감사 문구에서만 끝낸다. 문구·크레딧 중에는 누르고 있는 동안 빨리 감을 뿐이다(FastForwardKeyHeld).
        public void Continue()
        {
            if (_phase == Phase.Thanks)
                TryFinish();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointersDown++;
            _pressedInThanks = _phase == Phase.Thanks;
        }

        public void OnPointerUp(PointerEventData eventData) => _pointersDown = Mathf.Max(0, _pointersDown - 1);

        // 감사 문구에서 새로 누르면 끝낸다. 문구·크레딧 중에는 누르고 있는 동안 빨리 감을 뿐 건너뛰지 않는다.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_phase == Phase.Thanks && _pressedInThanks)
                TryFinish();
        }

        // 포커스를 잃은 사이에 뗀 손은 OnPointerUp이 오지 않을 수 있다 — 빨리 감기가 걸린 채 남지 않게 푼다.
        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
                _pointersDown = 0;
        }

        private void Update()
        {
            if (_phase == Phase.Idle || _phase == Phase.Done)
                return;

            EndingLook look = Look;
            float delta = Mathf.Min(Time.unscaledDeltaTime, 1f / 15);

            // 문구·크레딧은 누르고 있는 동안 빨리 흐른다. 덮기·감사 문구·건너뛰기 흐림은 실제 시간 그대로.
            bool playing = _phase == Phase.Lines || _phase == Phase.Credits;
            float target = playing && IsHeld ? look.FastForwardRate : 1;
            _rate = Mathf.MoveTowards(_rate, target, (look.FastForwardRate - 1) / look.FastForwardRampSeconds * delta);
            float step = playing ? delta * _rate : delta;
            _elapsed += step;

            switch (_phase)
            {
                case Phase.Covering:
                    SetBackgroundAlpha(UIMotion.Smooth(_elapsed / look.CoverSeconds));

                    if (_elapsed >= look.CoverSeconds)
                    {
                        Enter(Phase.Lines);
                        Covered?.Invoke();
                    }
                    break;

                case Phase.Lines:
                    AdvanceLines(look);
                    break;

                case Phase.Credits:
                    AdvanceCredits(look, step);
                    break;

                case Phase.Thanks:
                    _thanksGroup.alpha = UIMotion.Smooth(_elapsed / look.ThanksFadeSeconds);
                    float prompt = _elapsed - look.PromptDelay;

                    if (_prompt != null)
                        _prompt.alpha = prompt <= 0 ? 0 : Mathf.Min(1, prompt) * (0.55f + 0.45f * Mathf.Cos(prompt * look.PromptPulseHz * Mathf.PI * 2));
                    break;
            }

            if (_skipFade >= 0 && (_phase == Phase.Lines || _phase == Phase.Credits))
                AdvanceSkip(look, delta);
        }

        private void AdvanceLines(EndingLook look)
        {
            if (_lines.Count == 0)
            {
                Enter(Phase.Credits);
                return;
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                float t = Mathf.Clamp01((_elapsed - look.LineDelay - i * look.LineInterval) / look.LineFadeSeconds);
                float eased = UIMotion.Smooth(t);
                Block line = _lines[i];
                line.Text.alpha = eased;
                Vector2 position = line.Rect.anchoredPosition;
                line.Rect.anchoredPosition = new Vector2(position.x, BaseLineY(i) - look.LineRise * (1 - eased));
            }

            float shown = look.LineDelay + (_lines.Count - 1) * look.LineInterval + look.LineFadeSeconds + look.LinesHoldSeconds;
            float fade = Mathf.Clamp01((_elapsed - shown) / look.LinesOutSeconds);

            if (_skipFade < 0)
                _linesGroup.alpha = 1 - UIMotion.Smooth(fade);

            if (fade >= 1)
                Enter(Phase.Credits);
        }

        private void AdvanceCredits(EndingLook look, float delta)
        {
            _creditsGroup.alpha = _skipFade < 0 ? 1 : _creditsGroup.alpha;
            float screen = _rect.rect.height;
            Vector2 position = _credits.anchoredPosition;
            position.y += look.CreditsSpeed * delta;
            _credits.anchoredPosition = position;

            // 크레딧의 마지막 줄이 화면 위로 다 나가면 감사 문구.
            if (position.y - _creditsHeight >= screen)
                Enter(Phase.Thanks);
        }

        // 건너뛰기: 보이던 문구·크레딧을 흐린 뒤 감사 문구로.
        private void AdvanceSkip(EndingLook look, float delta)
        {
            _skipFade += delta;
            float alpha = _skipFrom * (1 - Mathf.Clamp01(_skipFade / look.SkipFadeSeconds));
            _linesGroup.alpha = Mathf.Min(_linesGroup.alpha, alpha);
            _creditsGroup.alpha = Mathf.Min(_creditsGroup.alpha, alpha);

            if (_skipFade >= look.SkipFadeSeconds)
                Enter(Phase.Thanks);
        }

        private void Enter(Phase phase)
        {
            _phase = phase;
            _elapsed = 0;

            if (phase == Phase.Lines)
            {
                SetBackgroundAlpha(1);
                _linesGroup.alpha = 1;
            }
            else if (phase == Phase.Credits)
            {
                _linesGroup.alpha = 0;
                _creditsGroup.alpha = 1;
                _credits.anchoredPosition = Vector2.zero;
            }
            else if (phase == Phase.Thanks)
            {
                _skipFade = -1;
                _linesGroup.alpha = 0;
                _creditsGroup.alpha = 0;
            }
        }

        // 처음(덮기 전): 배경 투명, 묶음 투명, 크레딧 제자리, 입력 상태 비움.
        private void Reset()
        {
            _phase = Phase.Idle;
            _elapsed = 0;
            _skipFade = -1;
            _skipFrom = 1;
            _rate = 1;
            _pointersDown = 0;
            _keyHeld = false;
            _pressedInThanks = false;

            SetBackgroundAlpha(0);

            if (_linesGroup != null)
                _linesGroup.alpha = 0;

            if (_creditsGroup != null)
                _creditsGroup.alpha = 0;

            if (_credits != null)
                _credits.anchoredPosition = Vector2.zero;

            if (_thanksGroup != null)
                _thanksGroup.alpha = 0;

            if (_prompt != null)
                _prompt.alpha = 0;

            foreach (Block line in _lines)
                line.Text.alpha = 0;
        }

        // 감사 문구가 막 나온 때(안내 전 절반)는 받지 않는다.
        private void TryFinish()
        {
            if (_elapsed >= Look.PromptDelay * 0.5f)
                Finish();
        }

        private void Finish()
        {
            if (_phase == Phase.Done)
                return;

            _phase = Phase.Done;
            Finished?.Invoke();
        }

        // ---- 배치 ----

        // 복제된 줄의 글꼴·크기·높이·자리를 지금 배율로 놓는다. 문구는 가운데에 쌓고, 크레딧은 위에서부터 쌓는다.
        private void Layout()
        {
            EndingLook look = Look;

            for (int i = 0; i < _lines.Count; i++)
            {
                Block line = _lines[i];
                ApplyTheme(line);
                line.Height = LineHeight();
                line.Rect.sizeDelta = new Vector2(line.Template.rectTransform.sizeDelta.x, line.Height);
                line.Rect.anchoredPosition = new Vector2(0, BaseLineY(i));
            }

            // 크레딧: 글자 묶음(_texts)의 차례대로 위에서부터 쌓는다. 빈 글자는 덩이가 없지만 간격은 그대로 둔다(Present와 같은 차례).
            int next = 0;
            float y = PlaceCredit(ref next, _texts.CreditsTitle, 0) - look.TitleGap * _scale;

            foreach (EndingCredit credit in _texts.Credits)
            {
                y = PlaceCredit(ref next, credit.Role, y) - look.RoleGap * _scale;

                foreach (string name in credit.Names)
                    y = PlaceCredit(ref next, name, y) - look.NameGap * _scale;

                y -= look.SectionGap * _scale;
            }

            _creditsHeight = -y;

            if (_credits != null && _creditsTitleTemplate != null)
                _credits.sizeDelta = new Vector2(_creditsTitleTemplate.rectTransform.sizeDelta.x, _creditsHeight);
        }

        // 글자가 있으면 다음 덩이를 y에 놓고 그 아래 y를 돌려준다. 높이는 글자 크기의 1.25배와 실제 줄 높이 중 큰 것.
        private float PlaceCredit(ref int next, string text, float y)
        {
            if (string.IsNullOrEmpty(text) || next >= _creditBlocks.Count)
                return y;

            Block block = _creditBlocks[next++];
            ApplyTheme(block);
            float width = block.Template.rectTransform.sizeDelta.x;
            block.Height = Mathf.Max(block.Text.fontSize * 1.25f, block.Text.GetPreferredValues(block.Text.text, width, 0).y);
            block.Rect.sizeDelta = new Vector2(width, block.Height);
            block.Rect.anchoredPosition = new Vector2(0, y);
            return y - block.Height;
        }

        // 문구 한 줄의 높이(= 줄 간격). 본보기의 높이 × 배율.
        private float LineHeight() => (_lineTemplate != null ? _lineTemplate.rectTransform.sizeDelta.y : 80f) * _scale;

        private float BaseLineY(int index)
        {
            float lineHeight = LineHeight();
            float top = (_lines.Count - 1) * lineHeight * 0.5f;
            return top - index * lineHeight;
        }

        private void ApplyTheme(Block block) =>
            ApplyTheme(block.Text, block.Role == UITextRole.Title ? _titleFont : _bodyFont, block.Template.fontSize, block.Template.rectTransform.anchoredPosition);

        // 글꼴, 크기(본보기 × 배율), 자리(본보기 × 배율). baseHeight가 있으면 높이도 배율로(너비는 그대로).
        private void ApplyTheme(TMP_Text text, TMP_FontAsset font, float baseSize, Vector2 basePosition, float baseHeight = -1)
        {
            if (text == null)
                return;

            if (font != null)
                text.font = font;

            text.fontSize = baseSize * _scale;
            text.rectTransform.anchoredPosition = basePosition * _scale;

            if (baseHeight >= 0)
                text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, baseHeight * _scale);
        }

        private void AddCreditBlock(TMP_Text template, UITextRole role, string text)
        {
            if (string.IsNullOrEmpty(text) || template == null)
                return;

            _creditBlocks.Add(Take(template, _credits, role, text));
        }

        // 본보기를 복제해(또는 풀에서 꺼내) 글자를 채운다. 크기·자리는 Layout이 놓는다.
        private Block Take(TMP_Text template, RectTransform parent, UITextRole role, string text)
        {
            Block block;

            if (_pool.TryGetValue(template, out Stack<Block> pool) && pool.Count > 0)
            {
                block = pool.Pop();
            }
            else
            {
                GameObject view = Instantiate(template.gameObject, parent, false);
                view.name = template.name;
                TMP_Text copy = view.GetComponent<TMP_Text>();
                block = new Block { Text = copy, Rect = copy.rectTransform, Template = template, Role = role };
            }

            block.Text.text = text;
            block.Text.alpha = 1;
            block.Rect.gameObject.SetActive(true);
            return block;
        }

        private void ReleaseAll(List<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                block.Rect.gameObject.SetActive(false);

                if (!_pool.TryGetValue(block.Template, out Stack<Block> pool))
                    _pool[block.Template] = pool = new Stack<Block>();

                pool.Push(block);
            }

            blocks.Clear();
        }

        private void SetBackgroundAlpha(float alpha)
        {
            if (_background == null)
                return;

            Color color = _backgroundColor;
            color.a = alpha;
            _background.color = color;
        }

        private TMP_Text Template(Refs key)
        {
            TMP_Text template = View.Text(key);

            if (template != null)
                template.gameObject.SetActive(false);

            return template;
        }

        private static CanvasGroup Group(RectTransform rect)
        {
            if (rect == null)
                return null;

            CanvasGroup group = rect.GetComponent<CanvasGroup>() ?? rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }
    }
}
