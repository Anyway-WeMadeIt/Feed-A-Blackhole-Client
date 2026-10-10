#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace BlackHole.Unity
{
    // 느낌 메모 파일(schema 1). 메모 하나 = 파일 하나: <DataFolder>/notes/<저장 이름>.json (예: qa-nodes-3_golden-x5_004.json).
    // 저장 이름은 시나리오_프로필_번호이고, 같은 시나리오·프로필로 저장할 때마다 번호가 하나씩 오른다.
    // - 에디터: <레포>/PlaytestData/notes/ (git 무시).
    // - 개발 빌드(폰): persistentDataPath/playtest/notes/. 꺼내는 길은 adb pull이다.
    // 플레이 메모 창과 개발 패널이 같은 함수로 쓰고 읽는다. 쓴 파일은 고치거나 지우지 않는다(사람이 지워도 된다).
    internal static class PlaytestNotes
    {
        public const string DataFolderName = "PlaytestData";
        private const string NotesFolderName = "notes";

        private static readonly System.Random Ids = new System.Random();
        private static (DateTime Write, long Length) _cachedStamp;
        private static bool _cacheValid;
        private static List<FeelNoteView> _cached = new List<FeelNoteView>();
        private static int _cachedSkipped;

#if UNITY_EDITOR
        // 레포 루트의 PlaytestData 폴더(Assets의 부모).
        public static string DataFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", DataFolderName));
#else
        public static string DataFolder => PlaytestFiles.Root;
#endif

        // 메모 파일이 쌓이는 폴더.
        public static string NotesFolder => Path.Combine(DataFolder, NotesFolderName);

        public static string PathOf(string name) => Path.Combine(NotesFolder, name + ".json");

        // 폴더가 바뀌었는지 볼 때 쓰는 값(가장 최근 쓰기 시각, 파일 크기 합 + 파일 수). 폴더가 없으면 default.
        public static (DateTime Write, long Length) Stamp()
        {
            try
            {
                var folder = new DirectoryInfo(NotesFolder);
                if (!folder.Exists)
                    return default;

                DateTime latest = default;
                long total = 0;
                foreach (FileInfo file in folder.EnumerateFiles("*.json"))
                {
                    if (file.LastWriteTimeUtc > latest)
                        latest = file.LastWriteTimeUtc;
                    total += file.Length + 1;
                }

                return (latest, total);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return default;
            }
        }

        // 이 시나리오·프로필로 다음에 저장할 메모 이름(앞부분_번호). 번호는 같은 시나리오·프로필 메모 수 + 1이다
        // (이름에 적힌 가장 큰 번호보다 작아지지 않고, 이미 있는 파일 이름과 겹치지 않게 한다).
        public static string NextName(string setupName, string profile)
        {
            string prefix = FeelNotes.NamePrefix(setupName, profile);
            int count = 0;
            int largest = 0;

            foreach (FeelNoteView note in ReadAll(out _))
            {
                if (!FeelNotes.SameGroup(note, setupName, profile))
                    continue;

                count++;

                if (note.Name != null && note.Name.Length > prefix.Length + 1 && note.Name.StartsWith(prefix + "_", StringComparison.Ordinal)
                    && int.TryParse(note.Name.Substring(prefix.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                    largest = Math.Max(largest, number);
            }

            int next = Math.Max(count, largest) + 1;
            while (File.Exists(PathOf(NameOf(prefix, next))))
                next++;

            return NameOf(prefix, next);
        }

        private static string NameOf(string prefix, int number) =>
            $"{prefix}_{number.ToString("000", CultureInfo.InvariantCulture)}";

        // 메모에 ID·저장 이름·시각·빌드를 채워 <저장 이름>.json으로 쓴다. 저장 이름은 note.Name.
        public static bool TrySave(FeelNote note, out string error)
        {
            note.AtUtc = DateTime.UtcNow;
            note.Id = FeelNote.NewId(note.AtUtc, Ids);
            note.BuildVersion = Application.version;

            try
            {
                Directory.CreateDirectory(NotesFolder);
                note.Name = NextName(note.SetupName, note.Profile);
                File.WriteAllText(PathOf(note.Name), PlaytestJson.Write(note.ToJson(), true) + "\n");
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return false;
            }
        }

        // 모든 메모(적은 시각 순). 폴더가 바뀌지 않았으면 다시 읽지 않는다. skipped는 읽지 못한 파일 수.
        public static IReadOnlyList<FeelNoteView> ReadAll(out int skipped)
        {
            (DateTime Write, long Length) stamp = Stamp();

            if (_cacheValid && stamp == _cachedStamp)
            {
                skipped = _cachedSkipped;
                return _cached;
            }

            var notes = new List<FeelNoteView>();
            skipped = 0;

            try
            {
                if (Directory.Exists(NotesFolder))
                {
                    foreach (string path in Directory.GetFiles(NotesFolder, "*.json"))
                    {
                        FeelNoteView view = FeelNotes.ParseFile(File.ReadAllText(path));
                        if (view == null)
                        {
                            skipped++;
                            continue;
                        }

                        // 이름은 파일 이름이 기준이다(사람이 파일 이름을 바꿨으면 그 이름).
                        view.Name = Path.GetFileNameWithoutExtension(path);
                        notes.Add(view);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[메모] {NotesFolder}를 읽지 못했다: {exception.Message}");
                skipped = _cachedSkipped;
                return _cached;
            }

            notes.Sort((a, b) =>
            {
                int byTime = string.CompareOrdinal(a.AtUtc, b.AtUtc);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.Name, b.Name);
            });

            _cached = notes;
            _cachedSkipped = skipped;
            _cachedStamp = stamp;
            _cacheValid = true;
            return _cached;
        }
    }
}
#endif
