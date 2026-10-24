using System;
using UnityEngine;

namespace BlackHole.Unity
{
    // 전투 장면의 밝기 테마. 설정의 Dark Mode를 따라 전투 배경(BattleBackgroundDriver)의 그림·움직이는 문양 머티리얼을 바꾼다.
    // 설정이 바뀌면 곧바로 다시 맞춘다. GameBootstrap이 설정을 읽은 뒤 Bind로 넘긴다. 연결하지 않으면 씬에 놓인 그대로 둔다.
    // 배경 위에 뜨는 피해·Gold 글자는 아래 검정 음영이 대비를 맡아 테마와 무관하게 같은 색이다(EnemyDamageText) — 여기서 바꾸지 않는다.
    // 전투 HUD(시계·막대·이정표 칸)의 색은 UI Theme(UIContext.ThemeId)가 맡는다.
    [DisallowMultipleComponent]
    public sealed class BattleBackgroundTheme : MonoBehaviour
    {
        [Serializable]
        private struct Look
        {
            // 정적 배경 그림(BG_BattleWhite·BG_BattleBlack).
            public Texture Background;
            // 움직이는 별빛·궤도 머티리얼(M_BattleWhite·M_BattleBlack). Play 중에는 드라이버가 인스턴스를 만들어 쓴다.
            public Material Ambient;
        }

        [SerializeField] private BattleBackgroundDriver _driver;
        [SerializeField] private Look _light;
        [SerializeField] private Look _dark;

        private GameSettings _settings;
        private bool? _shownDark;

        // 설정을 받아 지금 값을 반영하고, 이후 Dark Mode가 바뀌면 따라간다.
        public void Bind(GameSettings settings)
        {
            Unbind();
            _settings = settings;

            if (_settings == null)
                return;

            _settings.Changed += HandleSettingChanged;
            Apply(_settings.IsOn(GameSettings.DarkMode));
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_settings != null)
                _settings.Changed -= HandleSettingChanged;

            _settings = null;
        }

        private void HandleSettingChanged(string id)
        {
            if (id == GameSettings.DarkMode)
                Apply(_settings.IsOn(GameSettings.DarkMode));
        }

        private void Apply(bool dark)
        {
            if (_shownDark == dark)
                return;

            Look look = dark ? _dark : _light;

            if (_driver != null && look.Background != null && look.Ambient != null)
                _driver.SetTheme(look.Background, look.Ambient);
            else
                Debug.LogWarning($"[{nameof(BattleBackgroundTheme)}] {(dark ? "Dark" : "Light")} 배경의 드라이버·그림·머티리얼 중 빠진 것이 있어 배경을 바꾸지 못한다.", this);

            _shownDark = dark;
        }
    }
}
