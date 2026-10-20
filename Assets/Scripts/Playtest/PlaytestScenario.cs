#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 테스트 시나리오: 정해 둔 시점(성장도·Gold·산 노드)에서 판을 바로 시작한다.
    // 진행 상태는 저장 형식(ProgressSaveData)으로 만들어 저장을 불러올 때와 같은 검사를 거친다(ScenarioRunner).
    // 필드 이름이 곧 JSON 키다.
    [Serializable]
    internal sealed class PlaytestScenario
    {
        public const int MaxNameLength = 64;

        public string name;
        public string note;
        public string category = "기본";
        public string expected;
        public string contentFingerprint;

        public int growthStage;
        public long gold;
        public List<Node> nodes = new();
        // 0보다 크면: 적힌 노드를 산 뒤 이 Gold만큼 가장 싼 노드부터 산다(플레이어 흉내). 남은 돈은 버리고 gold로 맞춘다.
        public long autoBuyBudget;
        // 0보다 크면: 판을 이 Level에서 시작한다(판 시작 직후 EXP를 채운다).
        public int startLevel;
        // 0이 아니면: 판을 이 시드로 시작한다(같은 시드면 같은 출현).
        public int seed = 1;
        // 판을 시간 고정으로 시작한다.
        public bool freezeTime;

        [Serializable]
        public sealed class Node
        {
            public string nodeId;
            public int rank;
        }

        public static PlaytestScenario Parse(string json, out string error)
        {
            PlaytestScenario scenario;

            try
            {
                scenario = JsonUtility.FromJson<PlaytestScenario>(json);
            }
            catch (ArgumentException exception)
            {
                error = $"JSON을 읽지 못했다: {exception.Message}";
                return null;
            }

            if (scenario == null)
            {
                error = "빈 JSON이다.";
                return null;
            }

            if (string.IsNullOrEmpty(scenario.name) || scenario.name.Length > MaxNameLength)
            {
                error = $"name은 1~{MaxNameLength}자다.";
                return null;
            }

            if (scenario.gold < 0 || scenario.autoBuyBudget < 0 || scenario.startLevel < 0)
            {
                error = "gold·autoBuyBudget·startLevel은 0 이상이다.";
                return null;
            }

            scenario.nodes ??= new List<Node>();
            if (scenario.growthStage < 0) { error = "growthStage는 0 이상이다."; return null; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Node node in scenario.nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.nodeId) || node.rank <= 0 || !seen.Add(node.nodeId))
                { error = "노드 ID는 중복 없이 지정하고 Rank는 1 이상이어야 한다."; return null; }
            }
            error = null;
            return scenario;
        }
    }

    // 시나리오를 진행 상태에 넣는다. 저장을 불러올 때와 같은 검사(ProgressSave.Load)를 거친다.
    // 틀린 값(음수 Gold, 빈 노드 ID 등)은 errors, 맞춰 쓴 값(콘텐츠에 없는 노드, Rank 수 초과)은 warnings에 남긴다.
    internal static class ScenarioRunner
    {
        public static bool TryApply(
            PlaytestScenario scenario,
            ProgressState progress,
            NodeTree tree,
            HqGrowthDefinition growth,
            List<string> errors,
            List<string> warnings)
        {
            if (scenario == null || scenario.growthStage < 0 || scenario.growthStage > growth.MaxStage
                || scenario.gold < 0 || scenario.autoBuyBudget < 0 || scenario.startLevel < 0 || scenario.startLevel > growth.MaxLevel)
            { errors.Add("세팅의 단계·Level·Gold·예산이 허용 범위를 벗어났다."); return false; }
            int goal = growth.GoalLevelAt(scenario.growthStage);
            if (scenario.startLevel > 0 && (scenario.startLevel < growth.StartLevelAt(scenario.growthStage)
                || (goal != HqGrowthDefinition.NoGoal && scenario.startLevel >= goal)))
            { errors.Add("시작 Level은 해당 이정표 단계의 시작 이상, 다음 목표 미만이어야 한다."); return false; }
            if (scenario.nodes == null) { errors.Add("nodes 목록이 없다."); return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in scenario.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId) || !seen.Add(node.nodeId)
                    || !tree.TryGet(node.nodeId, out NodeDefinition definition) || node.rank <= 0 || node.rank > definition.MaxRank)
                    errors.Add("재현할 수 없는 노드 또는 Rank: " + node?.nodeId);
            }
            if (errors.Count > 0) return false;
            var data = new ProgressSaveData
            {
                FormatVersion = ProgressSaveData.CurrentFormatVersion,
                Gold = scenario.autoBuyBudget > 0 ? scenario.autoBuyBudget : scenario.gold,
                GrowthStage = scenario.growthStage,
                SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };

            foreach (PlaytestScenario.Node node in scenario.nodes)
                data.Nodes.Add(new NodeRankSaveData { NodeId = node?.nodeId, Rank = node?.rank ?? 0 });

            if (!TryRestore(data, progress, tree, growth, errors, warnings))
                return false;

            if (scenario.autoBuyBudget <= 0)
                return true;

            int bought = BuyCheapest(progress, tree);
            warnings.Add($"자동 구매: {bought}번 샀다(남은 Gold {progress.Gold:N0}은 버린다).");

            ProgressSaveData after = ProgressSave.Capture(progress, null, DateTime.UtcNow);
            after.Gold = scenario.gold;
            return TryRestore(after, progress, tree, growth, errors, warnings);
        }

        // 살 수 있는 노드 중 가장 싼 Rank부터, 살 수 없을 때까지 산다. 산 횟수를 돌려준다.
        private static int BuyCheapest(ProgressState progress, NodeTree tree)
        {
            int bought = 0;

            while (true)
            {
                string cheapest = null;
                long cheapestCost = long.MaxValue;

                foreach (NodeDefinition node in tree.Nodes)
                {
                    if (NodePurchase.Check(progress, tree, node.Id) != PurchaseResult.Purchased)
                        continue;

                    if (NodePurchase.TryGetNextCost(progress, tree, node.Id, out long cost) && (cost < cheapestCost || (cost == cheapestCost && (cheapest == null || string.CompareOrdinal(node.Id, cheapest) < 0))))
                    {
                        cheapest = node.Id;
                        cheapestCost = cost;
                    }
                }

                if (cheapest == null)
                    return bought;

                NodePurchase.TryPurchase(progress, tree, cheapest);
                bought++;
            }
        }

        private static bool TryRestore(
            ProgressSaveData data,
            ProgressState progress,
            NodeTree tree,
            HqGrowthDefinition growth,
            List<string> errors,
            List<string> warnings)
        {
            ProgressLoadResult result = ProgressSave.Load(data, tree.Content, growth);

            foreach (ContentDiagnostic adjustment in result.Adjustments)
                warnings.Add(adjustment.ToString());

            if (!result.Succeeded)
            {
                foreach (ContentDiagnostic diagnostic in result.Diagnostics)
                    errors.Add(diagnostic.ToString());

                return false;
            }

            ProgressSave.Restore(progress, result.Progress);
            return true;
        }
    }
}
#endif
