using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // 검증된 적 콘텐츠: 적 종류, 출현 배치, 전투 시작 공급. 읽기 전용이며 여러 판이 함께 쓴다.
    //
    // 생성자 보장(구현 = EnemyContentInvariants와 아래 검사):
    // [1] 적 종류가 한 번씩만 있다. 공급되는 종류는 6색이다. 변환 대상이 콘텐츠에 있고 픽업이 아니며, 변환 사슬이 돌지 않는다.
    // [2] 공급은 적을 정의 객체로 참조하고(EnemyContentLoader가 종류를 해석하며 진단한다), 픽업을 가리키지 않는다.
    // [3] 출현 배치와 주기 출현 띠가 있고(필수), 주기 출현 띠는 일반 출현 띠에서 풀었을 때 띠가 된다.
    // 오류가 있는 콘텐츠의 경로별 보고는 EnemyContentLoader가 맡는다.
    public sealed class EnemyContent
    {
        public IReadOnlyList<EnemyDefinition> Enemies { get; }
        // 출현 위치.
        public EnemyPlacementDefinition EnemyPlacement { get; }
        // 주기 출현 종류(혜성)의 출현 띠. 일반 띠를 기준으로 한 오프셋이다.
        public PeriodicSpawnPlacementDefinition PeriodicSpawnPlacement { get; }
        // 전투 시작 공급. 전투를 시작할 때 한 번 공급한다. 업그레이드가 더하는 공급 수는 판 조립이 더한다.
        public IReadOnlyList<SupplyRequest> StartSupply { get; }

        public EnemyContent(
            IReadOnlyList<EnemyDefinition> enemies,
            EnemyPlacementDefinition enemyPlacement,
            IReadOnlyList<SupplyRequest> startSupply,
            PeriodicSpawnPlacementDefinition periodicSpawnPlacement)
        {
            Enemies = Copy(enemies);
            EnemyPlacement = enemyPlacement ?? throw new ArgumentNullException(nameof(enemyPlacement));
            PeriodicSpawnPlacement = periodicSpawnPlacement ?? throw new ArgumentNullException(nameof(periodicSpawnPlacement));
            StartSupply = Copy(startSupply);

            var diagnostics = new List<ContentDiagnostic>();
            EnemyContentInvariants.CollectEnemies(Enemies, diagnostics, out Dictionary<EnemyType, EnemyDefinition> enemiesByType);
            EnemyContentInvariants.CheckTierCounts(Enemies, diagnostics);
            EnemyContentInvariants.CheckKindLinks(Enemies, enemiesByType, diagnostics);

            EnemyContentInvariants.CheckSupplyKinds(StartSupply, "StartSupply", diagnostics);
            EnemyContentInvariants.CheckPeriodicSpawnPlacement(EnemyPlacement, PeriodicSpawnPlacement, diagnostics);

            if (diagnostics.Count > 0)
                throw new ArgumentException(diagnostics[0].ToString());
        }

        // 호출자가 원본 목록을 나중에 바꿔도 따라 바뀌지 않게 한다.
        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null)
                return Array.Empty<T>();

            var copy = new T[source.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
