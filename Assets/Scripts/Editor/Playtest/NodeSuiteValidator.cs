using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 현재 Unity 에셋으로 노드 단계 세팅(qa-nodes-1~8)의 판 조립과 초기 상태를 확인한다.
    // 에셋을 바꾸지 않는다. 결과는 PlaytestData/node-suite-validation.md(git 무시).
    internal static class NodeSuiteValidator
    {
        [MenuItem("BlackHole/QA/Validate Node Suite")]
        private static void Validate()
        {
            string[] setups = AssetDatabase.FindAssets("t:GameContentSetup");
            if (setups.Length != 1)
            {
                Debug.LogError("[노드 QA] GameContentSetup이 정확히 하나여야 합니다.");
                return;
            }
            var setup = AssetDatabase.LoadAssetAtPath<GameContentSetup>(AssetDatabase.GUIDToAssetPath(setups[0]));
            LoadedContent loaded = GameContentLoader.Load(setup);
            if (loaded == null) return;

            var diagnostics = new List<ContentDiagnostic>();
            string fingerprint = ContentFingerprint.Of(setup.ToData(), setup.Nodes.Content.Read(diagnostics));
            if (diagnostics.Count > 0)
            {
                Debug.LogError("[노드 QA] " + string.Join("; ", diagnostics));
                return;
            }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(root, PlaytestLibrary.ScenariosFolder);
            var text = new StringBuilder("# Unity 노드 단계 세팅 검사\n\n");
            text.AppendLine("- 콘텐츠 지문: " + fingerprint);
            text.AppendLine("- 판 조립·초기 상태 검사입니다. 실제 입력·화면·사운드·성능 검수는 별도로 실시합니다.\n");
            int passed = 0, failed = 0;
            var files = new List<string>(Directory.GetFiles(folder, "qa-*.json"));
            files.Sort(StringComparer.Ordinal);
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!name.StartsWith("qa-nodes-", StringComparison.Ordinal)) continue;
                text.AppendLine("## " + name + "\n");
                try
                {
                    var scenario = PlaytestScenario.Parse(File.ReadAllText(file), out string error);
                    if (scenario == null) throw new ArgumentException(error);
                    TestSetupReport report = TestSetupPreview.Build(scenario, loaded.Content, loaded.NodeTree);
                    if (!report.Succeeded || report.Errors.Count > 0 || report.Unreachable.Count > 0)
                    {
                        failed++;
                        text.AppendLine("실패: " + string.Join("; ", report.Errors) + "; 연결 누락: " + string.Join(", ", report.Unreachable));
                        continue;
                    }
                    passed++;
                    text.AppendLine($"통과: 노드 {report.OwnedNodes}, Rank {report.OwnedRanks}, 비용 {report.TotalCost:N0}\n");
                    foreach (string warning in report.Warnings) text.AppendLine("- 참고: " + warning);
                    foreach (TestSetupReport.Section section in report.Sections)
                    {
                        text.AppendLine("\n### " + section.Title + "\n");
                        foreach (var row in section.Rows) text.AppendLine("- " + row.Label + ": " + row.Value);
                    }
                }
                catch (Exception exception)
                {
                    failed++;
                    text.AppendLine("실패: " + exception.Message);
                }
                text.AppendLine();
            }
            text.Insert(0, $"검사 결과: 통과 {passed}, 실패 {failed}\n\n");
            string output = Path.Combine(root, "PlaytestData", "node-suite-validation.md");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, text.ToString());
            if (failed > 0) Debug.LogError($"[노드 QA] 통과 {passed}, 실패 {failed}. {output}");
            else Debug.Log($"[노드 QA] 통과 {passed}. {output}");
            EditorUtility.RevealInFinder(output);
        }

        [MenuItem("BlackHole/QA/Validate Node Suite", true)]
        private static bool CanValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
