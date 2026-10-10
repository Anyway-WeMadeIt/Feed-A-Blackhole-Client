using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlackHole.Core;
using BlackHole.Unity;

// 노드 단계 세팅(qa-nodes-1~8) 검사. 생성기: Tools/QA/build_node_suite.py, 소속: Docs/QA/NodeSuite/zones.json.
// 실제 CSV·NodeCatalog·HqGrowthSetup을 읽는다. 가격이 바뀌어도 소속·연결이 그대로면 통과한다.
internal static class NodeSuiteChecks
{
    private const string Scenarios = "Assets/Playtest/Scenarios/";
    private const int LastStage = 8;
    private static readonly Regex Generated = new(@"^qa-(nodes-\d+|zone\d\d-(off|on|full)|budget-zone\d\d(-\d)?|mark\d\d-(before|after)|node-first)\.json$");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("[노드 단계] " + message);
    }

    private static string Text(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

    private static NodeTreeData Layout(string path)
    {
        var data = new NodeTreeData();
        foreach (Match block in Regex.Matches(Text(path), @"^    - Id: (.*?)(?=^    - Id: |\z)", RegexOptions.Multiline | RegexOptions.Singleline))
        {
            string body = block.Groups[1].Value;
            data.Nodes.Add(new NodeData
            {
                Id = body.Split('\n')[0].Trim(),
                Start = Regex.Match(body, @"^      Start: (\d+)$", RegexOptions.Multiline).Groups[1].Value == "1",
                X = int.Parse(Regex.Match(body, @"^      X: (-?\d+)$", RegexOptions.Multiline).Groups[1].Value),
                Y = int.Parse(Regex.Match(body, @"^      Y: (-?\d+)$", RegexOptions.Multiline).Groups[1].Value),
                Links = Regex.Matches(body, @"^      - (.+)$", RegexOptions.Multiline).Select(m => m.Groups[1].Value.Trim()).ToList(),
            });
        }
        Require(data.Nodes.Count > 0, "NodeCatalog 배치를 찾지 못함");
        return data;
    }

    private static HqGrowthDefinition Growth(string path)
    {
        string text = Text(path);
        byte[] bytes = Convert.FromHexString(Regex.Match(text, @"levelExp: ([0-9a-f]+)").Groups[1].Value);
        var exps = new long[bytes.Length / 8];
        for (int i = 0; i < exps.Length; i++) exps[i] = BitConverter.ToInt64(bytes, i * 8);
        var milestones = Regex.Matches(text, @"- level: (\d+)\s+targetGold: (\d+)\s+fieldScale: ([\d.]+)")
            .Select(m => new HqMilestone(int.Parse(m.Groups[1].Value), long.Parse(m.Groups[2].Value),
                float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture))).ToList();
        return new HqGrowthDefinition(exps, milestones);
    }

    public static void Run()
    {
        const string table = "Assets/Data/NodeTable/";
        var content = NodeContentCsv.Load(File.ReadAllText(table + "UpgradeStats.csv"), File.ReadAllText(table + "Nodes.csv"),
            File.ReadAllText(table + "NodeCost.csv"), File.ReadAllText(table + "NodeEffects.csv"));
        Require(content.Content != null, "실제 CSV 로드: " + string.Join("; ", content.Diagnostics));
        NodeTreeData data = Layout("Assets/Data/NodeCatalog.asset");
        var loaded = NodeTreeLoader.Load(data, content.Content);
        Require(loaded.Tree != null, "실제 배치 로드: " + string.Join("; ", loaded.Diagnostics));
        HqGrowthDefinition growth = Growth("Assets/Data/HqGrowthSetup.asset");

        // 단계 소속: 모든 노드가 정확히 한 단계에 있다.
        using var zones = JsonDocument.Parse(File.ReadAllText("Docs/QA/NodeSuite/zones.json"));
        var stageOf = new Dictionary<string, int>();
        foreach (JsonElement zone in zones.RootElement.GetProperty("zones").EnumerateArray())
            foreach (JsonElement node in zone.GetProperty("nodes").EnumerateArray())
                Require(stageOf.TryAdd(node.GetString(), zone.GetProperty("id").GetInt32()), "두 단계에 들어간 노드: " + node.GetString());
        var ids = loaded.Tree.Nodes.Select(n => n.Id).ToHashSet();
        Require(ids.SetEquals(stageOf.Keys), "단계 소속과 노드 목록이 다름: " + string.Join(", ", ids.Except(stageOf.Keys).Concat(stageOf.Keys.Except(ids))));

        // 생성 파일은 qa-nodes-1~8뿐이어야 한다(예전 생성 파일이 남지 않게).
        var expectedFiles = Enumerable.Range(1, LastStage).Select(i => $"qa-nodes-{i}.json").ToHashSet();
        var onDisk = Directory.GetFiles(Scenarios, "qa-*.json").Select(Path.GetFileName).Where(n => Generated.IsMatch(n)).ToHashSet();
        Require(onDisk.SetEquals(expectedFiles), "생성 파일 불일치: " + string.Join(", ", onDisk.Except(expectedFiles).Concat(expectedFiles.Except(onDisk))));

        for (int stage = 1; stage <= LastStage; stage++)
        {
            string name = $"qa-nodes-{stage}";
            Require(File.Exists(Scenarios + name + ".json.meta"), ".meta 없음: " + name);
            var scenario = PlaytestScenario.Parse(File.ReadAllText(Scenarios + name + ".json"), out string error);
            Require(scenario != null, name + ": " + error);
            var progress = new ProgressState(); var errors = new List<string>(); var warnings = new List<string>();
            Require(ScenarioRunner.TryApply(scenario, progress, loaded.Tree, growth, errors, warnings), name + ": " + string.Join("; ", errors));

            // 누적: 1~N단계 노드를 모두 최대 Rank로, 그 밖의 노드는 없음.
            foreach (var node in loaded.Tree.Nodes)
                Require(progress.RankOf(node.Id) == (stageOf[node.Id] <= stage ? node.MaxRank : 0), name + ": 누적 보유가 아님 " + node.Id);

            // 게임 순서로 살 수 있는 상태: 산 노드는 모두 산 시작 노드에서 산 노드만 거쳐 이어진다.
            var reached = new HashSet<string>(data.Nodes.Where(n => n.Start && progress.Owns(n.Id)).Select(n => n.Id));
            bool grew;
            do
            {
                grew = false;
                foreach (var edge in loaded.Tree.Graph.Links)
                {
                    if (reached.Contains(edge.A) && progress.Owns(edge.B)) grew |= reached.Add(edge.B);
                    if (reached.Contains(edge.B) && progress.Owns(edge.A)) grew |= reached.Add(edge.A);
                }
            } while (grew);
            foreach (var node in loaded.Tree.Nodes)
                Require(!progress.Owns(node.Id) || reached.Contains(node.Id), name + ": 이어지지 않은 노드 " + node.Id);
        }
        Console.WriteLine($"PASS: 노드 단계 세팅 {LastStage}개 — 1~N단계 누적 보유, 구매 경로 연결(실제 CSV·NodeCatalog)");
    }
}
