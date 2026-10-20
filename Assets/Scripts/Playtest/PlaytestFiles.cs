#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlackHole.Unity
{
    // 테스트 도구가 읽고 쓰는 파일.
    // - 프로필·시나리오 JSON: 에디터는 Assets/Playtest 폴더를 바로 읽는다(모으기 없이 새 파일이 보인다).
    //   개발 빌드는 PlaytestLibrary에 모은 에셋을 읽는다. 둘 다 기기 폴더(persistentDataPath/playtest/...)도 읽고,
    //   같은 이름이면 기기 파일이 이긴다 — 빌드 없이 폰에 JSON만 넣어(adb push) 값을 바꿔 볼 수 있다.
    // - 프로필별 저장: persistentDataPath/playtest/saves/<프로필 이름>
    // - 느낌 메모: PlaytestNotes(에디터는 레포 PlaytestData/, 개발 빌드는 Root 아래)
    internal static class PlaytestFiles
    {
        public const string AssetSource = "에셋";
        public const string DeviceSource = "기기";

        public static string Root => Path.Combine(Application.persistentDataPath, "playtest");
        public static string DeviceProfiles => Path.Combine(Root, "profiles");
        public static string DeviceScenarios => Path.Combine(Root, "scenarios");
        public static string Saves => Path.Combine(Root, "saves");

        public static List<PlaytestFile> Profiles(PlaytestLibrary library, List<string> problems) =>
            Collect(library != null ? library.Profiles : null, PlaytestLibrary.ProfilesFolder, DeviceProfiles, problems);

        public static List<PlaytestFile> Scenarios(PlaytestLibrary library, List<string> problems) =>
            Collect(library != null ? library.Scenarios : null, PlaytestLibrary.ScenariosFolder, DeviceScenarios, problems);

        private static List<PlaytestFile> Collect(
            IReadOnlyList<TextAsset> assets,
            string assetFolder,
            string deviceFolder,
            List<string> problems)
        {
            var files = new List<PlaytestFile>();
#if UNITY_EDITOR
            // 에디터의 현재 폴더는 프로젝트 루트다.
            ReadFolder(Path.GetFullPath(assetFolder), AssetSource, files, problems);
#else
            if (assets != null)
            {
                foreach (TextAsset asset in assets)
                {
                    if (asset != null)
                        files.Add(new PlaytestFile(asset.name + ".json", AssetSource, asset.text));
                }
            }
#endif
            ReadFolder(deviceFolder, DeviceSource, files, problems);
            return files;
        }

        private static void ReadFolder(string folder, string source, List<PlaytestFile> into, List<string> problems)
        {
            if (!Directory.Exists(folder))
                return;

            try
            {
                string[] paths = Directory.GetFiles(folder, "*.json");
                Array.Sort(paths, StringComparer.Ordinal);

                foreach (string path in paths)
                    into.Add(new PlaytestFile(Path.GetFileName(path), source, File.ReadAllText(path)));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                problems.Add($"{source} 폴더를 읽지 못했다({folder}): {error.Message}");
            }
        }
    }

    // 읽은 JSON 파일 하나.
    internal sealed class PlaytestFile
    {
        public string FileName { get; }
        public string Source { get; }
        public string Json { get; }

        public PlaytestFile(string fileName, string source, string json)
        {
            FileName = fileName;
            Source = source;
            Json = json;
        }

        public override string ToString() => $"{Source}/{FileName}";
    }
}
#endif
