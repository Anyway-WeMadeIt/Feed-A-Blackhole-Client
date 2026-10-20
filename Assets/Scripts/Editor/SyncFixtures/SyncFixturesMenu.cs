using System.Diagnostics;
using System.Text;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 서버 동기화 픽스처 도구(기대값 표, 골든 로그)의 메뉴: BlackHole > Sync Fixtures > ...
    // 판을 헤드리스로 돌리는 부분(HeadlessBattle, NearestEnemyBot, CheapestFirstBuyer)이 지금 콘텐츠로 제대로 도는지 확인하는 스모크 실행을 둔다.
    internal static class SyncFixturesMenu
    {
        [MenuItem("BlackHole/Sync Fixtures/Smoke Run")]
        private static void SmokeRunMenu() => Debug.Log(SmokeRun(battles: 30, maxBattleSeconds: 600));

        // 캠페인: 이정표에 닿지 못한 채 300판이면 막힌 것으로 보고 멈춘다. 지금 봇은 성장도 3(Lv30)까지 가서 거기서 막힌다(약 600판, 1분 안팎).
        // 판 루프는 백그라운드에서 돌고, 에디터에는 취소할 수 있는 진행 막대가 뜬다. 끝나면 보고서를 콘솔에 남긴다.
        [MenuItem("BlackHole/Sync Fixtures/Campaign Run (성장도 3까지)")]
        private static void CampaignRunMenu()
        {
            if (Running)
            {
                Debug.LogWarning("[SyncFixtures] 이미 캠페인이 돌고 있다.");
                return;
            }

            CancelRequested = false;
            string message = StartBackgroundSmoke(battles: 3000, maxBattleSeconds: 600, printEvery: 25, stallBattles: 300);

            if (!Running)
            {
                Debug.LogError("[SyncFixtures] " + message);
                return;
            }

            EditorApplication.update += PollCampaign;
        }

        // 진행 막대를 갱신하고, 끝나면 보고서를 남긴다.
        private static void PollCampaign()
        {
            if (Running)
            {
                // 전체 판 수를 미리 알 수 없다(막힘 규칙으로 끝난다). 막대는 진행 중임만 보여 주고 글자로 판 번호를 알린다.
                float pulse = (float)(EditorApplication.timeSinceStartup % 2) / 2;

                if (EditorUtility.DisplayCancelableProgressBar("Sync Fixtures: 캠페인", Progress, pulse))
                    CancelRequested = true;

                return;
            }

            EditorApplication.update -= PollCampaign;
            EditorUtility.ClearProgressBar();
            Debug.Log(BackgroundReport);
        }

        // 지금 콘텐츠로 빈 진행도에서 [판 → 결산 → 구매]를 battles번 돌리고 보고서를 돌려준다.
        // - 같은 시드로 첫 판을 두 번 돌려 결과가 같은지 확인한다(재현성).
        // - 판마다 걸린 실제 시간을 적는다(픽스처를 만드는 데 걸릴 시간을 가늠한다).
        // - 판마다 칸별 처치 장부가 EXP·Gold·처치 수와 맞는지 검사한다(W3-1 완료 조건, KillLedgerCheck). 어긋난 판은 늘 적는다.
        // maxBattleSeconds: 판 하나의 상한(판 시간). 끝나지 않는 판을 이 시간에서 끊는다.
        // printEvery: 판 줄을 이 간격으로만 적는다(이정표에 닿은 판, 상한에서 끊긴 판, 마지막 판은 늘 적는다). 1이면 모두.
        // stallBattles: 이정표에 닿지 못한 채 이만큼 판이 이어지면 막힌 것으로 보고 멈춘다. 봇이 마지막 이정표를 못 넘으면 Gold만 쌓여 long이 넘치기 때문이다. 0 이하면 쓰지 않는다.
        public static string SmokeRun(int battles, float maxBattleSeconds, int printEvery = 1, int stallBattles = 300)
        {
            LoadedContent loaded = LoadContent(out string error);
            return loaded == null ? error : Run(loaded, battles, maxBattleSeconds, printEvery, stallBattles);
        }

        // 긴 캠페인을 에디터를 멈추지 않고 돌린다. 콘텐츠는 메인 스레드에서 불러오고(에셋 접근), 판 루프는 백그라운드에서 돈다.
        // 끝나면 BackgroundReport에 보고서가 담긴다(그전에는 null).
        public static string BackgroundReport { get; private set; }

        // 돌고 있는 판의 진행 상황(판 번호, 성장도, 마지막 판의 소요). 긴 실행이 어디쯤인지 본다.
        public static volatile string Progress = "";

        // 백그라운드 실행이 돌고 있는가. 끝나면(보고서가 담긴 뒤) false다.
        public static volatile bool Running;

        // true로 두면 돌고 있는 실행이 다음 판 앞에서 멈추고 그때까지의 보고서를 남긴다.
        public static volatile bool CancelRequested;

        public static string StartBackgroundSmoke(int battles, float maxBattleSeconds, int printEvery, int stallBattles = 300)
        {
            LoadedContent loaded = LoadContent(out string error);

            if (loaded == null)
                return error;

            BackgroundReport = null;
            CancelRequested = false;
            Running = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                try { BackgroundReport = Run(loaded, battles, maxBattleSeconds, printEvery, stallBattles); }
                catch (System.Exception exception) { BackgroundReport = "실패: " + exception; }
                finally { Running = false; }
            });

            return "시작했다. 끝나면 BackgroundReport에 보고서가 담긴다.";
        }

        internal static LoadedContent LoadContent(out string error)
        {
            error = null;
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (setup == null)
            {
                error = "게임 콘텐츠 세트(GameContentSetup)를 찾지 못했다.";
                return null;
            }

            LoadedContent loaded = GameContentLoader.Load(setup);

            if (loaded == null)
                error = "콘텐츠가 검사를 통과하지 못했다. 콘솔의 오류를 본다.";

            return loaded;
        }

        private static string Run(LoadedContent loaded, int battles, float maxBattleSeconds, int printEvery, int stallBattles)
        {
            var report = new StringBuilder();
            report.AppendLine($"[SyncFixtures] 콘텐츠 버전 {loaded.ServerContentVersion}, 판 {battles}개, 판 상한 {maxBattleSeconds}초");

            // 재현성: 같은 시드, 같은 (빈) 진행도에서 두 번.
            HeadlessBattleResult first = RunFresh(loaded, seed: 1, maxBattleSeconds);
            HeadlessBattleResult second = RunFresh(loaded, seed: 1, maxBattleSeconds);
            bool same = Same(first, second);
            report.AppendLine($"재현성(시드 1, 빈 진행도 두 번): {(same ? "같다" : "다르다 !!")}");

            if (first.LedgerProblem != null)
                report.AppendLine($"  재현성 판의 처치 장부 불일치 !!: {first.LedgerProblem}");

            if (!same)
            {
                report.AppendLine($"  1회: {Describe(first)}");
                report.AppendLine($"  2회: {Describe(second)}");
            }

            // 이어서 돌리기: 결산 → 가장 싼 노드부터 구매.
            var progress = new ProgressState();
            var total = Stopwatch.StartNew();
            int capped = 0;
            int ledgerMismatches = 0;
            long bucketTotal = 0;
            long goldenCrits = 0;
            long timeBonuses = 0;
            int sinceMilestone = 0;
            long maxBattleMs = 0;

            for (int index = 1; index <= battles; index++)
            {
                if (CancelRequested)
                {
                    report.AppendLine($"취소됨: {index - 1}판까지 돌았다.");
                    battles = index - 1;
                    break;
                }

                int seed = unchecked(1 * 100003 + index);
                UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, loaded.NodeTree);

                Progress = $"판 {index} 시작: 성장도 {progress.GrowthStage}, Gold {progress.Gold:N0}, 경과 {total.ElapsedMilliseconds}ms";
                var timer = Stopwatch.StartNew();
                HeadlessBattleResult battle = HeadlessBattle.Run(
                    loaded.Content, progress, upgrades, new NearestEnemyBot(), seed, HeadlessBattle.DefaultFrameSeconds, maxBattleSeconds);
                long battleMs = timer.ElapsedMilliseconds;
                maxBattleMs = System.Math.Max(maxBattleMs, battleMs);

                var purchases = CheapestFirstBuyer.BuyAll(progress, loaded.NodeTree);
                capped += battle.Capped ? 1 : 0;
                ledgerMismatches += battle.LedgerProblem != null ? 1 : 0;
                bucketTotal += battle.Raw.KillBuckets.Count;
                goldenCrits += battle.Raw.GoldenCritCount;
                timeBonuses += battle.Raw.TimeBonusCount;
                sinceMilestone = battle.Raw.ReachedMilestone ? 0 : sinceMilestone + 1;

                if (index % printEvery == 0 || index == battles || battle.Raw.ReachedMilestone || battle.Capped || battle.LedgerProblem != null)
                    report.AppendLine($"  판 {index}: {Describe(battle)} | 구매 {purchases.Count}번, 남은 Gold {progress.Gold:N0}, 성장도 {progress.GrowthStage} | {battleMs}ms");

                if (battle.LedgerProblem != null)
                    report.AppendLine($"    처치 장부 불일치 !!: {battle.LedgerProblem}");

                if (progress.GrowthStage >= loaded.Content.Growth.MaxStage)
                {
                    report.AppendLine($"클리어: 성장도 {progress.GrowthStage}(마지막 이정표)에 {index}판째 닿았다.");
                    battles = index;
                    break;
                }

                if (stallBattles > 0 && sinceMilestone >= stallBattles)
                {
                    report.AppendLine($"막힘: 성장도 {progress.GrowthStage}에서 {stallBattles}판 동안 이정표에 닿지 못해 {index}판째에 멈춘다(Gold {progress.Gold:N0}).");
                    battles = index;
                    break;
                }
            }

            report.AppendLine($"상한에서 끊긴 판 {capped}개, 마지막 이정표 뒤 {sinceMilestone}판, 가장 오래 걸린 판 {maxBattleMs}ms");
            report.AppendLine($"처치 장부(W3-1): {(ledgerMismatches == 0 ? "모든 판이 맞다" : $"어긋난 판 {ledgerMismatches}개 !!")}, 판당 평균 칸 {bucketTotal / (double)System.Math.Max(1, battles):F1}개, 황금 치명 합 {goldenCrits:N0}번, 시간 추가 합 {timeBonuses:N0}번");

            report.AppendLine($"합계 {total.ElapsedMilliseconds}ms, 판당 평균 {total.ElapsedMilliseconds / (double)System.Math.Max(1, battles):F0}ms, 성장도 {progress.GrowthStage}");
            return report.ToString();
        }

        private static HeadlessBattleResult RunFresh(LoadedContent loaded, int seed, float maxBattleSeconds)
        {
            var progress = new ProgressState();
            UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, loaded.NodeTree);
            return HeadlessBattle.Run(loaded.Content, progress, upgrades, new NearestEnemyBot(), seed, HeadlessBattle.DefaultFrameSeconds, maxBattleSeconds);
        }

        private static bool Same(HeadlessBattleResult a, HeadlessBattleResult b) =>
            a.Raw.PlayedSeconds == b.Raw.PlayedSeconds
            && a.Raw.EarnedGold == b.Raw.EarnedGold
            && a.Raw.SettledGold == b.Raw.SettledGold
            && a.Raw.ReachedLevel == b.Raw.ReachedLevel
            && a.Raw.Exp == b.Raw.Exp
            && a.Raw.TotalKills == b.Raw.TotalKills
            && a.Raw.KillBuckets.Count == b.Raw.KillBuckets.Count
            && a.Raw.GoldenCritBonusGold == b.Raw.GoldenCritBonusGold
            && a.Raw.TimeBonusCount == b.Raw.TimeBonusCount
            && a.GoldAfter == b.GoldAfter;

        private static string Describe(HeadlessBattleResult battle) =>
            $"{battle.Raw.PlayedSeconds:F1}초, 처치 {battle.Raw.TotalKills}, 번 Gold {battle.Raw.EarnedGold:N0}, " +
            $"Level {battle.StartInfo.StartLevel}→{battle.Raw.ReachedLevel}, EXP {battle.Raw.Exp:N0}, " +
            $"{(battle.Raw.ReachedMilestone ? "이정표 닿음" : "이정표 미달")}{(battle.Capped ? ", 상한에서 끊김" : "")}";

        private static T FindAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
