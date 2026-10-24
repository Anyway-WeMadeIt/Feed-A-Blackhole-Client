using System;
using System.Collections.Generic;
using System.Text;
using BlackHole.Core;
using BlackHole.Systems.Numbers;

namespace BlackHole.Unity
{
    // 노드 툴팁의 글: 제목(수치 이름), 본문(설명·Rank·효과, 지금 값 → 산 뒤 값), 비용 줄. 트리 보기의 올림 툴팁과 고정 패널이 쓴다.
    // 열린 구역의 자물쇠 노드(Hidden)는 제목과 설명 한 줄만. 마지막 Rank까지 샀으면 증가량 대신 지금 값. Gold가 모자라면 모자란 만큼.
    // 번역표 키는 UIText.Node·UIText.Stat. [임시] 수치의 표시 이름이 정해지면 StatId 대신 쓴다.
    internal sealed class NodeTooltipText
    {
        // 효과 줄에서 산 뒤 수치의 색(연한 녹색). 팔레트에 이 역할이 없어 상수로 둔다.
        private const string AfterValueColor = "#8FC8A0";

        private readonly NodeTree _tree;
        private readonly Localizer _localizer;
        private readonly Dictionary<UpgradeStat, UpgradeStatDefinition> _definitions = new();

        public NodeTooltipText(NodeTree tree, Localizer localizer)
        {
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

            foreach (UpgradeStatDefinition definition in tree.Content.Stats)
                _definitions[definition.Stat] = definition;
        }

        // 제목: 첫 효과의 수치 이름(번역표 stat.{StatId}.name). 없으면 노드 ID.
        public string Title(string id)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return id;

            string statId = StatIdOf(node.RankAt(1).Effects[0].Stat);
            return statId != null && _localizer.TryGet(UIText.Stat.Name(statId), out string name) ? name : id;
        }

        // 본문: 설명·Rank·효과. 올림 툴팁은 비용까지(withCost), 고정 패널은 비용을 따로 받는다(Cost).
        public string Body(string id, ProgressState progress, bool withCost)
        {
            if (!_tree.TryGet(id, out NodeDefinition node))
                return string.Empty;

            NodeStatus status = NodePurchase.StatusOf(progress, _tree, id);

            // 아직 닿지 않은 노드(자물쇠): 수치·비용 없이 무엇을 주는지 한 줄만(새 요소를 미리 알 수 있게). 설명이 없으면 이름만.
            if (status.State == NodeState.Hidden)
            {
                string hiddenStat = StatIdOf(node.RankAt(1).Effects[0].Stat);
                return hiddenStat != null && _localizer.TryGet(UIText.Stat.Description(hiddenStat), out string hiddenDescription)
                    ? hiddenDescription
                    : string.Empty;
            }

            int rank = status.Rank;
            bool maxed = status.NextCost == null;
            NodeRankDefinition shown = node.RankAt(maxed ? node.MaxRank : rank + 1);
            UpgradeStatValues values = NodePurchase.StatsFor(progress, _tree);
            // 효과 이름은 효과가 둘 이상일 때만(하나면 제목과 같은 이름이 반복된다).
            bool named = shown.Effects.Count > 1;
            var text = new StringBuilder();

            string titleStat = StatIdOf(node.RankAt(1).Effects[0].Stat);
            if (titleStat != null && _localizer.TryGet(UIText.Stat.Description(titleStat), out string description))
                text.Append(description).Append('\n');

            if (node.MaxRank > 1)
                text.Append(_localizer.Format(UIText.Node.Rank, rank, node.MaxRank)).Append('\n');

            // 설명과 효과 사이 빈 줄 하나.
            text.Append('\n');

            // 효과마다 두 줄: "[이름] +50%" 그리고 "140% → 190%"(산 뒤 수치만 녹색). 수치 줄은 안에서 줄바꿈되지 않게 <nobr>.
            // 마지막 Rank까지 샀으면 증가량은 뜻이 없으니 "[이름] 현재 190%" 한 줄만.
            foreach (NodeEffect effect in shown.Effects)
            {
                UpgradeStatDefinition stat = values.DefinitionOf(effect.Stat);
                string unit = stat.Unit == UpgradeStatUnit.Percent ? "%" : string.Empty;
                float now = values.ValueOf(effect.Stat);

                if (named)
                    text.Append(StatName(stat.StatId)).Append(' ');

                if (maxed)
                {
                    text.Append("<nobr>").Append(_localizer.Format(UIText.Node.Now, NumberText.Value(now) + unit)).Append("</nobr>");
                }
                else
                {
                    text.Append(NumberText.Signed(effect.Value)).Append(unit).Append('\n');
                    float after = Math.Min(stat.Max, Math.Max(stat.Min, now + effect.Value));
                    text.Append("<nobr>").Append(NumberText.Value(now)).Append(unit)
                        .Append("  →  <color=").Append(AfterValueColor).Append('>').Append(NumberText.Value(after)).Append(unit).Append("</color></nobr>");
                }

                text.Append('\n');
            }

            if (!withCost)
                return text.ToString().TrimEnd('\n');

            // 설명·효과와 비용 사이 빈 줄 하나(가독성).
            text.Append('\n').Append(Cost(id, progress));
            return text.ToString();
        }

        // 비용 줄: "비용 70" / "최대", Gold가 모자라면 모자란 만큼을 한 줄 더. 아직 닿지 않은 노드는 빈 글자.
        public string Cost(string id, ProgressState progress)
        {
            if (!_tree.TryGet(id, out _))
                return string.Empty;

            NodeStatus status = NodePurchase.StatusOf(progress, _tree, id);

            if (status.State == NodeState.Hidden)
                return string.Empty;

            if (status.NextCost is not long cost)
                return _localizer.Get(UIText.Node.Maxed);

            string text = _localizer.Format(UIText.Node.Cost, NumberText.Compact(cost));

            if (progress.Gold < cost)
                text += "\n" + _localizer.Format(UIText.Node.Short, NumberText.Compact(cost - progress.Gold));

            return text;
        }

        // 수치 이름: 번역표 stat.{StatId}.name. 표에 없으면 StatId 그대로.
        private string StatName(string statId) =>
            _localizer.TryGet(UIText.Stat.Name(statId), out string name) ? name : statId;

        // UpgradeStat → 시트의 StatId("timer"). 정의가 없으면 null.
        private string StatIdOf(UpgradeStat stat) =>
            _definitions.TryGetValue(stat, out UpgradeStatDefinition definition) ? definition.StatId : null;
    }
}
