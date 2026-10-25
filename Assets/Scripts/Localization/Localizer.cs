using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // UI 글자 번역. UI 글자 표(Assets/Data/Localization/UIText.csv)에서 지금 언어의 글자를 찾는다.
    //
    // 표(CSV)
    // - 첫 줄은 열 이름: key, en-US, ko-KR … key가 아닌 열은 모두 언어 열이다(BCP 47). 그 아래 한 줄에 키 하나.
    // - 키가 #으로 시작하는 줄은 구분용 메모라 읽지 않는다.
    // - 글자 안의 {0}, {1} …은 Format이 채운다. 숫자를 이어 붙이지 말고 문장 전체를 번역한다("Gold {0}").
    //
    // 키 규칙
    // - 화면 프리팹에 적힌 글자: {화면 타입 이름}.{Ref 이름} (예: PausePanel.Title_Text). ViewTexts가 넣는다.
    // - 코드가 쓰는 글자: 소문자로 시작하고 점으로 나눈다(예: setting.DarkMode — 설정은 설정 ID를 그대로 쓴다).
    //
    // 지금 언어 칸이 비었으면 영어 칸을 쓴다. 둘 다 없으면 찾지 못한 것이다(TryGet은 false, Get은 키를 돌려준다).
    // 언어는 열 이름과 같은 글자(ko-KR)로 고른다. 이 게임에서는 GameSettings의 Language가 정한다(Create). 바뀌면 Changed로 알린다 — 글자를 다시 넣는 일은 받는 쪽이 한다.
    // 번역(글자 내용)만 맡는다. 언어별 글꼴·배치·그림은 UIPresentationFlow의 언어 변형이 맡는다.
    // 게임을 모른다(Create 빼고). 표 읽기는 Csv다.
    public sealed class Localizer
    {
        private const string KeyColumn = "key";
        private const string CommentPrefix = "#";
        private const string FallbackLocale = "en-US";

        // 키 → 그 줄의 칸들. 언어별 칸 위치는 _columns(열 이름 → 칸 번호).
        private readonly Dictionary<string, string[]> _rows = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _columns = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

        // 지금 언어(열 이름).
        public string Locale { get; private set; }

        public IReadOnlyCollection<string> Keys => _rows.Keys;

        public event Action Changed;

        public Localizer(string table, string locale)
        {
            Locale = locale ?? FallbackLocale;
            Read(table ?? string.Empty);
        }

        // 표를 읽고, 설정의 언어를 따라가게 한다. 설정과 Localizer는 앱이 끝날 때까지 함께 산다.
        public static Localizer Create(TextAsset table, GameSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (table == null)
                Debug.LogError("[UI] GameBootstrap에 UI 글자 표(UIText.csv)를 연결해야 한다. 지금은 프리팹 글자와 영어 원문으로 보인다.");

            var localizer = new Localizer(table != null ? table.text : null, settings.LanguageLocale.ToId());

            settings.Changed += id =>
            {
                if (id == GameSettings.Language)
                    localizer.SetLocale(settings.LanguageLocale.ToId());
            };

            return localizer;
        }

        public void SetLocale(string locale)
        {
            if (string.Equals(Locale, locale, StringComparison.OrdinalIgnoreCase))
                return;

            Locale = locale;
            Changed?.Invoke();
        }

        public bool TryGet(string key, out string text)
        {
            text = null;

            if (key == null || !_rows.TryGetValue(key, out string[] cells))
                return false;

            if (TryCell(cells, Locale, out text))
                return true;

            if (string.Equals(Locale, FallbackLocale, StringComparison.OrdinalIgnoreCase) || !TryCell(cells, FallbackLocale, out text))
                return false;

            WarnOnce(key, $"[글자] '{key}'의 {Locale} 번역이 비어 있어 {FallbackLocale} 글자를 쓴다.");
            return true;
        }

        public string Get(string key)
        {
            if (TryGet(key, out string text))
                return text;

            WarnOnce(key, $"[글자] UI 글자 표에 '{key}'가 없다.");
            return key;
        }

        // 표의 글자에 값을 채운다({0}, {1} …). 숫자는 미리 글자로 바꿔 넘긴다(골드 표기 등).
        public string Format(string key, params object[] args)
        {
            string template = Get(key);

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException)
            {
                WarnOnce(key, $"[글자] '{key}'의 {Locale} 글자 \"{template}\"의 {{n}} 자리가 넘긴 값과 맞지 않는다.");
                return template;
            }
        }

        private bool TryCell(string[] cells, string locale, out string text)
        {
            text = null;

            if (!_columns.TryGetValue(locale, out int column) || column >= cells.Length)
                return false;

            if (string.IsNullOrWhiteSpace(cells[column]))
                return false;

            text = cells[column];
            return true;
        }

        private void Read(string table)
        {
            List<string[]> rows = Csv.Parse(table);
            if (rows.Count == 0)
                return;

            string[] header = rows[0];
            int keyColumn = Array.FindIndex(header, name => string.Equals(name.Trim(), KeyColumn, StringComparison.OrdinalIgnoreCase));

            if (keyColumn < 0)
            {
                Debug.LogError($"[글자] UI 글자 표의 첫 줄에 '{KeyColumn}' 열이 없다.");
                return;
            }

            // key가 아닌 열은 모두 언어 열이다.
            for (int c = 0; c < header.Length; c++)
            {
                string name = header[c].Trim();

                if (c != keyColumn && name.Length > 0 && !_columns.TryAdd(name, c))
                    Debug.LogWarning($"[글자] UI 글자 표에 '{name}' 열이 두 번 있다. 앞의 것을 쓴다.");
            }

            if (!_columns.ContainsKey(Locale))
                Debug.LogWarning($"[글자] UI 글자 표에 '{Locale}' 열이 없다. 그 언어는 {FallbackLocale}로 보인다.");

            for (int i = 1; i < rows.Count; i++)
            {
                string[] cells = rows[i];
                string key = keyColumn < cells.Length ? cells[keyColumn].Trim() : string.Empty;

                if (key.Length == 0 || key.StartsWith(CommentPrefix, StringComparison.Ordinal))
                    continue;

                // 칸 안의 줄바꿈은 \n 하나로 맞춘다(Windows에서 받으면 \r\n이 될 수 있다).
                for (int c = 0; c < cells.Length; c++)
                    cells[c] = cells[c].Replace("\r\n", "\n");

                if (!_rows.TryAdd(key, cells))
                    Debug.LogWarning($"[글자] UI 글자 표에 '{key}'가 두 번 있다({i + 1}번째 줄). 앞의 것을 쓴다.");
            }
        }

        // 같은 키·언어의 경고는 한 번만 남긴다(글자를 매 프레임 쓰는 화면이 있어도 로그가 쌓이지 않게).
        private void WarnOnce(string key, string message)
        {
            if (_warned.Add(Locale + "|" + key))
                Debug.LogWarning(message);
        }
    }
}
