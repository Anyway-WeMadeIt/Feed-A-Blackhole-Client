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

    // 은하 체력바 — RPG 보스바처럼 체력을 여러 줄로 나눈다. 화면 아래 가운데. 부품은 BattleScreen 프리팹에 있고(Refs.Galaxy*), 이 클래스는 연출만 한다.
    // - 줄 수 = 색별 줄 수(HealthLines). 은하의 크기는 체력을 바꾸지 않는다(EnemyDefinition.StatsAt).
    // - 막대 하나가 지금 줄이다. 줄이 비면 아래에 깔린 다음 줄(색이 다르다)이 드러난다. 오른쪽 "x240"은 남은 줄 수(지금 줄 포함)이고 줄이 줄 때마다 튄다.
    // - 막대 안에 남은 체력 / 최대 체력. 깎인 몫은 흰 꼬리가 잠깐 남았다가 따라 줄어든다(줄을 넘으면 새 줄 전체에서 다시). 맞은 순간 막대가 잠깐 밝아진다.
    // - 나타날 때 아래에서 살짝 떠오르며 보이고, 은하가 죽으면 x0이 된 뒤 잠시 있다가 흐려진다.
    // - 판(Session)을 모른다. BattleScreen이 상태(GalaxyHealthState)를 넘기고 Tick으로 연출을 흘린다(실제 시간).
    // - 이름·줄 수 글자와 바탕·홈은 화면의 Styled가 테마(글꼴·크기·색)를 맞춘다. 막대 안 수치는 막대 위에 읽히도록 흰 글자 + 그림자로 고정하고 글꼴·크기만 따른다.
    internal sealed class GalaxyHealthBar
    {
        // 프리팹의 부품. BattleScreen이 Refs로 찾아 넘긴다. 이름이 같은 오브젝트가 프리팹에 있어야 한다(없으면 그 부품만 빈다).
        public readonly struct Parts
        {
            public readonly RectTransform Root;      // GalaxyHealth: 막대 전체(CanvasGroup). 화면 아래 가운데에 놓고 꺼 둔다
            public readonly Image Panel;             // GalaxyPanel_Image: 바탕(테마 Surface)
            public readonly Image Track;             // GalaxyTrack_Image: 막대 홈(테마 Line)
            public readonly Image NextLine;          // GalaxyNextLine_Image: 홈에 깔린 다음 줄
            public readonly Image Trail;             // GalaxyTrail_Image: 깎인 몫의 흰 꼬리
            public readonly Image Fill;              // GalaxyFill_Image: 지금 줄
            public readonly TMP_Text Health;         // GalaxyHealth_Text: 막대 안 수치(흰 글자)
            public readonly TMP_Text HealthShadow;   // GalaxyHealthShadow_Text: 수치 그림자(검정, 오른쪽 아래로 2px)
            public readonly TMP_Text Name;           // GalaxyName_Text: 은하 이름(막대 위 왼쪽)
            public readonly TMP_Text Lines;          // GalaxyLines_Text: 남은 줄 수 "x240"(막대 오른쪽)

            public Parts(RectTransform root, Image panel, Image track, Image nextLine, Image trail, Image fill,
                TMP_Text health, TMP_Text healthShadow, TMP_Text name, TMP_Text lines)
            {
                Root = root;
                Panel = panel;
                Track = track;
                NextLine = nextLine;
                Trail = trail;
                Fill = fill;
                Health = health;
                HealthShadow = healthShadow;
                Name = name;
                Lines = lines;
            }
        }

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
        // 프리팹의 수치 글꼴·크기. 테마가 글꼴을 주지 않으면 이 글꼴로, 크기는 이것 × 글자 배율.
        private readonly TMP_FontAsset _baseFont;
        private readonly float _healthBaseSize;
        // 프리팹에 놓인 자리. 나타날 때 이 아래에서 떠오른다.
        private readonly Vector2 _restPosition;

        private int _key = int.MinValue;
        // 줄 나누기(줄 수·줄 체력·줄 색).
        private readonly HealthLines _lines = new HealthLines();
        private float _shownHealth = float.NaN;
        private int _line;            // 지금 줄(1 = 마지막 줄, 0 = 죽음)
        private float _fraction;      // 지금 줄 안의 남은 몫(0 ~ 1)
        private float _trailFraction;
        private float _sinceHit = float.PositiveInfinity;
        // 줄 수가 줄 때 글자 튕김.
        private readonly ScalePop _countPop = new ScalePop(CountPopSeconds, CountPopScale);
        private float _appear;
        private float _sinceDeath = float.NaN;
        private bool _active;

        // parts: 프리팹의 부품. styled: 바탕·홈·이름·줄 수를 테마에 맞추는 화면의 Styled.
        public GalaxyHealthBar(in Parts parts, UIStyledElements styled)
        {
            _root = parts.Root;
            _group = _root.GetComponent<CanvasGroup>() ?? _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0;
            _restPosition = _root.anchoredPosition;

            if (parts.Panel != null)
                styled.AddImageColor(parts.Panel, UIColorRole.Surface);

            if (parts.Track != null)
                styled.AddImageColor(parts.Track, UIColorRole.Line);

            _under = parts.NextLine;
            _trail = parts.Trail != null ? parts.Trail.rectTransform : null;
            _fillImage = parts.Fill;
            _fill = _fillImage != null ? _fillImage.rectTransform : null;
            _health = parts.Health;
            _healthShadow = parts.HealthShadow;
            _name = parts.Name;
            _count = parts.Lines;
            _baseFont = _health != null ? _health.font : null;
            _healthBaseSize = _health != null ? _health.fontSize : 0;

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
                if (text == null)
                    continue;

                text.font = font != null ? font : _baseFont;
                text.fontSize = _healthBaseSize * theme.TextScale;
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
                    if (_count != null)
                        _countPop.Play(_count.transform);
                }

                _line = line;

                if (_count != null)
                    _count.text = "x" + line.ToString(CultureInfo.InvariantCulture);

                ApplyLineColors();
            }

            _shownHealth = galaxy.Health;
            _fraction = _lines.FractionOf(galaxy.Health, line);
            string health = NumberText.Compact((double)Mathf.Max(0, Mathf.Ceil(galaxy.Health))) + " / " + NumberText.Compact((double)galaxy.MaxHealth);

            if (_health != null)
                _health.text = health;

            if (_healthShadow != null)
                _healthShadow.text = health;

            if (galaxy.IsDead && float.IsNaN(_sinceDeath))
                _sinceDeath = 0;
        }

        // 언어가 바뀌었다. 보이는 이름을 다시 쓴다.
        public void Rename(string name)
        {
            if (_active && _name != null)
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
            float ease = UIMotion.EaseOut(_appear);
            _root.anchoredPosition = _restPosition - new Vector2(0, AppearRise * (1 - ease));

            // 꼬리: 맞은 뒤 잠깐 머물렀다가 막대를 따라 줄어든다. 막대보다 짧아지지 않는다.
            _sinceHit += delta;

            if (_sinceHit >= TrailHoldSeconds)
                _trailFraction = Mathf.Lerp(_trailFraction, _fraction, UIMotion.Follow(TrailSharpness, delta));

            _trailFraction = Mathf.Max(_trailFraction, _fraction);
            SetWidth(_fill, _fraction);
            SetWidth(_trail, _trailFraction);

            float flash = _sinceHit < FlashSeconds ? FlashAmount * (1 - _sinceHit / FlashSeconds) : 0;

            if (_fillImage != null)
                _fillImage.color = Color.Lerp(_lines.ColorOf(_line), Color.white, flash);

            _countPop.Tick(delta);

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
            _countPop.Stop();
            _sinceDeath = float.NaN;
            _appear = 0;

            if (_name != null)
                _name.text = name;

            _active = true;
            _group.alpha = 0;
            _root.gameObject.SetActive(true);
        }

        // 지금 줄 아래에 깔린 다음 줄의 색. 마지막 줄이면 비운다(홈이 보인다).
        private void ApplyLineColors()
        {
            bool hasNext = _line > 1;

            if (_under != null)
            {
                _under.enabled = hasNext;

                if (hasNext)
                    _under.color = _lines.ColorOf(_line - 1);
            }

            if (_fillImage != null)
                _fillImage.color = _lines.ColorOf(_line);
        }

        private static void SetWidth(RectTransform rect, float fraction)
        {
            if (rect == null)
                return;

            Vector2 max = rect.anchorMax;

            if (Mathf.Approximately(max.x, fraction))
                return;

            max.x = fraction;
            rect.anchorMax = max;
        }

    }
}
