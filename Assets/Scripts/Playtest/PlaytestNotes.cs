#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace BlackHole.Unity
{
    // 느낌 메모 파일(한 줄에 JSON 하나, schema 1). 덧붙이기만 하고 고치거나 지우지 않는다.
    // - 에디터: <레포>/PlaytestData/notes.ndjson (git 무시). AI가 레포에서 바로 읽는다.
    // - 개발 빌드(폰): persistentDataPath/playtest/notes.ndjson. 꺼내는 길은 adb pull이다.
    // 플레이 메모 창과 개발 패널이 같은 함수로 쓰고 읽는다.
    internal static class PlaytestNotes
    {
        public const string DataFolderName = "PlaytestData";
        private const string FileName = "notes.ndjson";

        private static readonly System.Random Ids = new System.Random();
        private static DateTime _cachedWrite;
        private static long _cachedLength = -1;
        private static List<FeelNoteView> _cached = new List<FeelNoteView>();
        private static int _cachedSkipped;

#if UNITY_EDITOR
        // 레포 루트의 PlaytestData 폴더(Assets의 부모).
        public static string DataFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", DataFolderName));
#else
        public static string DataFolder => PlaytestFiles.Root;
#endif

        public static string FilePath => Path.Combine(DataFolder, FileName);

        // 파일이 바뀌었는지 볼 때 쓰는 값(마지막 쓰기 시각과 크기). 파일이 없으면 default.
        public static (DateTime Write, long Length) Stamp()
        {
            try
            {
                var file = new FileInfo(FilePath);
                return file.Exists ? (file.LastWriteTimeUtc, file.Length) : default;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return default;
            }
        }

        // 이 시나리오·프로필로 다음에 저장할 메모 이름(앞부분_번호). 번호는 같은 시나리오·프로필 메모 수 + 1이다
        // (이름에 적힌 가장 큰 번호보다 작아지지 않게 한다 — 파일에서 줄을 지웠어도 이름이 겹치지 않게).
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

            return $"{prefix}_{(Math.Max(count, largest) + 1).ToString("000", CultureInfo.InvariantCulture)}";
        }

        // 메모에 ID·저장 이름·시각·빌드를 채워 한 줄로 붙인다. 성공하면 ID를 돌려준다(저장 이름은 note.Name).
        public static bool TryAppend(FeelNote note, out string id, out string error)
        {
            note.AtUtc = DateTime.UtcNow;
            note.Id = FeelNote.NewId(note.AtUtc, Ids);
            note.Name = NextName(note.SetupName, note.Profile);
            note.BuildVersion = Application.version;
            id = note.Id;

            try
            {
                Directory.CreateDirectory(DataFolder);
                File.AppendAllText(FilePath, PlaytestJson.Write(note.ToJson()) + "\n");
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return false;
            }
        }

        // 모든 메모의 원문 JSON(파일 순서). AI 묶음이 화면용 FeelNoteView가 아니라 원문을 담는다.
        public static List<JsonObject> ReadRaw()
        {
            try
            {
                return File.Exists(FilePath)
                    ? PlaytestJson.ParseLines(File.ReadAllLines(FilePath), FeelNote.Schema, out _)
                    : new List<JsonObject>();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[메모] {FilePath}를 읽지 못했다: {exception.Message}");
                return new List<JsonObject>();
            }
        }

        // 모든 메모(파일 순서 = 적은 순서). 파일이 바뀌지 않았으면 다시 읽지 않는다. skipped는 읽지 못한 줄 수.
        public static IReadOnlyList<FeelNoteView> ReadAll(out int skipped)
        {
            skipped = _cachedSkipped;

            try
            {
                var file = new FileInfo(FilePath);

                if (!file.Exists)
                {
                    _cached = new List<FeelNoteView>();
                    _cachedLength = -1;
                    _cachedSkipped = 0;
                    skipped = 0;
                    return _cached;
                }

                if (file.LastWriteTimeUtc == _cachedWrite && file.Length == _cachedLength)
                    return _cached;

                _cached = FeelNotes.Parse(File.ReadAllLines(FilePath), out _cachedSkipped);
                _cachedWrite = file.LastWriteTimeUtc;
                _cachedLength = file.Length;
                skipped = _cachedSkipped;
                return _cached;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[메모] {FilePath}를 읽지 못했다: {exception.Message}");
                return _cached;
            }
        }
    }
}
#endif
