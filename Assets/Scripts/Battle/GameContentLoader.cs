using System.Collections.Generic;
using System.Text;
using BlackHole.Core;
using BlackHole.Sample;
using UnityEngine;

namespace BlackHole.Unity
{
    // 콘텐츠 에셋 → 게임 정의(GameContent·NodeTree). 규칙은 Core 로더들이 보고, 여기서는 차례로 불러 진단을 콘솔에 남긴다.
    // 오류가 하나라도 있으면 null이다(부분 통과 금지).
    // 판 설정은 SampleContent(C#), 스킬은 스킬 설정 에셋, 적 종류는 적 종류 목록 에셋, 출현 배치와 전투 시작 공급은 적 공급 설정 에셋,
    // 블랙홀 성장의 Level 표는 블랙홀 성장 설정 에셋, 노드는 노드 목록(NodeCatalog)이 채운다.
    public static class GameContentLoader
    {
        public static LoadedContent Load(SkillSetup skills, EnemyCatalog enemies, EnemySupplySetup supply, HqGrowthSetup growth,
            NodeCatalog nodes)
        {
            ContentLoadResult content = ContentLoader.Load(ContentDataFrom(skills, enemies, supply, growth));
            LogErrors("콘텐츠", content.Diagnostics, null);

            if (!content.Succeeded)
                return null;

            if (nodes.Content == null)
            {
                Debug.LogError("[노드 콘텐츠] 노드 목록(NodeCatalog)에 노드 콘텐츠(NodeContentSource)를 연결해야 한다.", nodes);
                return null;
            }

            NodeContentLoadResult nodeContent = nodes.Content.Load();
            LogErrors("노드 콘텐츠", nodeContent.Diagnostics, nodes.Content);

            if (!nodeContent.Succeeded)
                return null;

            NodeTreeData layout = nodes.ToData();
            NodeTreeLoadResult tree = NodeTreeLoader.Load(layout, nodeContent.Content);
            LogErrors("노드 트리", tree.Diagnostics, nodes);

            if (!tree.Succeeded)
                return null;

            if (tree.Unplaced.Count > 0)
                Debug.LogWarning($"[노드 트리] 배치하지 않은 노드 {tree.Unplaced.Count}개는 트리에서 빠졌다(살 수 없다). 노드 도구(BlackHole > Node Tree)에서 놓는다.", nodes);

            // 노드를 모두 산 경우에도 판을 조립할 수 있어야 한다. 두 데이터는 따로 불러오므로 여기서 함께 본다.
            IReadOnlyList<ContentDiagnostic> fits = UpgradeContentCheck.Check(content.Content, tree.Tree);
            LogErrors("노드 트리 × 콘텐츠", fits, nodes);

            return fits.Count == 0 ? new LoadedContent(content.Content, tree.Tree, layout) : null;
        }

        // 콘텐츠 에셋으로 Core 저작 형식을 채운다.
        private static ContentData ContentDataFrom(SkillSetup skills, EnemyCatalog enemies, EnemySupplySetup supply, HqGrowthSetup growth)
        {
            ContentData data = SampleContent.Create();
            skills.WriteTo(data);
            enemies.WriteTo(data.Enemies);
            supply.WriteTo(data.Enemies);
            data.Growth = growth.ToData();
            return data;
        }

        // 단계마다 로그 하나: 머리 줄 아래에 진단을 한 줄씩.
        private static void LogErrors(string stage, IReadOnlyList<ContentDiagnostic> diagnostics, Object context)
        {
            if (diagnostics.Count == 0)
                return;

            var text = new StringBuilder($"[{stage}] 오류 {diagnostics.Count}개");

            foreach (ContentDiagnostic diagnostic in diagnostics)
                text.Append("\n  ").Append(diagnostic);

            Debug.LogError(text.ToString(), context);
        }
    }
}
