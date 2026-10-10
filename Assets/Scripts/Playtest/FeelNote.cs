#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BlackHole.Analytics;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 느낌 메모 한 줄(schema 1, M3). 적는 순간의 세팅·수치 지문·판 상태와 느낌을 함께 담는다.
    // 메모 하나 = 파일 하나(PlaytestNotes: notes/<저장 이름>.json). 필드 설명은 Docs/Archive/BalanceLoop/M3-feel-notes.md.
    // - setup: 그 판(또는 창의 세팅)이 시작한 상태. 노드는 실제로 적용된 Rank다(콘텐츠에 없는 노드는 빠진다). 노드마다 그 Rank까지의 효과도 적는다(effect: "breaker.radius +75%").
    // - stats: 그 판에 실제로 쓰인 계산된 수치(NoteStats). 노드 ID를 시트와 맞춰 보지 않아도 읽힌다.
    // - battle: 플레이 중에 적었을 때만. 에디터에서 세팅만 보고 적으면 없다(null).
    internal sealed class FeelNote
    {
        public const int Schema = 1;
        public const int DifficultyMin = -2;
        public const int DifficultyMax = 2;
        public const int FunMin = 1;
        public const int FunMax = 5;

        public string Id;
        // 저장 이름 = 파일 이름: 시나리오_프로필_번호(예: qa-nodes-3_golden-x5_004). 같은 시나리오·프로필에 쌓일 때마다 번호가 오른다(PlaytestNotes가 채운다).
        public string Name;
        public DateTime AtUtc;
        // 어디서 적었나: "notes"(플레이 메모 창) | "panel"(개발 패널) | "window"(예전: 테스트 세팅 창)
        public string Source;
        public string BuildVersion;

        public string SetupName;
        public int GrowthStage;
        public int StartLevel;
        public int Seed;
        public long Gold;
        public List<(string NodeId, int Rank)> Nodes = new List<(string NodeId, int Rank)>();
        // 게임 순서로 만들 수 있는 세팅인가. 모르면 null.
        public bool? ReachableInGame;
        // 노드마다 산 Rank까지 더한 효과(예: "breaker.radius +75%"). 노드 ID만으로는 시트를 봐야 읽히므로 함께 적는다(DescribeNodes).
        public readonly Dictionary<string, string> NodeEffects = new Dictionary<string, string>();

        public string Profile;
        public string Fingerprint;
        public string BaseFingerprint;

        // 계산된 수치(NoteStats): 나(블랙홀·Breaker)와 적(종류별 수·HP·Gold·EXP·성질), 바뀐 업그레이드 수치.
        public JsonObject Stats;

        public HudSnapshot Battle;
        public string BattleId;
        public bool Cheated;

        public int? Difficulty;
        public int? Fun;
        public List<string> Tags = new List<string>();
        // 느낌(무엇이 마음에 안 드나)과 고칠 방향(어떻게 고치면 좋겠나, JSON 키 intent).
        public string Text;
        public string Intent;

        public string SetupKey => FeelNotes.SetupKeyOf(GrowthStage, StartLevel, Nodes);

        // NodeEffects를 채운다: 노드마다 1 ~ 산 Rank의 효과를 수치(StatId)별로 더한다. 트리에 없는 노드는 건너뛴다.
        public void DescribeNodes(NodeTree tree)
        {
            NodeEffects.Clear();

            if (tree == null)
                return;

            foreach ((string nodeId, int rank) in Nodes)
            {
                if (!tree.TryGet(nodeId, out NodeDefinition node))
                    continue;

                var sums = new List<(UpgradeStat Stat, float Value)>();
                for (int r = 1; r <= rank && r <= node.MaxRank; r++)
                {
                    foreach (NodeEffect effect in node.RankAt(r).Effects)
                    {
                        int index = sums.FindIndex(sum => sum.Stat == effect.Stat);
                        if (index < 0)
                            sums.Add((effect.Stat, effect.Value));
                        else
                            sums[index] = (effect.Stat, sums[index].Value + effect.Value);
                    }
                }

                var parts = new List<string>(sums.Count);
                foreach ((UpgradeStat stat, float value) in sums)
                {
                    UpgradeStatDefinition definition = tree.Content.StatOf(stat);
                    string unit = definition.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                    string sign = value >= 0 ? "+" : string.Empty;
                    parts.Add($"{definition.StatId} {sign}{value.ToString("0.###", CultureInfo.InvariantCulture)}{unit}");
                }

                NodeEffects[nodeId] = string.Join(", ", parts);
            }
        }

        public static string NewId(DateTime atUtc, Random random) =>
            $"n-{atUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{random.Next(0x10000):x4}";

        public JsonObject ToJson()
        {
            var nodes = new List<object>(Nodes.Count);
            foreach ((string nodeId, int rank) in Nodes)
            {
                var node = new JsonObject { { "nodeId", nodeId }, { "rank", rank } };
                if (NodeEffects.TryGetValue(nodeId, out string effect))
                    node.Add("effect", effect);
                nodes.Add(node);
            }

            var tags = new List<object>(Tags);

            return new JsonObject
            {
                { "schema", Schema },
                { "id", Id },
                { "name", Name },
                { "atUtc", AtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
                { "source", Source },
                { "build", BuildVersion },
                { "setupKey", SetupKey },
                {
                    "setup", new JsonObject
                    {
                        { "name", SetupName },
                        { "growthStage", GrowthStage },
                        { "startLevel", StartLevel },
                        { "seed", Seed },
                        { "gold", Gold },
                        { "nodes", nodes },
                        { "reachableInGame", ReachableInGame },
                    }
                },
                {
                    "content", new JsonObject
                    {
                        { "profile", Profile ?? string.Empty },
                        { "fingerprint", Fingerprint },
                        { "baseFingerprint", BaseFingerprint },
                    }
                },
                { "stats", Stats },
                { "battle", Battle != null ? BattleJson() : null },
                { "difficulty", Difficulty },
                { "fun", Fun },
                { "tags", tags },
                { "text", Text ?? string.Empty },
                { "intent", Intent ?? string.Empty },
            };
        }

        private JsonObject BattleJson()
        {
            var alive = new JsonObject();
            foreach ((var type, int count) in Battle.Alive)
                alive.Add(ContractIds.Of(type.ToString()), count);

            return new JsonObject
            {
                { "battleId", BattleId },
                { "elapsed", Round(Battle.Elapsed) },
                { "remaining", Round(Battle.Remaining) },
                { "stage", Battle.Stage },
                { "level", Battle.Level },
                { "goalLevel", Battle.GoalLevel },
                { "exp", Battle.Exp },
                { "kills", Battle.Kills },
                { "gold", Battle.Gold },
                { "damage", Round(Battle.Damage) },
                {
                    "rates5s", Battle.RateSpan > 0
                        ? new JsonObject
                        {
                            { "span", Round(Battle.RateSpan) },
                            { "kills", Round(Battle.KillsPerSecond) },
                            { "gold", Round(Battle.GoldPerSecond) },
                            { "exp", Round(Battle.ExpPerSecond) },
                            { "damage", Round(Battle.DamagePerSecond) },
                        }
                        : null
                },
                { "alive", alive },
                { "cheated", Cheated },
            };
        }

        private static double Round(double value) => Math.Round(value, 2);
    }

    // 읽은 메모(창의 목록에 필요한 만큼).
    internal sealed class FeelNoteView
    {
        public string Id;
        // 저장 이름(파일 이름).
        public string Name;
        public string AtUtc;
        public string SetupKey;
        public string SetupName;
        public string Profile;
        public string Fingerprint;
        public int? Difficulty;
        public int? Fun;
        public List<string> Tags = new List<string>();
        public string Text;
        public string Intent;
        public bool HasBattle;
        public int? BattleLevel;
        public double? BattleElapsed;
    }

    internal static class FeelNotes
    {
        // 프로필 없이(원본 수치로) 적은 메모의 프로필 표시.
        public const string BaseProfileLabel = "원본";

        // 저장 이름 앞부분: 시나리오(세팅 이름)_프로필. 파일 이름이 되므로 쓸 수 없는 글자는 -로 바꾼다.
        public static string NamePrefix(string setupName, string profile) =>
            $"{FileSafe(string.IsNullOrEmpty(setupName) ? "이름 없음" : setupName)}_{FileSafe(string.IsNullOrEmpty(profile) ? BaseProfileLabel : profile)}";

        // Windows·Android 어디서나 파일 이름에 쓸 수 있게(<>:"/\|?* 와 제어 문자 → -, 끝의 점·공백 제거).
        private static string FileSafe(string text)
        {
            var safe = new StringBuilder(text.Length);
            foreach (char c in text)
                safe.Append(c < 32 || "<>:\"/\\|?*".IndexOf(c) >= 0 ? '-' : c);

            string result = safe.ToString().TrimEnd('.', ' ');
            return result.Length > 0 ? result : "-";
        }

        // 같은 시나리오·프로필로 적은 메모인가(이름이 없는 예전 메모도 세팅 이름·프로필로 본다).
        public static bool SameGroup(FeelNoteView note, string setupName, string profile) =>
            (note.SetupName ?? string.Empty) == (setupName ?? string.Empty) && (note.Profile ?? string.Empty) == (profile ?? string.Empty);

        // 세팅 키: 이정표 단계·시작 Level·노드(id:rank, 이름 순)의 SHA-1 앞 8자리. 이름이 달라도 같은 상태면 같은 키다.
        public static string SetupKeyOf(int growthStage, int startLevel, IEnumerable<(string NodeId, int Rank)> nodes)
        {
            var list = new List<string>();
            foreach ((string nodeId, int rank) in nodes)
            {
                if (rank > 0 && !string.IsNullOrEmpty(nodeId))
                    list.Add($"{nodeId}:{rank}");
            }

            list.Sort(StringComparer.Ordinal);
            string canonical = $"stage={growthStage};level={startLevel};nodes={string.Join(",", list)}";

            using SHA1 sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(8);
            for (int i = 0; i < 4; i++)
                hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

            return hex.ToString();
        }

        // 난이도 −2 ~ +2의 이름.
        public static string DifficultyLabel(int difficulty) => difficulty switch
        {
            -2 => "너무 쉬움",
            -1 => "쉬움",
            0 => "적당",
            1 => "어려움",
            2 => "너무 어려움",
            _ => difficulty.ToString(CultureInfo.InvariantCulture),
        };

        // 태그 어휘(묶음, ID, 이름). ID가 파일에 들어간다.
        public static readonly (string Group, string Id, string Label)[] Tags =
        {
            ("속도", "too-slow", "너무 느림"),
            ("속도", "too-fast", "너무 빠름"),
            ("속도", "level-stall", "Level 정체"),
            ("속도", "level-rush", "Level 급등"),
            ("재미", "boring", "지루함"),
            ("재미", "satisfying", "시원함"),
            ("재미", "chaotic", "정신없음"),
            ("밀도", "too-dense", "너무 빽빽함"),
            ("밀도", "too-sparse", "너무 듬성함"),
            ("보상", "gold-too-low", "Gold 부족"),
            ("보상", "gold-too-high", "Gold 과다"),
            ("보상", "golden-too-strong", "황금 과함"),
            ("보상", "node-pointless", "노드 효과 없음"),
            ("기타", "bug", "버그"),
            ("기타", "visual", "연출"),
            ("기타", "ui", "UI"),
        };

        public static string TagLabel(string id)
        {
            foreach ((string _, string tagId, string label) in Tags)
            {
                if (tagId == id)
                    return label;
            }

            return id;
        }

        // 메모 파일 하나(JSON 객체 하나)를 읽는다. 형식이 틀리면 null(사람이 고치다 깨뜨려도 나머지는 읽는다).
        public static FeelNoteView ParseFile(string json)
        {
            try
            {
                return PlaytestJson.Parse(json) is JsonObject obj && obj.Int("schema") == FeelNote.Schema ? ViewOf(obj) : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static FeelNoteView ViewOf(JsonObject obj)
        {
            JsonObject setup = obj.Object("setup");
            JsonObject content = obj.Object("content");
            JsonObject battle = obj.Object("battle");
            var view = new FeelNoteView
            {
                Id = obj.Text("id"),
                Name = obj.Text("name"),
                AtUtc = obj.Text("atUtc"),
                SetupKey = obj.Text("setupKey"),
                SetupName = setup?.Text("name"),
                Profile = content?.Text("profile"),
                Fingerprint = content?.Text("fingerprint"),
                Difficulty = obj.Int("difficulty"),
                Fun = obj.Int("fun"),
                Text = obj.Text("text") ?? string.Empty,
                Intent = obj.Text("intent") ?? string.Empty,
                HasBattle = battle != null,
                BattleLevel = battle?.Int("level"),
                BattleElapsed = battle?.Number("elapsed"),
            };

            List<object> tags = obj.Array("tags");
            if (tags != null)
            {
                foreach (object tag in tags)
                {
                    if (tag is string id)
                        view.Tags.Add(id);
                }
            }

            return view;
        }
    }
}
#endif
