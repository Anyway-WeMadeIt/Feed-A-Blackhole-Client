using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using BlackHole.Unity;
using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools
{
    // 수치 파일 감시(M2 수치 실시간 반영). 에디터를 켜면 늘 돈다.
    // - 대상: Assets/Data(게임 콘텐츠 세트가 가리키는 에셋과 노드 CSV), Assets/Playtest/Profiles, Assets/Playtest/Scenarios의 .csv·.asset·.json
    // - 디스크 바뀜을 FileSystemWatcher로 잡는다. 사람이 메모장으로 고쳤든, AI가 고쳤든, Unity가 저장했든 같다.
    // - 마지막 바뀜 뒤 0.3초 조용하면 한 번에 처리한다(git checkout처럼 한꺼번에 바뀌어도 한 번).
    // - 처리: 직접 가져오고(ImportAsset — Unity는 설정에 따라 플레이 중이나 창이 뒤에 있을 때 가져오지 않는다)
    //   → 판 수치가 바뀌었으면 게임과 같은 로더로 검사 → LiveDataSignal로 알린다.
    // 스크립트는 감시하지 않으므로 처리 중 컴파일은 일어나지 않는다.
    [InitializeOnLoad]
    internal static class LiveDataWatcher
    {
        private const double QuietSeconds = 0.3;

        private static readonly string[] Roots =
        {
            "Assets/Data",
            PlaytestLibrary.ProfilesFolder,
            PlaytestLibrary.ScenariosFolder,
        };

        private static readonly string[] Extensions = { ".csv", ".asset", ".json" };

        private static readonly ConcurrentQueue<string> Incoming = new ConcurrentQueue<string>();
        private static readonly List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        private static readonly HashSet<string> Batch = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string ProjectRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
        private static double _lastEvent;

        static LiveDataWatcher()
        {
            foreach (string root in Roots)
            {
                string full = Path.GetFullPath(root);

                if (!Directory.Exists(full))
                    continue;

                try
                {
                    var watcher = new FileSystemWatcher(full)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    };
                    watcher.Changed += OnFileEvent;
                    watcher.Created += OnFileEvent;
                    watcher.Deleted += OnFileEvent;
                    watcher.Renamed += OnRenamed;
                    watcher.EnableRaisingEvents = true;
                    Watchers.Add(watcher);
                }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is PlatformNotSupportedException)
                {
                    Debug.LogWarning($"[수치 감시] {root}를 감시하지 못한다: {error.Message}");
                }
            }

            EditorApplication.update += Drain;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        private static void Stop()
        {
            foreach (FileSystemWatcher watcher in Watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            Watchers.Clear();
            EditorApplication.update -= Drain;
        }

        // 감시 스레드에서 온다. 큐에만 넣고 처리는 메인 스레드(Drain)에서 한다.
        private static void OnFileEvent(object sender, FileSystemEventArgs e) => Incoming.Enqueue(e.FullPath);

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            Incoming.Enqueue(e.OldFullPath);
            Incoming.Enqueue(e.FullPath);
        }

        private static void Drain()
        {
            while (Incoming.TryDequeue(out string full))
            {
                string path = ToAssetPath(full);

                if (path == null || !Relevant(path))
                    continue;

                Batch.Add(path);
                _lastEvent = EditorApplication.timeSinceStartup;
            }

            if (Batch.Count == 0 || EditorApplication.timeSinceStartup - _lastEvent < QuietSeconds)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            var paths = new List<string>(Batch);
            paths.Sort(StringComparer.Ordinal);
            Batch.Clear();
            Process(paths);
        }

        private static string ToAssetPath(string full)
        {
            string path = Path.GetFullPath(full).Replace('\\', '/');
            return path.StartsWith(ProjectRoot, StringComparison.OrdinalIgnoreCase) ? path.Substring(ProjectRoot.Length) : null;
        }

        // .meta와 편집기의 임시 파일(.csv~, .tmp 등)은 뺀다.
        private static bool Relevant(string path)
        {
            foreach (string extension in Extensions)
            {
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void Process(List<string> paths)
        {
            bool structure = false;

            foreach (string path in paths)
            {
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                else
                    structure = true;
            }

            // 지워졌거나 이름이 바뀐 파일: 에셋 목록을 다시 맞춘다.
            if (structure)
                AssetDatabase.Refresh();

            HashSet<string> content = ContentPaths();
            bool contentChanged = paths.Exists(path =>
                content.Contains(path) || path.StartsWith(PlaytestLibrary.ProfilesFolder + "/", StringComparison.Ordinal));

            LiveDataChange change = contentChanged
                ? Validate(paths)
                : new LiveDataChange(paths, false, true, Array.Empty<string>(), null, null);

            if (contentChanged)
            {
                if (change.Valid)
                    Debug.Log($"[수치 감시] 바뀜: {change.Files} · 지문 {change.Fingerprint}");
                else
                    Debug.LogWarning($"[수치 감시] 바뀐 값이 검사를 통과하지 못했다({change.Files}): 오류 {change.Errors.Count}개");
            }

            LiveDataSignal.Raise(change);
        }

        // 판 수치가 사는 에셋: 게임 콘텐츠 세트가 가리키는 것 전부(에셋 참조를 따라간다. 노드 CSV 포함).
        private static HashSet<string> ContentPaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameContentSetup)))
            {
                foreach (string dependency in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true))
                    paths.Add(dependency);
            }

            return paths;
        }

        // 새 값을 게임과 같은 로더로 불러 본다(고른 프로필 포함). 로더가 콘솔에 남기는 오류를 모은다.
        private static LiveDataChange Validate(IReadOnlyList<string> paths)
        {
            var errors = new List<string>();
            GameContentSetup setup = FindAsset<GameContentSetup>();

            if (setup == null)
            {
                errors.Add("게임 콘텐츠 세트(GameContentSetup) 에셋을 찾지 못했다.");
                return new LiveDataChange(paths, true, false, errors, null, null);
            }

            PlaytestSession session = PlaytestSession.Create(FindAsset<PlaytestLibrary>(), new ContentTag());

            void Collect(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                    errors.Add(message);
            }

            LoadedContent loaded;
            Application.logMessageReceived += Collect;

            try
            {
                loaded = GameContentLoader.Load(setup, session.Patch);
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            if (session.ProfileProblem != null)
                errors.Insert(0, session.ProfileProblem);

            bool valid = loaded != null && session.ProfileProblem == null;
            string profile = session.AppliedProfile != null ? session.AppliedProfile.name : string.Empty;
            return new LiveDataChange(paths, true, valid, errors, valid ? session.Fingerprint : null, profile);
        }

        private static T FindAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
