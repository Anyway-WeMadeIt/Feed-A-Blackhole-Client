using System;
using System.IO;
using BlackHole.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 개발 빌드 직전에 지금 커밋·브랜치·빌드 시각을 Resources 텍스트로 넣고, 빌드가 끝나면 지운다.
    // 기기의 성능 HUD 첫 줄과 CSV 파일 이름이 이 값을 쓴다(BuildStamp).
    // 빌드가 실패해 지우지 못하고 남아도 .gitignore에 있어 커밋되지 않고, 다음 빌드가 덮어쓴다.
    internal sealed class BuildStampWriter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string Folder = "Assets/Diagnostics.Generated";
        private const string ResourcesFolder = Folder + "/Resources";
        private static readonly string AssetPath = ResourcesFolder + "/" + BuildStamp.ResourceName + ".txt";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) == 0)
                return;

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string commit = "unknown";
            string branch = "-";

            if (root != null && GitHead.TryRead(root, out string headCommit, out string headBranch))
            {
                commit = headCommit;
                branch = headBranch;
            }

            Directory.CreateDirectory(ResourcesFolder);
            File.WriteAllText(AssetPath, commit + "\n" + branch + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("[Perf] 빌드 정보: " + commit + " " + branch);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }
    }
}
