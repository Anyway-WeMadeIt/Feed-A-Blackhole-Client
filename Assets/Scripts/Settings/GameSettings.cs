using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BlackHole.Unity
{
    // 플레이어 설정. 판 규칙과 관계없는 이 기기의 설정이라 Core가 아니라 여기에 두고, PlayerPrefs에 저장한다.
    // 설정마다 정의(Definition: ID·이름·고치는 방법·기본값·선택지)가 있고, 값은 종류와 관계없이 수 하나로 둔다:
    // 켜기/끄기는 0·1, 막대는 0 ~ 1, 선택지는 번호.
    //
    // GameSettings는 값을 저장만 한다 — 그 기능(소리, 화면 흔들림 등)이 IsOn·LevelOf·IndexOf·PercentOf로 값을 읽고,
    // 바뀜은 Changed로 받는다. 모바일 게임이라 PC 전용 설정(화면 모드·수직 동기화·컨트롤러 감도)은 두지 않는다.
    public sealed class GameSettings
    {
        // 설정을 고치는 방법(설정 창의 행 모양).
        public enum Kind
        {
            Toggle,     // 켜기/끄기
            Slider,     // 0 ~ 1 막대
            Choice,     // 선택지 중 하나(펼침 목록)
            Stepper,    // 선택지 중 하나(◄ ►로 한 칸씩)
        }

        public sealed class Definition
        {
            public string Id { get; }
            public string Label { get; }
            public Kind Kind { get; }
            // 기본값. 켜기/끄기는 0·1, 막대는 0 ~ 1, 선택지는 번호.
            public float Default { get; }
            // 선택지(Choice·Stepper). 다른 종류는 비어 있다.
            public IReadOnlyList<string> Options { get; }

            public Definition(string id, string label, Kind kind, float defaultValue, IReadOnlyList<string> options)
            {
                Id = id;
                Label = label;
                Kind = kind;
                Default = defaultValue;
                Options = options;
            }
        }

        // 설정 ID. PlayerPrefs 키이기도 하다 — 바꾸면 저장된 값을 잃는다.
        public const string DarkMode = "DarkMode";
        public const string MasterVolume = "MasterVolume";
        public const string EffectsVolume = "EffectsVolume";
        public const string MusicVolume = "MusicVolume";
        public const string ShuffleMusic = "ShuffleMusic";
        public const string Language = "Language";
        public const string FpsLimit = "FpsLimit";
        public const string TextSize = "TextSize";
        public const string ShowMoneyText = "ShowMoneyText";
        public const string ShowDamage = "ShowDamage";
        public const string ScreenShake = "ScreenShake";
        public const string BlackHolePulse = "BlackHolePulse";
        public const string MatterParticles = "MatterParticles";
        public const string NumberFormat = "NumberFormat";
        public const string EnemyOrbit = "EnemyOrbit";

        private const string KeyPrefix = "Settings.";

        // 선택지는 번호로 저장한다. 순서를 바꾸면 저장된 값의 뜻이 바뀌므로 끝에만 더한다.
        private static readonly string[] NoOptions = Array.Empty<string>();
        // 언어 이름은 번역하지 않고 그 언어로 쓴다(어느 언어에서도 자기 언어를 찾을 수 있게).
        private static readonly string[] LanguageOptions = { "English", "한국어" };
        private static readonly UILocale[] LanguageValues = { UILocale.English, UILocale.Korean };
        // 프레임 제한. 앞 세 개(30·60·120)는 예전 선택지와 번호가 같다 — 예전에 144 이상을 고른 값은 120으로 읽힌다.
        private static readonly string[] FpsLimitOptions = { "30", "60", "120" };
        private static readonly int[] FpsLimitValues = { 30, 60, 120 };
        private const int DefaultFpsLimitIndex = 1;
        // 퍼센트 단계(글자 크기): 50% ~ 150%, 10%씩. 100%가 기본(원작과 같은 범위). 예전에 160% 이상을 고른 값은 150%로 읽힌다.
        private const int PercentMin = 50;
        private const int PercentMax = 150;
        private const int PercentStep = 10;
        private static readonly string[] PercentOptions = Percents();
        private const int DefaultPercentIndex = (100 - PercentMin) / PercentStep;
        // Gold 숫자 표기. 번호는 NumberFormat 열거형의 순서와 같다(NumberText).
        private static readonly string[] NumberFormatOptions = { "Short (1.2M)", "Scientific (1.2e6)" };

        // 설정 창에 보이는 순서. 이름과 선택지는 영어 원문이다 — 설정 창은 UI 글자 표(setting.{ID}, setting.{ID}.{번호})의
        // 번역을 먼저 쓰고, 표에 없으면 이 원문을 쓴다(숫자·퍼센트 선택지는 표에 넣지 않는다).
        public static IReadOnlyList<Definition> Definitions { get; } = new[]
        {
            Toggle(DarkMode, "Dark Mode", true),
            Slider(MasterVolume, "Master Volume", 0.5f),
            Slider(EffectsVolume, "Effects Volume", 0.5f),
            Slider(MusicVolume, "Music Volume", 0.25f),
            Toggle(ShuffleMusic, "Shuffle Music", false),
            Options(Kind.Choice, Language, "Language", LanguageOptions, 0),
            Options(Kind.Choice, FpsLimit, "FPS Limit", FpsLimitOptions, DefaultFpsLimitIndex),
            Options(Kind.Stepper, TextSize, "Text Size", PercentOptions, DefaultPercentIndex),
            Toggle(ShowMoneyText, "Show Money Text", true),
            Toggle(ShowDamage, "Show Damage", true),
            Toggle(ScreenShake, "Screen Shake", true),
            Toggle(BlackHolePulse, "Black Hole Pulse", true),
            Toggle(MatterParticles, "Matter Particles", true),
            Options(Kind.Choice, NumberFormat, "Number Format", NumberFormatOptions, 0),
            // 적이 블랙홀을 중심으로 공전하는가(원작 "Object Rotation"). 끄면 적이 제자리에 머문다(중력장의 끌림은 그대로).
            Toggle(EnemyOrbit, "Object Rotation", true),
        };

        private static readonly Dictionary<string, Definition> ById = IndexById();

        private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);

        // 값이 바뀐 설정의 ID. 설정 창에서 고칠 때마다 온다.
        public event Action<string> Changed;

        private GameSettings() { }

        // 저장된 값을 읽는다. 없으면 기본값.
        public static GameSettings Load()
        {
            var settings = new GameSettings();

            foreach (Definition definition in Definitions)
                settings._values[definition.Id] = Normalize(definition, PlayerPrefs.GetFloat(KeyPrefix + definition.Id, definition.Default));

            return settings;
        }

        public float ValueOf(string id) => _values[Require(id).Id];
        public bool IsOn(string id) => ValueOf(id) >= 0.5f;
        public float LevelOf(string id) => ValueOf(id);
        public int IndexOf(string id) => Mathf.RoundToInt(ValueOf(id));

        // 고른 언어(Language 선택지 → UI 언어).
        public UILocale LanguageLocale => LanguageValues[IndexOf(Language)];

        // 고른 프레임 제한(초당 프레임 수).
        public int FrameRateLimit => FpsLimitValues[IndexOf(FpsLimit)];

        // 퍼센트 단계(Text Size)의 배율. 100%면 1이다.
        public float PercentOf(string id) => (PercentMin + IndexOf(id) * PercentStep) / 100f;

        // 글자 크기 배율(Text Size). 100%면 1이다.
        public float TextScale => PercentOf(TextSize);

        // 고른 Gold 숫자 표기.
        public BlackHole.Systems.Numbers.NumberFormat NumberFormatValue => (BlackHole.Systems.Numbers.NumberFormat)IndexOf(NumberFormat);

        public void SetOn(string id, bool on) => Set(id, on ? 1 : 0);
        public void SetLevel(string id, float level) => Set(id, level);
        public void SetIndex(string id, int index) => Set(id, index);

        // PlayerPrefs를 디스크에 쓴다. 설정 창을 닫을 때 부른다(앱을 끌 때는 Unity가 쓴다).
        public void Save() => PlayerPrefs.Save();

        // 종류에 맞게 값을 고른 뒤 저장하고 알린다.
        private void Set(string id, float value)
        {
            Definition definition = Require(id);
            value = Normalize(definition, value);

            if (_values[id] == value)
                return;

            _values[id] = value;
            PlayerPrefs.SetFloat(KeyPrefix + id, value);

            Changed?.Invoke(id);
        }

        private static float Normalize(Definition definition, float value)
        {
            switch (definition.Kind)
            {
                case Kind.Toggle: return value >= 0.5f ? 1 : 0;
                case Kind.Slider: return Mathf.Clamp01(value);
                default: return Mathf.Clamp(Mathf.Round(value), 0, definition.Options.Count - 1);
            }
        }

        private static Definition Require(string id)
        {
            if (id != null && ById.TryGetValue(id, out Definition definition))
                return definition;

            throw new ArgumentException($"알 수 없는 설정 ID '{id}'.", nameof(id));
        }

        private static Definition Toggle(string id, string label, bool on) =>
            new Definition(id, label, Kind.Toggle, on ? 1 : 0, NoOptions);

        private static Definition Slider(string id, string label, float level) =>
            new Definition(id, label, Kind.Slider, level, NoOptions);

        private static Definition Options(Kind kind, string id, string label, string[] options, int index) =>
            new Definition(id, label, kind, index, options);

        private static string[] Percents()
        {
            var options = new string[(PercentMax - PercentMin) / PercentStep + 1];

            for (int i = 0; i < options.Length; i++)
                options[i] = (PercentMin + i * PercentStep).ToString(CultureInfo.InvariantCulture) + "%";

            return options;
        }

        private static Dictionary<string, Definition> IndexById()
        {
            var byId = new Dictionary<string, Definition>(StringComparer.Ordinal);

            foreach (Definition definition in Definitions)
                byId.Add(definition.Id, definition);

            return byId;
        }
    }
}
