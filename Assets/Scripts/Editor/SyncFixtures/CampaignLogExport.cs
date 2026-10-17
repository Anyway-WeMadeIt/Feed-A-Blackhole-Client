using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using BlackHole.ContentExport;
using BlackHole.Core;
using BlackHole.Net.Unity;
using BlackHole.Sync;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 골든 캠페인 로그 내보내기(계약 3.8a, W4-2a): BlackHole > Sync Fixtures > Export Campaign Log...
    // 빈 진행도부터 [판 → 결산 → 구매]를 봇과 구매 정책으로 이어 돌면서, 게임이 쓰는 로그 생성기(SyncLogFactory)로 로그 전체를 만든다.
    // 서버 테스트(W4-2b)가 이 로그를 그대로 서버에 넣어 모두 통과하는지, 조작한 로그가 정해진 곳에서 멈추는지 확인한다.
    //
    // 로그 순서: 처음에 RESET, 판마다 RUN_START → SETTLEMENT → 구매들. 구매는 결산 뒤에 둔다(판 도중에 끼면 서버가 열린 판을 포기로 닫는다, C31).
    // 같은 콘텐츠면 같은 바이트다(C38): runId는 판 번호에서, 시각은 고정 시각 + seq초로, 빌드 버전은 golden으로 고정하고 시드는 판 번호에서 정한다.
    // 파일을 만드는 코드는 GoldenCampaignBuilder, 끝낼 때를 정하는 코드는 CampaignPolicy(둘 다 Unity 무관, 테스트 있음)다. 여기는 판을 돌려 그 둘에 넘긴다.
    internal static class CampaignLogExport
    {
        public const string FileName = "golden-campaign.json";
        public const string GzipFileName = FileName + ".gz";

        // 로그에 싣는 값(계약 3.8a의 결정성).
        public const string BuildVersion = "golden";
        private static readonly DateTime FirstLogUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 이정표 없이 이만큼 판이 이어지면 막힌 것으로 끝낸다(스모크 도구의 캠페인과 같은 값).
        public const int StallBattles = 300;
        public const int BattleLimit = 3000;

        // 클리어(마지막 이정표) 뒤에 더 돌리는 판 수(계약 C40).
        public const int PostClearBattles = 3;

        // 판 하나의 안전 상한(판 시간, 초). 닿으면 끝나지 않는 판이라 캠페인을 그 앞에서 끝낸다.
        public const float MaxBattleSeconds = 600;

        // 이 크기를 넘으면 gzip을 권한다(파일이 저장소에 들어간다).
        public const int LargeFileBytes = 4 * 1024 * 1024;

        private const string FolderKey = "BlackHole.CampaignLogExport.Folder";
        private const string ServerFixturesPath = "../Feed-A-Blackhole-Server/src/test/resources/fixtures";
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        [MenuItem("BlackHole/Sync Fixtures/Export Campaign Log...")]
        private static void ExportMenu() => ExportFromMenu(compress: false);

        [MenuItem("BlackHole/Sync Fixtures/Export Campaign Log (gzip)...")]
        private static void ExportGzipMenu() => ExportFromMenu(compress: true);

        // 같은 콘텐츠로 두 번 돌려 같은 글자인지 확인한다(재현성). 오래 걸린다(캠페인 두 번).
        [MenuItem("BlackHole/Sync Fixtures/Verify Campaign Log Determinism")]
        private static void VerifyMenu()
        {
            CampaignLogResult first = BuildFromMenu();
            CampaignLogResult second = first == null ? null : BuildFromMenu();

            if (first == null || second == null)
                return;

            bool same = string.Equals(first.Text, second.Text, StringComparison.Ordinal);
            Debug.Log($"[캠페인 로그] 재현성: 같은 콘텐츠로 두 번 만든 파일이 {(same ? "같다." : "다르다 !!")}");

            if (!same)
                Debug.LogError("[캠페인 로그] 결정적이지 않은 부분이 있다. 처음 달라지는 자리를 찾아 고친다.");
        }

        private static void ExportFromMenu(bool compress)
        {
            string folder = EditorUtility.OpenFolderPanel("캠페인 로그를 쓸 폴더(서버 저장소의 src/test/resources/fixtures)", DefaultFolder(), "");

            if (string.IsNullOrEmpty(folder))
                return;

            EditorPrefs.SetString(FolderKey, folder);
            string path = ExportTo(folder, compress);

            if (path != null)
                EditorUtility.DisplayDialog("캠페인 로그 내보내기", $"내보냈다.\n\n{path}\n\n서버 저장소에서 바뀐 파일을 커밋한다.", "확인");
        }

        // folder에 캠페인 로그를 쓰고 파일 경로를 돌려준다. 실패하거나 취소하면 null(콘솔 참고).
        // 같은 이름의 다른 확장자 파일(.json ↔ .json.gz)은 지운다. 서버 로더가 어느 쪽을 읽을지 헷갈리지 않게 하기 위해서다.
        public static string ExportTo(string folder, bool compress = false)
        {
            try
            {
                CampaignLogResult result = BuildFromMenu();

                if (result == null)
                    return null;

                Directory.CreateDirectory(folder);
                byte[] bytes = Utf8NoBom.GetBytes(result.Text);
                string path = Path.Combine(folder, compress ? GzipFileName : FileName);
                string stale = Path.Combine(folder, compress ? FileName : GzipFileName);

                if (compress)
                    bytes = Gzip(bytes);

                File.WriteAllBytes(path, bytes);

                if (File.Exists(stale))
                    File.Delete(stale);

                Debug.Log(result.Report + $"\n파일: {bytes.Length:N0}바이트 → {path}");

                if (!compress && bytes.Length > LargeFileBytes)
                    Debug.LogWarning($"[캠페인 로그] 파일이 {bytes.Length / 1024 / 1024}MB를 넘는다. 메뉴 'Export Campaign Log (gzip)...'로 압축해서 내보내는 것을 검토한다.");

                return path;
            }
            catch (Exception error)
            {
                Debug.LogError($"[캠페인 로그] 내보내지 못했다: {error}");
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // 메뉴가 쓰는 실행: 콘텐츠를 읽고 진행 막대를 띄우며 돌린다. 콘텐츠를 불러오지 못하거나 취소하면 null.
        private static CampaignLogResult BuildFromMenu()
        {
            LoadedContent loaded = SyncFixturesMenu.LoadContent(out string error);

            if (loaded == null)
            {
                Debug.LogError("[캠페인 로그] " + error);
                return null;
            }

            if (string.IsNullOrEmpty(loaded.ServerContentVersion))
            {
                Debug.LogError("[캠페인 로그] 서버 콘텐츠 버전을 계산하지 못했다.");
                return null;
            }

            try
            {
                return Build(loaded, index => EditorUtility.DisplayCancelableProgressBar(
                    "Sync Fixtures: 캠페인 로그", $"판 {index}", (float)(EditorApplication.timeSinceStartup % 2) / 2));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // 캠페인을 돌려 파일 글자를 만든다. onBattle이 true를 돌려주면 취소한다(결과는 null).
        // 에디터 도구(진행 막대 없이 쓰는 호출)와 검증이 부른다.
        public static CampaignLogResult Build(LoadedContent loaded, Func<int, bool> onBattle = null)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));

            GameContent content = loaded.Content;
            var progress = new ProgressState();
            var entries = new List<GoldenCampaignEntry>();
            long seq = 0;

            // 로그의 시각은 만드는 순간의 seq로 정한다(Next()가 seq를 올린 뒤에 생성기가 시계를 읽는다).
            var factory = new SyncLogFactory(new UnityJsonSerializer(), BuildVersion, loaded.ServerContentVersion, () => FirstLogUtc.AddSeconds(seq - 1));
            long Next() => ++seq;

            entries.Add(new GoldenCampaignEntry(factory.Reset(Next()), progress.Gold, progress.GrowthStage));

            var policy = new CampaignPolicy(content.Growth.MaxStage, StallBattles, BattleLimit, PostClearBattles);
            CampaignStopReason? stop = null;
            GoldenTimeSlack minSlack = null;
            int index = 0;

            // 판을 돌리다 멈추기 전의 진행도(끝나지 않은 판은 로그에 넣지 않으므로 파일의 final은 그 앞의 값이다).
            long finalGold = progress.Gold;
            int finalStage = progress.GrowthStage;

            while (stop == null)
            {
                index++;

                if (onBattle != null && onBattle(index))
                    return null;

                bool postClear = policy.NextIsPostClear;
                long goldBefore = progress.Gold;
                int stageBefore = progress.GrowthStage;
                UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, loaded.NodeTree);
                HeadlessBattleResult battle;

                try
                {
                    battle = HeadlessBattle.Run(content, progress, upgrades, new NearestEnemyBot(), SeedOf(index),
                        HeadlessBattle.DefaultFrameSeconds, MaxBattleSeconds);
                }
                catch (OverflowException)
                {
                    // 결산에서 Gold가 long을 넘었다(결산이 마지막 단계라 진행도는 바뀌지 않았다).
                    stop = CampaignStopReason.Overflow;
                    break;
                }

                if (battle.Capped)
                {
                    // 끝나지 않은 판은 결산 로그를 만들 수 없다. 진행도는 결산까지 바뀌었으므로 그 앞의 값으로 되돌려 쓴다.
                    finalGold = goldBefore;
                    finalStage = stageBefore;
                    stop = CampaignStopReason.Capped;
                    break;
                }

                // 칸별 장부가 판의 결과와 어긋나면 로그가 서버 검증을 통과하지 못한다. 골든 로그로 쓸 수 없다.
                if (battle.LedgerProblem != null)
                    throw new InvalidOperationException($"판 {index}의 처치 장부가 결과와 맞지 않는다: {battle.LedgerProblem}");

                string runId = RunIdOf(index);
                entries.Add(new GoldenCampaignEntry(factory.RunStart(Next(), runId, battle.StartInfo, battle.GoldBefore), goldBefore, stageBefore));
                entries.Add(new GoldenCampaignEntry(factory.Settlement(Next(), runId, battle.Raw), progress.Gold, progress.GrowthStage));

                GoldenTimeSlack slack = new(seq, TimeSlackOf(battle, upgrades, content));

                if (minSlack == null || slack.Seconds < minSlack.Seconds)
                    minSlack = slack;

                if (!postClear)
                {
                    foreach (PurchaseRecord purchase in CheapestFirstBuyer.BuyAll(progress, loaded.NodeTree))
                    {
                        entries.Add(new GoldenCampaignEntry(
                            factory.NodePurchase(Next(), purchase.NodeId, purchase.Rank, purchase.Cost), purchase.GoldAfter, progress.GrowthStage));
                    }
                }

                finalGold = progress.Gold;
                finalStage = progress.GrowthStage;
                stop = policy.AfterBattle(progress.GrowthStage, battle.Raw.ReachedMilestone);
            }

            var nodes = new List<(string NodeId, int Rank)>(progress.OwnedNodes.Count);

            foreach (string nodeId in progress.OwnedNodes)
                nodes.Add((nodeId, progress.RankOf(nodeId)));

            var meta = new GoldenCampaignMeta(policy.Battles, content.Growth.MaxStage, finalStage, policy.PostClearBattles, stop.Value);
            string text = GoldenCampaignBuilder.Build(loaded.ServerContentVersion, meta, entries, new GoldenFinalState(finalGold, finalStage, nodes), minSlack);

            var report = new StringBuilder();
            report.AppendLine($"[캠페인 로그] 콘텐츠 버전 {loaded.ServerContentVersion}");
            report.AppendLine($"  판 {meta.Battles}개(클리어 뒤 {meta.PostClearBattles}판), 로그 {entries.Count}개, 끝난 이유 {GoldenCampaignBuilder.StopReasonText(meta.StopReason)}");
            report.AppendLine($"  성장도 {meta.ReachedGrowthStage}/{meta.MaxGrowthStage}{(meta.Cleared ? "(클리어)" : "")}, 최종 Gold {finalGold:N0}, 산 노드 {nodes.Count}개");
            report.Append(minSlack == null
                ? "  시간 여유: 결산이 없다."
                : $"  시간 여유(S8 상한 − playedSeconds)의 최솟값: {minSlack.Seconds:F4}초(seq {minSlack.Seq}){(minSlack.Seconds < 0 ? " !! 음수다. 서버의 허용 오차(0.01초 + 상한의 0.01%)를 넘는지 본다." : "")}");

            return new CampaignLogResult(text, loaded.ServerContentVersion, report.ToString());
        }

        // 판 번호(1부터)로 정하는 시드. SyncFixturesMenu의 캠페인과 같은 식이다.
        public static int SeedOf(int battleIndex) => unchecked(1 * 100003 + battleIndex);

        // 판 번호로 만든 소문자 UUID(계약 3.8a): 00000000-0000-4000-8000-{판 번호 16진수 12자리}.
        public static string RunIdOf(int battleIndex) =>
            "00000000-0000-4000-8000-" + battleIndex.ToString("x12", CultureInfo.InvariantCulture);

        // 서버 S8의 상한(계획서 6.2, 서버 SettlementValidator)에서 playedSeconds를 뺀 값. 서버와 같은 순서·타입(double)으로 계산한다.
        // 상한 = 제한 시간 + (도달 Level − 시작 Level) × 성장 시간 + 시간 추가 횟수 × 시간 추가 1회의 초. 음수면 상한을 넘은 것이다.
        public static double TimeSlackOf(HeadlessBattleResult battle, UpgradeStatValues upgrades, GameContent content)
        {
            RunStartInfo start = battle.StartInfo;
            float growthTime = HqUpgradeStats.GrowthTimeFrom(upgrades);
            double bound = (double)start.TimeLimitSeconds
                + (double)(battle.Raw.ReachedLevel - start.StartLevel) * growthTime
                + (double)battle.Raw.TimeBonusCount * content.TimeLimit.KillTimeBonus;
            return bound - battle.Raw.PlayedSeconds;
        }

        private static byte[] Gzip(byte[] bytes)
        {
            using var output = new MemoryStream();

            using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(bytes, 0, bytes.Length);

            return output.ToArray();
        }

        // 지난번 폴더, 없으면 클라이언트 저장소 옆의 서버 저장소 픽스처 폴더, 그 상위(src/test/resources), 그것도 없으면 에셋 폴더.
        private static string DefaultFolder()
        {
            string saved = EditorPrefs.GetString(FolderKey, "");

            if (Directory.Exists(saved))
                return saved;

            string fixtures = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ServerFixturesPath));

            if (Directory.Exists(fixtures))
                return fixtures;

            string resources = Path.GetDirectoryName(fixtures);
            return Directory.Exists(resources) ? resources : Application.dataPath;
        }
    }

    // 캠페인 로그 한 번의 결과: 파일 글자, 콘텐츠 버전, 사람이 읽는 요약.
    internal sealed class CampaignLogResult
    {
        public string Text { get; }
        public string ContentVersion { get; }
        public string Report { get; }

        public CampaignLogResult(string text, string contentVersion, string report)
        {
            Text = text;
            ContentVersion = contentVersion;
            Report = report;
        }
    }
}
