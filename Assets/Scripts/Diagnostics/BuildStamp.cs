#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace BlackHole.Unity
{
    // 이 빌드가 어느 커밋에서 나왔는가. 성능 HUD 첫 줄과 CSV 파일 이름·요약에 쓴다(스크린샷만 봐도 빌드를 알 수 있게).
    // - 기기(개발 빌드): 빌드 직전에 BuildStampWriter(에디터)가 넣은 Resources/FabBuildStamp.txt를 읽는다.
    // - 에디터 플레이: 저장소의 .git 폴더를 직접 읽는다(git 명령이 PATH에 없어도 된다).
    // 에디터에서는 커밋 뒤에 '+'를 붙인다(아직 커밋하지 않은 변경이 섞여 있을 수 있다는 표시). 빌드는 변경 여부를 확인하지 않는다.
    internal static class BuildStamp
    {
        public const string ResourceName = "FabBuildStamp";

        private static bool _loaded;
        private static string _commit = "unknown";
        private static string _branch = "-";
        private static string _builtAt = "-";

        public static string Commit { get { Load(); return _commit; } }
        public static string Branch { get { Load(); return _branch; } }
        public static string BuiltAt { get { Load(); return _builtAt; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _loaded = false;

        private static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;

#if UNITY_EDITOR
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (root != null && GitHead.TryRead(root, out string commit, out string branch))
            {
                _commit = commit + "+";
                _branch = branch;
            }

            _builtAt = "editor";
#else
            TextAsset stamp = Resources.Load<TextAsset>(ResourceName);
            if (stamp == null)
                return;

            string[] lines = stamp.text.Split('\n');
            if (lines.Length > 0 && lines[0].Trim().Length > 0)
                _commit = lines[0].Trim();
            if (lines.Length > 1 && lines[1].Trim().Length > 0)
                _branch = lines[1].Trim();
            if (lines.Length > 2 && lines[2].Trim().Length > 0)
                _builtAt = lines[2].Trim();

            Resources.UnloadAsset(stamp);
#endif
        }
    }

#if UNITY_EDITOR
    // .git 폴더에서 HEAD 커밋과 브랜치를 읽는다. git 실행 파일 없이 동작한다(GitHub Desktop만 쓰는 환경 포함).
    // 워크트리(.git이 파일), packed-refs, 분리된 HEAD를 처리한다.
    internal static class GitHead
    {
        public static bool TryRead(string projectRoot, out string commit, out string branch)
        {
            commit = null;
            branch = "-";

            try
            {
                string gitDir = Path.Combine(projectRoot, ".git");

                if (File.Exists(gitDir))
                {
                    string pointer = File.ReadAllText(gitDir).Trim();
                    if (!pointer.StartsWith("gitdir:", StringComparison.Ordinal))
                        return false;

                    gitDir = Path.GetFullPath(Path.Combine(projectRoot, pointer.Substring("gitdir:".Length).Trim()));
                }

                string headPath = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(headPath))
                    return false;

                string head = File.ReadAllText(headPath).Trim();

                if (!head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    commit = Short(head);
                    branch = "(detached)";
                    return commit != null;
                }

                string refName = head.Substring("ref:".Length).Trim();
                const string HeadsPrefix = "refs/heads/";
                branch = refName.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? refName.Substring(HeadsPrefix.Length) : refName;

                // 워크트리면 refs는 공용 폴더(commondir)에 있다.
                string commonDir = gitDir;
                string commonPointer = Path.Combine(gitDir, "commondir");
                if (File.Exists(commonPointer))
                    commonDir = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonPointer).Trim()));

                foreach (string dir in new[] { gitDir, commonDir })
                {
                    string refPath = Path.Combine(dir, refName.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(refPath))
                    {
                        commit = Short(File.ReadAllText(refPath).Trim());
                        return commit != null;
                    }
                }

                string packed = Path.Combine(commonDir, "packed-refs");
                if (File.Exists(packed))
                {
                    foreach (string line in File.ReadAllLines(packed))
                    {
                        if (line.Length > 41 && line[40] == ' ' && line.Substring(41).Trim() == refName)
                        {
                            commit = Short(line.Substring(0, 40));
                            return commit != null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Perf] .git에서 커밋을 읽지 못했습니다: " + e.Message);
            }

            return false;
        }

        private static string Short(string hash) =>
            hash != null && hash.Length >= 7 ? hash.Substring(0, 7) : null;
    }
#endif
}
#endif
