#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 프로필 창(BalanceProfileWindow)의 고치기 규칙. 창은 그리기만 하고, 값 고치기·묶음 배율·이름·저장 형식은 여기서 정한다.
    // - 노드 효과 칸 하나 = 경로 node/<노드 ID>/<Rank>/<StatId> 하나(BalanceProfilePatcher와 같은 문법).
    // - 원본과 같은 값으로 되돌리면 패치를 지운다. 프로필에는 원본과 다른 값만 남는다.
    // - 묶음 배율은 원본 × 배율이다(여러 번 눌러도 곱이 쌓이지 않는다). Int 수치는 반올림한다.
    // - 메모가 이미 있는 프로필의 값을 바꾸면 새 이름(…-v2)으로 저장한다. 같은 이름의 메모가 서로 다른 값으로 플레이한 기록이 되지 않게.
    internal static class BalanceProfileEdit
    {
        // 새 이름 뒤에 붙는 판 번호: golden-x5 → golden-x5-v2 → golden-x5-v3.
        private static readonly Regex VersionSuffix = new Regex(@"^(.*)-v(\d+)$");

        // 노드 효과 칸 하나.
        internal sealed class EffectRow
        {
            public string Path;
            public string NodeId;
            public int Rank;
            public int MaxRank;
            public string StatId;
            public double Original;
            public bool IsInt;
            public bool Percent;
        }

        public static string EffectPath(string nodeId, int rank, string statId) =>
            $"node/{nodeId}/{rank.ToString(CultureInfo.InvariantCulture)}/{statId}";

        // 노드 효과 칸 전부. 수치 시트 순 → 노드 시트 순 → Rank 순. 같은 경로가 두 번 있으면 처음 것(패처가 고치는 행)만.
        public static List<EffectRow> EffectRows(NodeContentData nodes)
        {
            var statOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            var stats = new Dictionary<string, UpgradeStatRowData>(StringComparer.Ordinal);
            foreach (UpgradeStatRowData stat in nodes.Stats)
            {
                if (stat?.StatId != null && !stats.ContainsKey(stat.StatId))
                {
                    statOrder.Add(stat.StatId, statOrder.Count);
                    stats.Add(stat.StatId, stat);
                }
            }

            var nodeOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            var rankCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (NodeRowData node in nodes.Nodes)
            {
                if (node?.NodeId != null && !nodeOrder.ContainsKey(node.NodeId))
                {
                    nodeOrder.Add(node.NodeId, nodeOrder.Count);
                    rankCounts.Add(node.NodeId, node.RankCount);
                }
            }

            var rows = new List<EffectRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (NodeEffectRowData effect in nodes.Effects)
            {
                if (effect?.NodeId == null || effect.StatId == null)
                    continue;

                string path = EffectPath(effect.NodeId, effect.Rank, effect.StatId);
                if (!seen.Add(path))
                    continue;

                stats.TryGetValue(effect.StatId, out UpgradeStatRowData stat);
                rows.Add(new EffectRow
                {
                    Path = path,
                    NodeId = effect.NodeId,
                    Rank = effect.Rank,
                    MaxRank = rankCounts.TryGetValue(effect.NodeId, out int count) ? count : effect.Rank,
                    StatId = effect.StatId,
                    Original = BalanceProfilePatcher.Decimal(effect.Value),
                    IsInt = stat != null && stat.ValueType == "Int",
                    Percent = stat != null && stat.Unit == "Percent",
                });
            }

            int Order(Dictionary<string, int> order, string key) => order.TryGetValue(key, out int index) ? index : int.MaxValue;

            rows.Sort((a, b) =>
            {
                int byStat = Order(statOrder, a.StatId).CompareTo(Order(statOrder, b.StatId));
                if (byStat != 0)
                    return byStat;
                int byNode = Order(nodeOrder, a.NodeId).CompareTo(Order(nodeOrder, b.NodeId));
                if (byNode != 0)
                    return byNode;
                int byId = string.CompareOrdinal(a.NodeId, b.NodeId);
                return byId != 0 ? byId : a.Rank.CompareTo(b.Rank);
            });

            return rows;
        }

        public static BalanceProfile.Patch Find(BalanceProfile profile, string path) =>
            profile?.patches?.Find(patch => patch != null && patch.path == path);

        // 칸의 지금 값: 프로필에 있으면 그 값, 없으면 원본.
        public static double ValueOf(BalanceProfile profile, string path, double original) =>
            Find(profile, path) is BalanceProfile.Patch patch ? patch.value : original;

        // 칸 하나를 value로. 원본과 같으면 패치를 지운다. 프로필이 바뀌었으면 true.
        public static bool Set(BalanceProfile profile, string path, double value, double original)
        {
            BalanceProfile.Patch patch = Find(profile, path);

            if (Same(value, original))
            {
                if (patch == null)
                    return false;

                profile.patches.Remove(patch);
                return true;
            }

            if (patch != null)
            {
                if (Same(patch.value, value))
                    return false;

                patch.value = value;
                return true;
            }

            profile.patches.Add(new BalanceProfile.Patch { path = path, value = value });
            return true;
        }

        // 묶음 배율: 원본 × factor. Int 수치는 정수로, 실수는 소수 넷째 자리까지.
        public static double Scaled(double original, double factor, bool isInt)
        {
            double value = original * factor;
            return isInt
                ? Math.Round(value, MidpointRounding.AwayFromZero)
                : Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        public static bool Same(double a, double b) =>
            Math.Abs(a - b) <= 1e-9 * Math.Max(1d, Math.Max(Math.Abs(a), Math.Abs(b)));

        // 플레이에 영향을 주는 내용(경로·값)만 모은 열쇠. 설명·이유·근거 메모만 고쳤으면 같다.
        public static string ValuesKey(BalanceProfile profile)
        {
            var lines = new List<string>();
            if (profile?.patches != null)
            {
                foreach (BalanceProfile.Patch patch in profile.patches)
                {
                    if (patch?.path != null)
                        lines.Add(patch.path + "=" + patch.value.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        // 이번에 값이 바뀐 패치(불러온 프로필과 값이 다르거나 새로 생긴 것)에 이유와 근거 메모를 단다.
        // 이유가 비어 있으면 원래 이유를 그대로 둔다. 단 패치 수를 돌려준다.
        public static int Annotate(BalanceProfile profile, BalanceProfile loaded, string reason, IReadOnlyList<string> memos)
        {
            int count = 0;
            string trimmed = reason?.Trim();

            foreach (BalanceProfile.Patch patch in profile.patches)
            {
                BalanceProfile.Patch before = Find(loaded, patch.path);
                if (before != null && Same(before.value, patch.value))
                    continue;

                count++;

                if (!string.IsNullOrEmpty(trimmed))
                    patch.reason = trimmed;

                if (memos == null)
                    continue;

                patch.memos ??= new List<string>();
                foreach (string memo in memos)
                {
                    if (!string.IsNullOrEmpty(memo) && !patch.memos.Contains(memo))
                        patch.memos.Add(memo);
                }
            }

            return count;
        }

        // 저장할 이름. 메모가 이미 있는 프로필을 같은 이름으로 저장하면서 값이 바뀌었으면 새 이름을 고른다.
        // renamed: 새 이름을 골랐다.
        public static string SaveName(string name, string loadedName, bool valuesChanged, int memoCount, ICollection<string> taken, out bool renamed)
        {
            renamed = valuesChanged && memoCount > 0 && !string.IsNullOrEmpty(loadedName) && name == loadedName;
            return renamed ? NextVersion(name, taken) : name;
        }

        // 다음 판 이름: …-vN이면 N+1, 아니면 -v2. 이미 쓰인 이름은 건너뛴다. 이름 길이 제한을 넘으면 앞부분을 줄인다.
        public static string NextVersion(string name, ICollection<string> taken)
        {
            Match match = VersionSuffix.Match(name ?? string.Empty);
            string stem = match.Success ? match.Groups[1].Value : name ?? string.Empty;
            int number = match.Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) + 1 : 2;

            while (true)
            {
                string suffix = "-v" + number.ToString(CultureInfo.InvariantCulture);
                string head = stem.Length + suffix.Length > BalanceProfile.MaxNameLength
                    ? stem.Substring(0, Math.Max(0, BalanceProfile.MaxNameLength - suffix.Length))
                    : stem;
                string candidate = head + suffix;

                if (taken == null || !taken.Contains(candidate))
                    return candidate;

                number++;
            }
        }

        // 깊은 복사(불러온 상태를 따로 들고 있으려고).
        public static BalanceProfile Clone(BalanceProfile profile)
        {
            var copy = new BalanceProfile { name = profile?.name, note = profile?.note };
            if (profile?.patches == null)
                return copy;

            foreach (BalanceProfile.Patch patch in profile.patches)
            {
                if (patch == null)
                    continue;

                copy.patches.Add(new BalanceProfile.Patch
                {
                    path = patch.path,
                    value = patch.value,
                    reason = patch.reason,
                    memos = patch.memos != null ? new List<string>(patch.memos) : new List<string>(),
                });
            }

            return copy;
        }

        // 저장 형식: 손으로 쓴 프로필과 같은 모양(빈 설명·이유·메모는 쓰지 않는다). 패치 순서는 그대로(새 패치는 끝에).
        public static JsonObject ToJson(BalanceProfile profile)
        {
            var patches = new List<object>();
            foreach (BalanceProfile.Patch patch in profile.patches)
            {
                if (patch == null)
                    continue;

                var item = new JsonObject { { "path", patch.path }, { "value", patch.value } };
                if (!string.IsNullOrWhiteSpace(patch.reason))
                    item.Add("reason", patch.reason.Trim());
                if (patch.memos != null && patch.memos.Count > 0)
                    item.Add("memos", new List<object>(patch.memos));
                patches.Add(item);
            }

            var json = new JsonObject { { "name", profile.name } };
            if (!string.IsNullOrWhiteSpace(profile.note))
                json.Add("note", profile.note.Trim());
            json.Add("patches", patches);
            return json;
        }

        // 수치 표시: 정수는 그대로, 실수는 소수 넷째 자리까지, 퍼센트 수치는 %를 붙인다.
        public static string Format(double value, bool percent) =>
            value.ToString("0.####", CultureInfo.InvariantCulture) + (percent ? "%" : string.Empty);

        // 원본 대비 차이: "+5", "-15%". 같으면 빈 글자.
        public static string Delta(double value, double original, bool percent)
        {
            if (Same(value, original))
                return string.Empty;

            double delta = Math.Round(value - original, 4, MidpointRounding.AwayFromZero);
            return (delta > 0 ? "+" : string.Empty) + Format(delta, percent);
        }
    }
}
#endif
