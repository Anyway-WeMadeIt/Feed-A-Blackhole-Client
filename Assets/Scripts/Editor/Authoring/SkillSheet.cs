using System;
using System.Collections.Generic;
using BlackHole.Core;
using static BlackHole.Authoring.SheetCells;

namespace BlackHole.Authoring
{
    // 스킬 기본 수치(ContentData.Breaker)와 데이터 시트의 Skills 탭 사이의 변환.
    // Skills 탭: key | value | note. 키는 아래 열 개이고 모두 있어야 한다. note 열과 그 뒤 열은 읽지 않는다.
    // Breaker 키 이름은 노드가 올리는 수치 이름(breaker.damage 등)과 같다: 이 탭의 값이 그 수치의 기본값이다.
    // 규칙은 게임과 같은 정의 생성자(BreakerDefinition)로 본다.
    public static class SkillSheet
    {
        public const string Tab = "Skills";

        private sealed class Key
        {
            public string Name;
            public string Parameter;
            public string Note;
            public Func<BreakerData, float> Get;
            public Action<BreakerData, float> Set;
        }

        private static readonly Key[] _keys =
        {
            new Key { Name = "breaker.damage", Parameter = "damage", Note = "Breaker 한 Tick의 피해.",
                Get = b => b.Damage, Set = (b, v) => b.Damage = v },
            new Key { Name = "breaker.interval", Parameter = "interval", Note = "공격 주기(초).",
                Get = b => b.Interval, Set = (b, v) => b.Interval = v },
            new Key { Name = "breaker.radius", Parameter = "radius", Note = "공격 원의 반지름. 화면의 범위 표시도 이 값이다.",
                Get = b => b.Radius, Set = (b, v) => b.Radius = v },
            new Key { Name = "breaker.crit-chance", Parameter = "critChance", Note = "한 Tick이 치명타일 확률(0 ~ 1).",
                Get = b => b.CritChance, Set = (b, v) => b.CritChance = v },
            new Key { Name = "breaker.crit-damage", Parameter = "critDamage", Note = "치명타 피해 보너스(0 이상). 치명타 피해 = 피해 × (1 + 보너스). 1이면 +100%(2배).",
                Get = b => b.CritDamage, Set = (b, v) => b.CritDamage = v },
            new Key { Name = "breaker.moon-duration", Parameter = "moonDuration", Note = "달 중첩 하나의 지속 시간(초).",
                Get = b => b.MoonDuration, Set = (b, v) => b.MoonDuration = v },
            new Key { Name = "breaker.moon-speed-bonus", Parameter = "moonSpeedBonus", Note = "달 중첩 하나의 공격 속도 보너스(0 이상). 0.2면 +20%.",
                Get = b => b.MoonSpeedBonus, Set = (b, v) => b.MoonSpeedBonus = v },
            new Key { Name = "breaker.moon-radius-bonus", Parameter = "moonRadiusBonus", Note = "달 중첩 하나의 공격 범위(반지름) 보너스(0 이상). 0.1이면 +10%.",
                Get = b => b.MoonRadiusBonus, Set = (b, v) => b.MoonRadiusBonus = v },
            new Key { Name = "breaker.comet-duration", Parameter = "cometDuration", Note = "혜성 중첩 하나의 지속 시간(초).",
                Get = b => b.CometDuration, Set = (b, v) => b.CometDuration = v },
            new Key { Name = "breaker.comet-crit-damage-bonus", Parameter = "cometCritDamageBonus", Note = "혜성 중첩 하나의 치명타 피해 보너스 증가(0 이상). 0.5면 +50%.",
                Get = b => b.CometCritDamageBonus, Set = (b, v) => b.CometCritDamageBonus = v },
        };

        public static string SkillsCsv(BreakerData breaker)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "key", "value", "note" } };

            foreach (Key key in _keys)
                rows.Add(new[] { key.Name, Number(key.Get(breaker)), key.Note });

            return Csv.Write(rows);
        }

        // 탭을 읽어 통과하면 into.Breaker를 바꾼다. 반환: 진단(없으면 통과).
        public static List<ContentDiagnostic> Read(string csv, ContentData into)
        {
            var diagnostics = new List<ContentDiagnostic>();
            List<string[]> rows = Csv.Parse(csv ?? string.Empty);
            var names = new List<string>();

            foreach (Key key in _keys)
                names.Add(key.Name);

            Dictionary<string, int> keyRows = KeyRows(rows, Tab, names, diagnostics);

            if (diagnostics.Count > 0)
                return diagnostics;

            var breaker = new BreakerData();

            foreach (Key key in _keys)
            {
                int sheetRow = keyRows[key.Name];
                key.Set(breaker, ReadFloat(rows[sheetRow - 1], 1, Tab, sheetRow, diagnostics));
            }

            if (diagnostics.Count > 0)
                return diagnostics;

            try { new BreakerDefinition(breaker.Damage, breaker.Interval, breaker.Radius, breaker.CritChance, breaker.CritDamage,
                breaker.MoonDuration, breaker.MoonSpeedBonus, breaker.MoonRadiusBonus, breaker.CometDuration, breaker.CometCritDamageBonus,
                breaker.PlanetDamageBonus, breaker.StarDamageBonus); }
            catch (ArgumentException error) { diagnostics.Add(RuleAt("breaker.", error, keyRows)); }

            if (diagnostics.Count == 0)
            {
                into.Breaker = breaker;
            }

            return diagnostics;
        }

        // 생성자가 알린 매개변수로 그 키의 값 칸을 찾는다.
        private static ContentDiagnostic RuleAt(string skill, ArgumentException error, Dictionary<string, int> keyRows)
        {
            foreach (Key key in _keys)
            {
                if (key.Name.StartsWith(skill, StringComparison.Ordinal) && key.Parameter == error.ParamName)
                    return new ContentDiagnostic(Cell(Tab, 1, keyRows[key.Name]), RuleMessage(error));
            }

            return new ContentDiagnostic(Tab, RuleMessage(error));
        }
    }
}
