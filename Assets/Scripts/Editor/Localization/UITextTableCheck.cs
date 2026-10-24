using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // UI 글자 표(UIText.csv)를 검사한다. 표를 바꾸지 않고 결과를 콘솔에 남긴다.
    // - 열: key 열과 모든 UILocale 열(UILocale.ToId())이 있는가
    // - 키: 비었거나 두 번 나오는가
    // - 칸: 언어 칸이 비었는가(비면 실행 중에는 영어로 보인다)
    // - 화면 키({화면 타입 이름}.{Ref 이름}): 그 화면 타입이 있고, 그 Refs에 그 이름이 있는가
    // - 코드 키(UIText의 상수): 표에 있는가. 번호·ID가 들어가는 키(UIText의 함수)는 검사하지 않는다
    // - {0} 자리: 언어마다 같은 번호를 쓰는가
    internal static class UITextTableCheck
    {
        private const string TablePath = "Assets/Data/Localization/UIText.csv";
        private const string KeyColumn = "key";
        private const string CommentPrefix = "#";
        private const UILocale BaseLocale = UILocale.English;

        private static readonly Regex Placeholder = new(@"\{(\d+)[^{}]*\}");

        [MenuItem("BlackHole/Localization/Check UI Text Table")]
        private static void Check()
        {
            var table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            if (table == null)
            {
                Debug.LogError($"[글자 검사] {TablePath}가 없다.");
                return;
            }

            List<string> problems = Inspect(table.text, ViewRefs(), CodeKeys());

            if (problems.Count == 0)
                Debug.Log($"[글자 검사] {TablePath}: 문제 없음.", table);
            else
                Debug.LogWarning($"[글자 검사] {TablePath}: {problems.Count}건\n- " + string.Join("\n- ", problems), table);
        }

        // refsByView: 화면 타입 이름 → 그 Refs 이름들. codeKeys: 코드가 쓰는 고정 키(UIText 상수). null이면 검사하지 않는다.
        internal static List<string> Inspect(
            string text, IReadOnlyDictionary<string, HashSet<string>> refsByView, IReadOnlyCollection<string> codeKeys = null)
        {
            var problems = new List<string>();
            List<string[]> rows = Csv.Parse(text ?? string.Empty);

            if (rows.Count == 0)
            {
                problems.Add("표가 비어 있다.");
                return problems;
            }

            string[] header = rows[0];
            int keyColumn = ColumnOf(header, KeyColumn);

            if (keyColumn < 0)
            {
                problems.Add($"첫 줄에 '{KeyColumn}' 열이 없다.");
                return problems;
            }

            var columns = new List<(UILocale Locale, int Column)>();
            foreach (UILocale locale in (UILocale[])Enum.GetValues(typeof(UILocale)))
            {
                int column = ColumnOf(header, locale.ToId());

                if (column < 0)
                    problems.Add($"첫 줄에 '{locale.ToId()}' 열이 없다.");
                else
                    columns.Add((locale, column));
            }

            var firstLine = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 1; i < rows.Count; i++)
            {
                string[] cells = rows[i];
                int line = i + 1;
                string key = CellOf(cells, keyColumn).Trim();

                if (key.StartsWith(CommentPrefix, StringComparison.Ordinal))
                    continue;

                if (key.Length == 0)
                {
                    if (Array.Exists(cells, cell => !string.IsNullOrWhiteSpace(cell)))
                        problems.Add($"{line}번째 줄: 키가 비어 있다.");

                    continue;
                }

                if (firstLine.TryGetValue(key, out int first))
                {
                    problems.Add($"{line}번째 줄: '{key}'가 {first}번째 줄에도 있다.");
                    continue;
                }

                firstLine.Add(key, line);

                foreach ((UILocale locale, int column) in columns)
                {
                    if (string.IsNullOrWhiteSpace(CellOf(cells, column)))
                        problems.Add($"'{key}': {locale.ToId()} 칸이 비어 있다.");
                }

                CheckViewKey(key, refsByView, problems);
                CheckPlaceholders(key, cells, columns, problems);
            }

            if (codeKeys != null)
            {
                foreach (string key in codeKeys)
                {
                    if (!firstLine.ContainsKey(key))
                        problems.Add($"코드 키 '{key}'(UIText)가 표에 없다.");
                }
            }

            return problems;
        }

        // UIText와 그 안의 중첩 클래스에 있는 public const string 전부.
        internal static List<string> CodeKeys()
        {
            var keys = new List<string>();
            Collect(typeof(UIText), keys);
            return keys;
        }

        private static void Collect(Type type, List<string> keys)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                    keys.Add((string)field.GetRawConstantValue());
            }

            foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
                Collect(nested, keys);
        }

        // 대문자로 시작하는 키는 화면 키다(코드 키는 소문자로 시작한다).
        private static void CheckViewKey(string key, IReadOnlyDictionary<string, HashSet<string>> refsByView, List<string> problems)
        {
            if (!char.IsUpper(key[0]))
                return;

            int dot = key.IndexOf('.');
            if (dot <= 0 || dot == key.Length - 1)
            {
                problems.Add($"'{key}': 화면 키는 {{화면 타입 이름}}.{{Ref 이름}}이어야 한다.");
                return;
            }

            string view = key.Substring(0, dot);
            string refId = key.Substring(dot + 1);

            if (!refsByView.TryGetValue(view, out HashSet<string> refs))
                problems.Add($"'{key}': '{view}' 화면 타입이 없다.");
            else if (!refs.Contains(refId))
                problems.Add($"'{key}': {view}.Refs에 '{refId}'가 없다.");
        }

        // 영어 칸의 {n} 번호와 다른 언어 칸의 번호가 같아야 한다(빈 칸은 건너뛴다).
        private static void CheckPlaceholders(string key, string[] cells, List<(UILocale Locale, int Column)> columns, List<string> problems)
        {
            int baseColumn = columns.FindIndex(entry => entry.Locale == BaseLocale);
            if (baseColumn < 0)
                return;

            string baseText = CellOf(cells, columns[baseColumn].Column);
            if (string.IsNullOrWhiteSpace(baseText))
                return;

            SortedSet<int> expected = PlaceholdersOf(baseText);

            foreach ((UILocale locale, int column) in columns)
            {
                string text = CellOf(cells, column);
                if (locale == BaseLocale || string.IsNullOrWhiteSpace(text))
                    continue;

                SortedSet<int> actual = PlaceholdersOf(text);
                if (!actual.SetEquals(expected))
                {
                    problems.Add(
                        $"'{key}': {locale.ToId()}의 자리 {{{string.Join("},{", actual)}}}가 " +
                        $"{BaseLocale.ToId()}의 {{{string.Join("},{", expected)}}}와 다르다.");
                }
            }
        }

        private static SortedSet<int> PlaceholdersOf(string text)
        {
            var numbers = new SortedSet<int>();

            foreach (Match match in Placeholder.Matches(text))
                numbers.Add(int.Parse(match.Groups[1].Value));

            return numbers;
        }

        // 화면(UIBase<TRefs>를 상속한 타입)마다 Refs 이름들.
        private static Dictionary<string, HashSet<string>> ViewRefs()
        {
            var refsByView = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (Type type in TypeCache.GetTypesDerivedFrom<UIBase>())
            {
                if (type.IsAbstract || type.IsGenericType)
                    continue;

                Type refs = RefsTypeOf(type);
                if (refs != null)
                    refsByView[type.Name] = new HashSet<string>(Enum.GetNames(refs), StringComparer.Ordinal);
            }

            return refsByView;
        }

        private static Type RefsTypeOf(Type type)
        {
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(UIBase<>))
                    return current.GetGenericArguments()[0];
            }

            return null;
        }

        private static int ColumnOf(string[] header, string name) =>
            Array.FindIndex(header, cell => string.Equals(cell.Trim(), name, StringComparison.OrdinalIgnoreCase));

        private static string CellOf(string[] cells, int column) =>
            column >= 0 && column < cells.Length ? cells[column] : string.Empty;
    }
}
