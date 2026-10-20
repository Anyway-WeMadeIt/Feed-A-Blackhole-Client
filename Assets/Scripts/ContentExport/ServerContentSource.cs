using System;
using BlackHole.Core;

namespace BlackHole.ContentExport
{
    // 서버 콘텐츠 묶음을 만드는 데 필요한 클라이언트 콘텐츠(계약 1.2). 에디터 도구가 에셋에서 모아 넘긴다.
    public sealed class ServerContentSource
    {
        // 노드 시트 4개의 원본 CSV 글자(시트에서 내려받은 그대로).
        public string UpgradeStatsCsv { get; }
        public string NodesCsv { get; }
        public string NodeCostCsv { get; }
        public string NodeEffectsCsv { get; }

        // 노드 배치(NodeCatalog.ToData()).
        public NodeTreeData Layout { get; }

        // 판 콘텐츠(GameContentSetup.ToData()).
        public ContentData Battle { get; }

        // 불러온 노드 콘텐츠. UpgradeStat → 시트의 StatId를 찾는 데 쓴다.
        public NodeContent Nodes { get; }

        public ServerContentSource(
            string upgradeStatsCsv,
            string nodesCsv,
            string nodeCostCsv,
            string nodeEffectsCsv,
            NodeTreeData layout,
            ContentData battle,
            NodeContent nodes)
        {
            UpgradeStatsCsv = upgradeStatsCsv ?? throw new ArgumentNullException(nameof(upgradeStatsCsv));
            NodesCsv = nodesCsv ?? throw new ArgumentNullException(nameof(nodesCsv));
            NodeCostCsv = nodeCostCsv ?? throw new ArgumentNullException(nameof(nodeCostCsv));
            NodeEffectsCsv = nodeEffectsCsv ?? throw new ArgumentNullException(nameof(nodeEffectsCsv));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }
    }
}
