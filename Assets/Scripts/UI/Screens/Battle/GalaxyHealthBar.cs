using System.Globalization;
using BlackHole.Systems.Numbers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 전투 화면에 보이는 은하 한 마리의 상태. GameHost가 판의 은하를 찾아 매 프레임 넘긴다. 은하가 없는 판이면 넘기지 않는다(null).
    // Key는 은하마다 다른 값(적 번호)이라, 바뀌면 새 은하로 보고 막대를 처음부터 연다.
    internal readonly struct GalaxyHealthState
    {
        public readonly int Key;
        public readonly int Tier;
        public readonly int Size;
        public readonly float Health;
        public readonly float MaxHealth;
        public readonly Color Color;

        public GalaxyHealthState(int key, int tier, int size, float health, float maxHealth, Color color)
        {
            Key = key;
            Tier = tier;
            Size = size;
            Health = health;
            MaxHealth = maxHealth;
            Color = color;
        }

        public bool IsDead => Health <= 0;
    }

    // 은하 체력바 [임시: 프리팹 없이 코드로 만든다] — RPG 보스바처럼 체력을 여러 줄로 나눈다. 화면 아래 가운데.
    // - 줄 수 = 색별 줄 수(HealthLines). 은하의 크기는 체력을 바꾸지 않는다(EnemyDefinition.StatsAt).
    // - 막대 하나가 지금 줄이다. 줄이 비면 아래에 깔린 다음 줄(색이 다르다)이 드러난다. 오른쪽 "x240"은 남은 줄 수(지금 줄 포함)이고 줄이 줄 때마다 튄다.
    // - 막대 안에 남은 체력 / 최대 체력. 깎인 몫은 흰 꼬리가 잠깐 남았다가 따라 줄어든다(줄을 넘으면 새 줄 전체에서 다시). 맞은 순간 막대가 잠깐 밝아진다.
    // - 나타날 때 아래에서 살짝 떠오르며 보이고, 은하가 죽으면 x0이 된 뒤 잠시 있다가 흐려진다.
    // - 판(Session)을 모른다. BattleScreen이 상태(GalaxyHealthState)를 넘기고 Tick으로 연출을 흘린다(실제 시간).
    // - 이름·줄 수 글자와 바탕·홈은 화면의 Styled가 테마(글꼴·크기·색)를 맞춘다. 막대 안 수치는 막대 위에 읽히도록 흰 글자 + 그림자로 고정하고 글꼴·크기만 따른다.
    internal sealed class GalaxyHealthBar
    {
        // 배치(기준 해상도 1920×1080, Safe Area 안). 화면에서 보고 맞출 값이다.
        private const float Width = 980f;
        private const float Height = 104f;
        private const float BottomMargin = 32f;
        private const float Padding = 24f;
        private const float TrackHeight = 34f;
        private const float TrackBottom = 18f;
        private const float CountWidth = 150f;
        private const float NameSize = 30f;
        private const float HealthSize = 24f;
        private const float CountSize = 40f;

        // 연출(초). 실제 시간으로 흐른다.
        private const float AppearSeconds = 0.3f;
        private const float AppearRise = 24f;
        private const float TrailHoldSeconds = 0.3f;
        private const float TrailSharpness = 7f;
        private const float FlashSeconds = 0.12f;
        private const float FlashAmount = 0.5f;
        private const float CountPopSeconds = 0.15f;
        private const float CountPopScale = 0.18f;
        private const float DeathHoldSeconds = 0.8f;
        private const float FadeSeconds = 0.4f;

        private static readonly Color TrailColor = new(1f, 1f, 1f, 0.8f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.65f);
        private static readonly Vector2 ShadowOffset = new(2f, -2f);

        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly Image _under;
        private readonly RectTransform _fill;
        private readonly Image _fillImage;
        private readonly RectTransform _trail;
        private readonly TMP_Text _name;
        private readonly TMP_Text _health;
        private readonly TMP_Text _healthShadow;
        private readonly TMP_Text _count;
        private readonly TMP_FontAsset _baseFont;

        private int _key = int.MinValue;
        // 줄 나누기(줄 수·줄 체력·줄 색).
        private readonly HealthLines _lines = new HealthLines();
        private float _shownHealth = float.NaN;
        private int _line;            // 지금 줄(1 = 마지막 줄, 0 = 죽음)
        private float _fraction;      // 지금 줄 안의 남은 몫(0 ~ 1)
        private float _trailFraction;
        private float _sinceHit = float.PositiveInfinity;
        private float _sincePop = float.PositiveInfinity;
        private float _appear;
        private float _sinceDeath = float.NaN;
        private bool _active;

        public GalaxyHealthBar(RectTransform parent, TMP_FontAsset font, UIStyledElements styled)
        {
            _baseFont = font;

            _root = UIParts.NewRect("Galaxy Health", parent);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.sizeDelta = new Vector2(Width, Height);
            _root.anchoredPosition = new Vector2(0, BottomMargin);

            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0;

            Image panel = UIParts.NewImage("Panel", _root, new Color(1, 1, 1, 0.82f));
            UIParts.Stretch(panel.rectTransform);
            styled.AddImageColor(panel, UIColorRole.Surface);

            // 막대 홈: 왼쪽 여백부터 줄 수 칸 앞까지.
            RectTransform track = UIParts.NewRect("Track", _root);
            track.anchorMin = new Vector2(0, 0);
            track.anchorMax = new Vector2(1, 0);
            track.pivot = new Vector2(0.5f, 0);
            track.offsetMin = new Vector2(Padding, TrackBottom);
            track.offsetMax = new Vector2(-(Padding + CountWidth), TrackBottom + TrackHeight);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.raycastTarget = false;
            trackImage.color = new Color(1, 1, 1, 0.35f);
            styled.AddImageColor(trackImage, UIColorRole.Line);

            _under = UIParts.NewImage("Next Line", track, Color.white);
            UIParts.Stretch(_under.rectTransform);
            _trail = UIParts.NewImage("Trail", track, TrailColor).rectTransform;
            UIParts.Stretch(_trail);
            _fillImage = UIParts.NewImage("Fill", track, Color.white);
            _fill = _fillImage.rectTransform;
            UIParts.Stretch(_fill);

            // 막대 안 수치: 그림자 → 글자 순서로 그린다.
            _healthShadow = NewText("Health Shadow", track, font, HealthSize, TextAlignmentOptions.Center);
            UIParts.Stretch((RectTransform)_healthShadow.transform);
            ((RectTransform)_healthShadow.transform).anchoredPosition = ShadowOffset;
            _healthShadow.color = ShadowColor;
            _health = NewText("Health", track, font, HealthSize, TextAlignmentOptions.Center);
            UIParts.Stretch((RectTransform)_health.transform);
            _health.color = Color.white;

            // 이름: 막대 위 왼쪽.
            _name = NewText("Name", _root, font, NameSize, TextAlignmentOptions.BottomLeft);
            RectTransform nameRect = (RectTransform)_name.transform;
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(Padding, TrackBottom + TrackHeight + 4);
            nameRect.offsetMax = new Vector2(-Padding, -6);

            // 남은 줄 수: 막대 오른쪽, 막대 높이 가운데.
            _count = NewText("Lines", _root, font, CountSize, TextAlignmentOptions.Right);
            RectTransform countRect = (RectTransform)_count.transform;
            countRect.anchorMin = new Vector2(1, 0);
            countRect.anchorMax = new Vector2(1, 0);
            countRect.pivot = new Vector2(1, 0.5f);
            countRect.sizeDelta = new Vector2(CountWidth - 8, TrackHeight + 16);
            countRect.anchoredPosition = new Vector2(-Padding, TrackBottom + TrackHeight * 0.5f);

            styled.AddText(_name, UITextRole.Body, UITextSizePolicy.Prefab);
            styled.AddText(_count, UITextRole.Title, UITextSizePolicy.Prefab);

            _root.gameObject.SetActive(false);
        }

        // 막대 안 수치의 글꼴·크기(색은 흰색 고정). BattleScreen.OnPresented가 넘긴다.
        public void ApplyTheme(in UIAppliedTheme theme)
        {
            TMP_FontAsset font = theme.FontFor(UITextRole.Caption);

            foreach (TMP_Text text in new[] { _health, _healthShadow })
            {
                text.font = font != null ? font : _baseFont;
                text.fontSize = HealthSize * theme.TextScale;
            }
        }

        // 이번 프레임의 은하 상태. 은하가 없으면 null — 보이던 막대는 바로 숨긴다(새 판·은하 없는 판).
        // name: 지금 언어의 은하 이름(색). 은하가 바뀌거나 언어가 바뀔 때만 다시 쓴다.
        public void Show(GalaxyHealthState? state, string name)
        {
            if (state is not GalaxyHealthState galaxy)
            {
                Hide();
                return;
            }

            if (galaxy.Key != _key)
                Open(galaxy, name);

            if (galaxy.Health == _shownHealth)
                return;

            bool hit = !float.IsNaN(_shownHealth) && galaxy.Health < _shownHealth;
            int line = _lines.LineOf(galaxy.Health);

            if (hit)
                _sinceHit = 0;

            if (line != _line)
            {
                // 줄을 넘었다: 꼬리는 새 줄 전체에서 다시 줄어든다. 줄 수가 튄다.
                if (!float.IsNaN(_shownHealth))
                {
                    _trailFraction = 1;
                    _sincePop = 0;
                }

                _line = line;
                _count.text = "x" + line.ToString(CultureInfo.InvariantCulture);
                ApplyLineColors();
            }

            _shownHealth = galaxy.Health;
            _fraction = _lines.FractionOf(galaxy.Health, line);
            string health = NumberText.Compact((double)Mathf.Max(0, Mathf.Ceil(galaxy.Health))) + " / " + NumberText.Compact((double)galaxy.MaxHealth);
            _health.text = health;
            _healthShadow.text = health;

            if (galaxy.IsDead && float.IsNaN(_sinceDeath))
                _sinceDeath = 0;
        }

        // 언어가 바뀌었다. 보이는 이름을 다시 쓴다.
        public void Rename(string name)
        {
            if (_active)
                _name.text = name;
        }

        public void Hide()
        {
            if (!_active)
                return;

            _active = false;
            _key = int.MinValue;
            _root.gameObject.SetActive(false);
        }

        // 연출을 흘린다(실제 시간).
        public void Tick(float delta)
        {
            if (!_active)
                return;

            _appear = Mathf.Min(1, _appear + delta / AppearSeconds);
            float ease = 1 - (1 - _appear) * (1 - _appear);
            _root.anchoredPosition = new Vector2(0, BottomMargin - AppearRise * (1 - ease));

            // 꼬리: 맞은 뒤 잠깐 머물렀다가 막대를 따라 줄어든다. 막대보다 짧아지지 않는다.
            _sinceHit += delta;

            if (_sinceHit >= TrailHoldSeconds)
                _trailFraction = Mathf.Lerp(_trailFraction, _fraction, 1 - Mathf.Exp(-TrailSharpness * delta));

            _trailFraction = Mathf.Max(_trailFraction, _fraction);
            SetWidth(_fill, _fraction);
            SetWidth(_trail, _trailFraction);

            float flash = _sinceHit < FlashSeconds ? FlashAmount * (1 - _sinceHit / FlashSeconds) : 0;
            _fillImage.color = Color.Lerp(_lines.ColorOf(_line), Color.white, flash);

            _sincePop += delta;
            float pop = _sincePop < CountPopSeconds ? Mathf.Sin(_sincePop / CountPopSeconds * Mathf.PI) * CountPopScale : 0;
            _count.transform.localScale = Vector3.one * (1 + pop);

            float alpha = ease;

            if (!float.IsNaN(_sinceDeath))
            {
                _sinceDeath += delta;
                alpha *= 1 - Mathf.Clamp01((_sinceDeath - DeathHoldSeconds) / FadeSeconds);
            }

            _group.alpha = alpha;
        }

        private void Open(in GalaxyHealthState galaxy, string name)
        {
            _key = galaxy.Key;
            _lines.Open(galaxy.Tier, galaxy.MaxHealth, galaxy.Color);
            _shownHealth = float.NaN;
            _line = -1;
            _trailFraction = 1;
            _sinceHit = float.PositiveInfinity;
            _sincePop = float.PositiveInfinity;
            _sinceDeath = float.NaN;
            _appear = 0;
            _name.text = name;
            _active = true;
            _group.alpha = 0;
            _root.gameObject.SetActive(true);
        }

        // 지금 줄 아래에 깔린 다음 줄의 색. 마지막 줄이면 비운다(홈이 보인다).
        private void ApplyLineColors()
        {
            bool hasNext = _line > 1;
            _under.enabled = hasNext;

            if (hasNext)
                _under.color = _lines.ColorOf(_line - 1);

            _fillImage.color = _lines.ColorOf(_line);
        }

        private static void SetWidth(RectTransform rect, float fraction)
        {
            Vector2 max = rect.anchorMax;

            if (Mathf.Approximately(max.x, fraction))
                return;

            max.x = fraction;
            rect.anchorMax = max;
        }

        // 흰 글자, 한 줄(NoWrap).
        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment) =>
            UIParts.NewText(name, parent, font, size, Color.white, alignment, TextWrappingModes.NoWrap);
    }
}
