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

    // 엔딩 화면 [임시: 프리팹 없이 코드로 만든다(EndingLayout). 나중에 프리팹으로].
    // 순서: 검은 화면이 천천히 덮는다 → 다 덮이면 Covered(이때 판을 결산한다) → 엔딩 문구가 한 줄씩 떠오른다 → 크레딧이 아래에서 위로 흐른다 →
    //       감사 문구와 안내("눌러서 타이틀로") → 누르면 Finished(타이틀로 간다).
    // - 빨리 감기: 화면을 누르고 있는 동안(클릭·터치, Space) 문구·크레딧이 FastForwardRate배로 흐른다. 떼면 원래 빠르기로 돌아온다. 건너뛰지는 않는다.
    // - 감사 문구에서 누르면 끝낸다. 감사 문구가 나온 뒤에 새로 누른 것만 받는다(크레딧을 빨리 감다가 뗀 것으로는 끝나지 않는다).
    // - 건너뛰기(Skip: Esc): 문구·크레딧 중이면 감사 문구로 바로 간다. 감사 문구에서는 끝낸다. 다 덮이기 전에는 받지 않는다.
    // - 맨 위 캔버스에서 화면 전환 덮개(ScreenTransition) 바로 아래에 둔다 — 모든 화면·창을 가리고, 타이틀로 가는 전환은 이 위에서 닫힌다.
    // - 실제 시간으로 흐른다(timeScale과 무관). 색은 테마와 무관하게 검은 바탕·밝은 글자로 고정한다.
    [DisallowMultipleComponent]
    internal sealed class EndingScreen : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private enum Phase { Covering, Lines, Credits, Thanks, Done }

        // 시간(초) [임시].
        private const float CoverSeconds = 1.6f;
        private const float LineDelay = 0.8f;           // 다 덮인 뒤 첫 줄까지
        private const float LineInterval = 2.6f;        // 줄 사이
        private const float LineFadeSeconds = 1.0f;
        private const float LineRise = 18f;             // 떠오르는 거리(px)
        private const float LinesHoldSeconds = 3.0f;    // 마지막 줄이 다 보인 뒤 머묾
        private const float LinesOutSeconds = 1.2f;
        private const float CreditsSpeed = 110f;        // px/초
        private const float ThanksFadeSeconds = 1.4f;
        private const float PromptDelay = 1.0f;         // 감사 문구 뒤 안내까지
        private const float PromptPulseHz = 0.6f;
        private const float SkipFadeSeconds = 0.5f;
        private const float FastForwardRate = 5f;           // 누르고 있을 때 문구·크레딧 빠르기 배율
        private const float FastForwardRampSeconds = 0.2f;  // 1배 ↔ 빨리 감기 배율로 바뀌는 데 걸리는 시간

        private static readonly Color Background = new(0.015f, 0.01f, 0.03f, 1f);

        private RectTransform _rect;
        private Image _background;
        private IReadOnlyList<TMP_Text> _lines;
        private CanvasGroup _linesGroup;
        private RectTransform _credits;
        private CanvasGroup _creditsGroup;
        private float _creditsHeight;
        private CanvasGroup _thanksGroup;
        private TMP_Text _prompt;

        private Phase _phase = Phase.Covering;
        private float _elapsed;         // 지금 단계가 흐른 시간
        private float _skipFade = -1;   // 건너뛰어 문구·크레딧을 흐리는 중이면 0 이상
        private float _skipFrom = 1;
        private float _rate = 1;            // 문구·크레딧이 흐르는 빠르기 배율(1 ~ FastForwardRate)
        private int _pointersDown;          // 화면을 누르고 있는 손가락·버튼 수
        private bool _keyHeld;              // 빨리 감기 키(Space)를 누르고 있는가
        private bool _pressedInThanks;      // 지금 누른 것이 감사 문구가 나온 뒤에 누른 것인가

        // 검은 화면이 다 덮였다(한 번). 이 뒤에는 아래 화면이 보이지 않는다.
        public event Action Covered;

        // 끝났다(한 번). 받는 쪽이 이 화면을 지운다.
        public event Action Finished;

        public bool IsCovered => _phase != Phase.Covering;

        // 빨리 감기 키(Space)를 누르고 있는가. ScreenFlow가 매 프레임 넣는다.
        public bool FastForwardKeyHeld { set => _keyHeld = value; }

        private bool IsHeld => _pointersDown > 0 || _keyHeld;

        // canvas: 맨 위 캔버스. siblingIndex: 그 캔버스 안의 자리(화면 전환 덮개 바로 아래).
        // title·body: 글꼴(없으면 TMP 기본 글꼴). scale: 글자 크기 설정 배율.
        public static EndingScreen Create(Transform canvas, int siblingIndex, EndingTexts texts, TMP_FontAsset title, TMP_FontAsset body, float scale)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            var view = new GameObject("Ending Screen", typeof(RectTransform));
            view.layer = canvas.gameObject.layer;
            var rect = (RectTransform)view.transform;
            rect.SetParent(canvas, false);
            UIParts.Stretch(rect);
            rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, canvas.childCount - 1));

            var screen = view.AddComponent<EndingScreen>();
            screen.Build(rect, texts ?? new EndingTexts(), title, body ?? title, scale > 0 ? scale : 1);
            return screen;
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

        // 부품은 EndingLayout이 만든다. 이 화면은 참조만 들고 연출한다.
        private void Build(RectTransform rect, EndingTexts texts, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, float scale)
        {
            _rect = rect;
            var layout = new EndingLayout(rect, texts, titleFont, bodyFont, scale);
            _background = layout.Background;
            _background.color = WithAlpha(Background, 0);
            _lines = layout.Lines;
            _linesGroup = layout.LinesGroup;
            _credits = layout.Credits;
            _creditsGroup = layout.CreditsGroup;
            _creditsHeight = layout.CreditsHeight;
            _thanksGroup = layout.ThanksGroup;
            _prompt = layout.Prompt;
        }

        private void Update()
        {
            float delta = Mathf.Min(Time.unscaledDeltaTime, 1f / 15);

            // 문구·크레딧은 누르고 있는 동안 빨리 흐른다. 덮기·감사 문구·건너뛰기 흐림은 실제 시간 그대로.
            bool playing = _phase == Phase.Lines || _phase == Phase.Credits;
            float target = playing && IsHeld ? FastForwardRate : 1;
            _rate = Mathf.MoveTowards(_rate, target, (FastForwardRate - 1) / FastForwardRampSeconds * delta);
            float step = playing ? delta * _rate : delta;
            _elapsed += step;

            switch (_phase)
            {
                case Phase.Covering:
                    _background.color = WithAlpha(Background, UIMotion.Smooth(_elapsed / CoverSeconds));

                    if (_elapsed >= CoverSeconds)
                    {
                        Enter(Phase.Lines);
                        Covered?.Invoke();
                    }
                    break;

                case Phase.Lines:
                    AdvanceLines();
                    break;

                case Phase.Credits:
                    AdvanceCredits(step);
                    break;

                case Phase.Thanks:
                    _thanksGroup.alpha = UIMotion.Smooth(_elapsed / ThanksFadeSeconds);
                    float prompt = _elapsed - PromptDelay;
                    _prompt.alpha = prompt <= 0 ? 0 : Mathf.Min(1, prompt) * (0.55f + 0.45f * Mathf.Cos(prompt * PromptPulseHz * Mathf.PI * 2));
                    break;
            }

            if (_skipFade >= 0 && (_phase == Phase.Lines || _phase == Phase.Credits))
                AdvanceSkip(delta);
        }

        private void AdvanceLines()
        {
            if (_lines.Count == 0)
            {
                Enter(Phase.Credits);
                return;
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                float t = Mathf.Clamp01((_elapsed - LineDelay - i * LineInterval) / LineFadeSeconds);
                float eased = UIMotion.Smooth(t);
                _lines[i].alpha = eased;
                RectTransform rect = (RectTransform)_lines[i].transform;
                Vector2 position = rect.anchoredPosition;
                rect.anchoredPosition = new Vector2(position.x, BaseLineY(i) - LineRise * (1 - eased));
            }

            float shown = LineDelay + (_lines.Count - 1) * LineInterval + LineFadeSeconds + LinesHoldSeconds;
            float fade = Mathf.Clamp01((_elapsed - shown) / LinesOutSeconds);

            if (_skipFade < 0)
                _linesGroup.alpha = 1 - UIMotion.Smooth(fade);

            if (fade >= 1)
                Enter(Phase.Credits);
        }

        private void AdvanceCredits(float delta)
        {
            _creditsGroup.alpha = _skipFade < 0 ? 1 : _creditsGroup.alpha;
            float screen = _rect.rect.height;
            Vector2 position = _credits.anchoredPosition;
            position.y += CreditsSpeed * delta;
            _credits.anchoredPosition = position;

            // 크레딧의 마지막 줄이 화면 위로 다 나가면 감사 문구.
            if (position.y - _creditsHeight >= screen)
                Enter(Phase.Thanks);
        }

        // 건너뛰기: 보이던 문구·크레딧을 흐린 뒤 감사 문구로.
        private void AdvanceSkip(float delta)
        {
            _skipFade += delta;
            float alpha = _skipFrom * (1 - Mathf.Clamp01(_skipFade / SkipFadeSeconds));
            _linesGroup.alpha = Mathf.Min(_linesGroup.alpha, alpha);
            _creditsGroup.alpha = Mathf.Min(_creditsGroup.alpha, alpha);

            if (_skipFade >= SkipFadeSeconds)
                Enter(Phase.Thanks);
        }

        private void Enter(Phase phase)
        {
            _phase = phase;
            _elapsed = 0;

            if (phase == Phase.Lines)
            {
                _background.color = Background;
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

        // 감사 문구가 막 나온 때(안내 전 절반)는 받지 않는다.
        private void TryFinish()
        {
            if (_elapsed >= PromptDelay * 0.5f)
                Finish();
        }

        private void Finish()
        {
            if (_phase == Phase.Done)
                return;

            _phase = Phase.Done;
            Finished?.Invoke();
        }

        private float BaseLineY(int index)
        {
            float lineHeight = ((RectTransform)_lines[index].transform).sizeDelta.y;
            float top = (_lines.Count - 1) * lineHeight * 0.5f;
            return top - index * lineHeight;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
