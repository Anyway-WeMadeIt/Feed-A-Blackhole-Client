using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlackHole.Core;
using BlackHole.Simulation;
using UnityEngine;

namespace BlackHole.Tests
{

    // W 3-1 칸별 처치 집께 검증을 씬에서 돌리고 결과를 콘솔로그에 기록
    //   - 칸의 전제: 황금 아닌 성질은 성질 없음과, 황금 성질끼리는 서로 Gold·EXP가 같다.
    //   - 완료 조건: Σ(칸 처치 수 × 칸 EXP) + 시작 EXP == Hq.Exp, Σ(칸 처치 수 × 칸 Gold) + 황금 치명타 보너스 == EarnedGold.
    //   - 일관성: 칸 중복 없음, 칸 합계 == TotalKills, 종류별 합계 == Kills, 혜성 칸 (0, 1, 황금 아님).
    //   - 경로: 시드 전체에서 황금 처치·황금 치명타·시간 추가·혜성 수거가 한 번 이상.
    public sealed class KillBucketSettlementProbe : MonoBehaviour
    {
        private const string Tag = "[W3-1]";

        [SerializeField] private int _firstSeed = 1;
        [SerializeField, Min(1)] private int _seedCount = 20;
        [SerializeField] private bool _runOnStart = true;

        [Tooltip("시드마다 통과 요약을 남긴다. 끄면, 실패와 최종 요약만 남는다")]
        [SerializeField] private bool _logEachSeed = true;

        private bool _running;

        private void Start()
        {
            if (_runOnStart)
                Run();
        }

        // Play 중에는 한 프레임에 시드 하나씩 돌려 에디터가 멈추지 않게 하고, Play 밖에서는 한 번에 끝까지 돈다.
        [ContextMenu("Run W3-1 Kill Bucket Check")]
        public void Run()
        {
            if (_running)
            {
                Debug.LogWarning($"{Tag} 이미 검사 중입니다.", this);
                return;
            }

            if (Application.isPlaying)
            {
                StartCoroutine(RunAll());
                return;
            }

            IEnumerator steps = RunAll();
            while (steps.MoveNext()) { }
        }

        private IEnumerator RunAll()
        {
            _running = true;
            Debug.Log($"{Tag} 칸별 처치 검증 시작: 시드 {_firstSeed} ~ {_firstSeed + _seedCount - 1}", this);

            var coverage = new Coverage();
            int passedSeeds = 0;
            int failedSeeds = 0;
            bool tableChecked = false;
            bool tablePassed = false;

            for (int i = 0; i < _seedCount; i++)
            {
                int seed = _firstSeed + i;
                var failures = new List<string>();
                BattleRun run = null;

                try
                {
                    run = RunBattle(seed);

                    // 수치표는 산 노드로만 정해지므로(EnemyStatTable) 판마다 같다. 첫 판에서 한 번만 본다.
                    if (!tableChecked)
                    {
                        tablePassed = CheckStatTable(run.World.Stats);
                        tableChecked = true;
                    }

                    CheckSums(run, failures);
                    CheckConsistency(run.Raw, failures);
                    coverage.Add(run.Raw);
                }
                catch (Exception e)
                {
                    failures.Add($"예외: {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e, this);
                }

                if (failures.Count == 0)
                {
                    passedSeeds++;

                    if (_logEachSeed)
                        Debug.Log($"{Tag} seed {seed} 통과 - {Summary(run)}", this);
                }
                else
                {
                    failedSeeds++;
                    string summary = run != null ? $" - {Summary(run)}" : "";

                    Debug.LogError($"{Tag} seed {seed} 실패 {failures.Count}건{summary}\n  - {string.Join("\n  - ", failures)}", this);
                }

                if (Application.isPlaying) yield return null;
            }

            if (!tableChecked)
            {
                Debug.LogError($"{Tag} 결과 : 실행 안됨 - 판을 하나도 돌리지 못해 아무 검사도 하지 않았다." +
                    $"위의 첫 예외를 본다 (Run Battle 준비 코드 등).", this);
                _running = false;
                yield break;
            }

            bool coveragePassed = coverage.Report(this);
            bool allPassed = tablePassed && failedSeeds == 0 && coveragePassed;

            string result = $"{Tag} 결과: {(allPassed ? "전체 통과" : "실패 있음")} | " +
                $"수치표 전제 {(tablePassed ? "통과" : "실패")} | " +
                $"시드 {passedSeeds}/{_seedCount} 통과 | " +
                $"경로 {(coveragePassed ? "통과" : "실패")}";

            if (allPassed) Debug.Log(result, this);
            else Debug.Log(result, this);

            _running = false;
        }

        #region 검사

        // 칸의 전제. 실패하면 집계가 아니라 계약(칸 정의) 문제다.
        private bool CheckStatTable(EnemyStatTable table)
        {
            var mismatches = new List<string>();

            foreach (EnemyDefinition kind in table.Kinds)
            {
                EnemyComposition composition = table.CompositionOf(kind);

                for(int size = SizeRule.Base; size <= composition.Size; size++)
                {
                    for (int tier = 00; tier < kind.Tiers.Count; tier++)
                    {
                        EnemyStats plain = table.Of(kind, tier, null, size);
                        bool hasGolden = false;
                        EnemyStats golden = default;

                        foreach (EnemyTraitDefinition trait in composition.Traits)
                        {
                            EnemyStats stats = table.Of(kind, tier, trait, size);
                            string at = $"{kind.Type} tier {tier} size {size} trait {trait.Type}";

                            if (trait.Effect is GoldenDefinition)
                            {
                                if (!hasGolden)
                                {
                                    golden = stats;
                                    hasGolden = true;
                                }
                                else if (!SameReward(stats, golden))
                                {
                                    mismatches.Add($"{at} : 다른 황금 성질과 다르다 ({Describe(stats)} vs {Describe(plain)}");
                                }
                            }
                            else if(!SameReward(stats, plain))
                            {
                                mismatches.Add($"{at}: 성질 없음과 다르다 ({Describe(stats)} vs {Describe(plain)})");
                            }
                        }
                    }
                }
            }

            if (mismatches.Count == 0)
            {
                Debug.Log($"{Tag} 수치표 전제 통과 — 처치 보상이 (종류, 색 등급, 크기, 황금 여부)로 정해진다.", this);
                return true;
            }

            Debug.LogError(
                $"{Tag} 수치표 전제 실패 {mismatches.Count}건 — 성질이 Gold·EXP를 바꾼다. 집계가 아니라 칸 정의(계약) 문제다.\n  - " +
                string.Join("\n  - ", mismatches), this);
            return false;
        }
        #endregion

        // 완료 조건
        private static void CheckSums(BattleRun run, List<string> failures)
        {
            BattleRawData raw = run.Raw;
            Hq hq = run.World.Hq;
            EnemyStatTable table = run.World.Stats;

            long startExp = hq.Growth.ExpToReach(hq.StartLevel) ?? 0;
            long bucketExp = 0;
            long bucketGold = 0;

            foreach (KillBucketCount kill in raw.KillBuckets)
            {
                if (!TryStatsOf(table, kill.Bucket, out EnemyStats stats))
                {
                    failures.Add($"{kill.Bucket.Enemy.Type} : 황금 칸인데, 이 판 구성에 황금 성질이 없다");
                    continue;
                }

                bucketExp = checked(bucketExp + (long)kill.Count * stats.Exp);
                bucketGold = checked(bucketGold + (long)kill.Count * stats.Gold);
            }

            long expectedExp = startExp + bucketExp;
            if (expectedExp != hq.Exp)
                failures.Add($"EXP 불일치 : 시작 {startExp} + 칸 {bucketExp} = " +
                    $"{expectedExp}, Hq.Exp {hq.Exp} (차이 {hq.Exp - expectedExp})");

            long expectedGold = bucketGold + raw.GoldenCritBonusGold;
            if (expectedGold != raw.EarnedGold)
                failures.Add($"Gold 불일치: 칸 {bucketGold} + 치명 보너스 {raw.GoldenCritBonusGold} = " +
                    $"{expectedGold}, EarnedGold {raw.EarnedGold} (차이 {raw.EarnedGold - expectedGold})");

            if (raw.Exp != hq.Exp)
                failures.Add($"BattleRawData.Exp {raw.Exp} != Hq.Exp {hq.Exp}");
        }

        // 일관성
        private static void CheckConsistency(BattleRawData raw, List<string> failures)
        {
            int distinct = raw.KillBuckets.Select(k => k.Bucket).Distinct().Count();
            if (distinct != raw.KillBuckets.Count)
                failures.Add($"칸 중복: 칸 {raw.KillBuckets.Count}개 중 서로 다른 칸 {distinct}개");

            int distinctLogKeys = raw.KillBuckets.Select(LogKey).Distinct().Count();
            if (distinctLogKeys != raw.KillBuckets.Count)
                failures.Add($"로그 키 중복: 칸 {raw.KillBuckets.Count}개 중 서로 다른 (enemyId, tier, size, golden) {distinctLogKeys}개");

            int bucketTotal = raw.KillBuckets.Sum(k => k.Count);
            if (bucketTotal != raw.TotalKills)
                failures.Add($"칸 합계 {bucketTotal} != TotalKills {raw.TotalKills}");

            var byKind = new Dictionary<EnemyDefinition, int>();
            foreach (EnemyKillCount kill in raw.Kills)
                byKind[kill.Enemy] = byKind.TryGetValue(kill.Enemy, out int n) ? n +kill.Count : kill.Count;

            if (byKind.Count != raw.Kills.Count)
                failures.Add($"종류 수 : 칸 기준 {byKind.Count}, KillByKind {raw.Kills.Count}");
            foreach(EnemyKillCount kill in raw.Kills)
            {
                int fromBuckets = byKind.TryGetValue(kill.Enemy, out int n) ? n : 0;
                if (fromBuckets != kill.Count)
                    failures.Add($"{kill.Enemy.Type} 처치 수 : 칸 기준 {fromBuckets}, KillsByKind {kill.Count}");
            }
            foreach (KillBucketCount kill in raw.KillBuckets.Where(k => k.Bucket.Enemy.IsPickup))
            {
                KillBucket b = kill.Bucket;
                if (b.Tier != 0 || b.Size != SizeRule.Base || b.Golden)
                    failures.Add($"혜성 칸이 (0, {SizeRule.Base}, 황금 아님)이 아니다: {b.Enemy.Type} tier {b.Tier} size {b.Size} golden {b.Golden}");
            }

            if (raw.GoldenCritBonusGold < 0)
                failures.Add($"치명 보너스가 음수 : {raw.GoldenCritBonusGold}");

            if (raw.GoldenCritCount == 0 && raw.GoldenCritBonusGold != 0)
                failures.Add($"치명타 0회인데 보너스 {raw.GoldenCritBonusGold}");

            // Gold 치명타는 황금 적 처치에서만, 시간 추가는 수거형이 아닌 처치에서만 발생
            int goldenKills = raw.KillBuckets.Where(k => k.Bucket.Golden).Sum(k => k.Count);
            if (raw.GoldenCritCount > goldenKills)
                failures.Add($"Gold 치명타 {raw.GoldenCritCount}회 -> Gold 처치 {goldenKills}");

            int nonPickupKills = raw.KillBuckets.Where(k => !k.Bucket.Enemy.IsPickup).Sum(k => k.Count);
            if (raw.TimeBonusCount > nonPickupKills)
                failures.Add($"시간 추가 {raw.TimeBonusCount}회 -> 수거형 아닌 처치 {nonPickupKills}");
        }

        // 시드 전체에서 각 보상 경로가 한 번 이상 일어났는가. 아니면 위 검사가 그 경로를 지나지 않고 통과한 것이다.
        private sealed class Coverage
        {
            private int _goldenKills, _goldenCrits, _timeBonuses, _pickupKills;

            public void Add(BattleRawData raw)
            {
                _goldenKills += raw.KillBuckets.Where(k => k.Bucket.Golden).Sum(k => k.Count);
                _pickupKills += raw.KillBuckets.Where(k => k.Bucket.Enemy.IsPickup).Sum(k => k.Count);
                _goldenCrits += raw.GoldenCritCount;
                _timeBonuses += raw.TimeBonusCount;
            }

            public bool Report(UnityEngine.Object context)
            {
                var missing = new List<string>();
                if (_goldenKills == 0) missing.Add("황금 적 처치");
                if (_goldenCrits == 0) missing.Add("황금 치명타");
                if (_timeBonuses == 0) missing.Add("시간 추가 성공");
                if (_pickupKills == 0) missing.Add("혜성 수거");

                string counts = $"황금 처치 {_goldenKills}, 황금 치명타 {_goldenCrits}, 시간 추가 {_timeBonuses}, 혜성 {_pickupKills}";

                if (missing.Count == 0)
                {
                    Debug.Log($"{Tag} 경로 통과 - {counts}", context);
                    return true;
                }

                Debug.LogError($"{Tag} 경로 부족: {string.Join(", ", missing)}가 한 번도 없다. 시드를 늘리거나 해당 적을 깔아 두고 다시 돈다. ({counts})", context);
                return false;
            }
        }

        #region 도움 함수

        // 칸 하나의 Gold/EXP: 이 판의 수치표에서 황금이면 황금 성질의 값, 아니면 성질 없음의 값
        private static bool TryStatsOf(EnemyStatTable table, KillBucket bucket, out EnemyStats stats)
        {
            EnemyTraitDefinition trait = null;

            if (bucket.Golden)
            {
                trait = table.CompositionOf(bucket.Enemy).Traits.FirstOrDefault(t => t.Effect is GoldenDefinition);

                if (trait == null)
                {
                    stats = default;
                    return false;
                }
            }

            stats = table.Of(bucket.Enemy, bucket.Tier, trait, bucket.Size);
            return true;
        }

        private static (EnemyType, int, int, bool) LogKey(KillBucketCount kill) =>
            (kill.Bucket.Enemy.Type, kill.Bucket.Tier, kill.Bucket.Size, kill.Bucket.Golden);
        private static bool SameReward(EnemyStats a, EnemyStats b) => a.Gold == b.Gold && a.Exp == b.Exp;
        private static string Describe(EnemyStats s) => $"Gold {s.Gold}, EXP {s.Exp}";
        private static string Summary(BattleRun run)
        {
            BattleRawData raw = run.Raw;
            var sb = new StringBuilder();
            sb.Append($"{raw.PlayedSeconds:0.0}초, 처치 {raw.TotalKills}(칸 {raw.KillBuckets.Count}), ");
            sb.Append($"EXP {raw.Exp}, Gold {raw.EarnedGold}, ");
            sb.Append($"치명 {raw.GoldenCritCount}회/{raw.GoldenCritBonusGold}G, 시간 추가 {raw.TimeBonusCount}회");
            if (run.Record.Capped) sb.Append(", 안전 상한에 끊김");
            return sb.ToString();
        }
        #endregion

        private static BattleRun RunBattle(int seed) =>
            throw new NotImplementedException("RunBattle : 판 준비 코드 (콘텐츠/진행 상태/노드 수치/봇)를 채워야 한다");
    }
}
