#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 이번 실행의 테스트 도구 상태: 고른 밸런스 프로필과 적용 결과, 읽은 프로필·시나리오 목록.
    // 프로필은 앱 시작 때 콘텐츠를 불러오며 한 번 적용한다(GameBootstrap → GameContentLoader.Load(setup, Patch)).
    // 바꾸려면 고르고(PlayerPrefs) 장면을 다시 불러온다 — 판 조립이 시작 때 값을 굳히므로 중간에 바꾸지 않는다.
    internal sealed class PlaytestSession
    {
        public const string ProfileKey = "playtest.profile";

        private readonly PlaytestLibrary _library;

        public ContentTag Tag { get; }

        // 개발 패널 글꼴(PlaytestLibrary). 없으면 null.
        public Font Font => _library != null ? _library.Font : null;

        // 고른 프로필 이름(PlayerPrefs). 원본이면 "".
        public string SelectedProfile { get; }

        // 적용한 프로필. 원본이거나 적용하지 못했으면 null.
        public BalanceProfile AppliedProfile { get; private set; }

        // 고른 프로필을 적용하지 못한 이유. 없으면 null.
        public string ProfileProblem { get; private set; }

        // 수치 지문(ContentFingerprint). 원본은 프로필을 적용하기 전, Fingerprint는 이번 실행이 실제로 쓰는 값.
        // 콘텐츠를 불러오기 전에는 빈 글이다.
        public string BaseFingerprint { get; private set; } = string.Empty;
        public string Fingerprint { get; private set; } = string.Empty;
        public IReadOnlyList<string> ProfileErrors { get; private set; } = Array.Empty<string>();

        public List<ProfileOption> Profiles { get; } = new();
        public List<ScenarioOption> Scenarios { get; } = new();

        // 파일을 읽다 난 문제(폴더 접근 등). 개별 JSON 오류는 각 항목에 있다.
        public List<string> FileProblems { get; } = new();

        private PlaytestSession(PlaytestLibrary library, ContentTag tag, string selectedProfile)
        {
            _library = library;
            Tag = tag;
            SelectedProfile = selectedProfile;
        }

        public static PlaytestSession Create(PlaytestLibrary library, ContentTag tag)
        {
            var session = new PlaytestSession(library, tag, PlayerPrefs.GetString(ProfileKey, ""));
            session.Refresh();
            return session;
        }

        // 프로필·시나리오 파일을 다시 읽는다. 적용한 프로필은 바뀌지 않는다(다시 시작해야 바뀐다).
        public void Refresh()
        {
            FileProblems.Clear();
            Profiles.Clear();
            Scenarios.Clear();

            foreach (PlaytestFile file in PlaytestFiles.Profiles(_library, FileProblems))
            {
                BalanceProfile profile = BalanceProfile.Parse(file.Json, out string error);
                // 같은 이름은 나중에 읽은 것(기기 파일)이 이긴다.
                if (profile != null)
                    Profiles.RemoveAll(option => option.Profile != null && option.Profile.name == profile.name);

                Profiles.Add(new ProfileOption(file, profile, error));
            }

            foreach (PlaytestFile file in PlaytestFiles.Scenarios(_library, FileProblems))
            {
                PlaytestScenario scenario = PlaytestScenario.Parse(file.Json, out string error);

                if (scenario != null)
                    Scenarios.RemoveAll(option => option.Scenario != null && option.Scenario.name == scenario.name);

                Scenarios.Add(new ScenarioOption(file, scenario, error));
            }
        }

        // GameContentLoader의 패치(ContentPatch). 고른 프로필이 없으면 아무것도 하지 않는다.
        public IReadOnlyList<string> Patch(ContentData content, NodeContentData nodes)
        {
            BaseFingerprint = ContentFingerprint.Of(content, nodes);
            Fingerprint = BaseFingerprint;

            if (string.IsNullOrEmpty(SelectedProfile))
                return Array.Empty<string>();

            ProfileOption option = Profiles.Find(candidate => candidate.Profile != null && candidate.Profile.name == SelectedProfile);

            if (option == null)
            {
                ProfileProblem = $"고른 프로필 '{SelectedProfile}'을 찾지 못해 원본 값으로 시작했다.";
                Debug.LogWarning($"[테스트] {ProfileProblem}");
                return Array.Empty<string>();
            }

            IReadOnlyList<string> errors = BalanceProfilePatcher.Apply(option.Profile, content, nodes);

            if (errors.Count > 0)
            {
                ProfileProblem = $"프로필 '{SelectedProfile}'의 패치에 오류가 {errors.Count}개 있어 원본 값으로 시작했다.";
                ProfileErrors = errors;
                return errors;
            }

            AppliedProfile = option.Profile;
            Tag.SetProfile(option.Profile.name);
            Fingerprint = ContentFingerprint.Of(content, nodes);
            Debug.Log($"[테스트] 밸런스 프로필 '{option.Profile.name}'을 적용했다(값 {option.Profile.patches.Count}개, {option.File}).");
            return errors;
        }

        // 적용한 값이 콘텐츠 검사를 통과하지 못해 원본으로 다시 불러올 때 부른다.
        public void RejectProfile()
        {
            ProfileProblem = $"프로필 '{SelectedProfile}'을 적용한 값이 콘텐츠 검사를 통과하지 못해 원본 값으로 시작했다. 콘솔의 [콘텐츠] 오류를 본다.";
            AppliedProfile = null;
            Tag.SetProfile("");
            Fingerprint = BaseFingerprint;
        }

        // 진행 저장 폴더. 프로필을 적용했으면 프로필마다 따로 둔다.
        public string SaveDirectory(string persistentDataPath) =>
            AppliedProfile == null ? persistentDataPath : Path.Combine(PlaytestFiles.Saves, AppliedProfile.name);

        // 다음 시작 때 쓸 프로필을 고른다(""는 원본). 장면을 다시 불러와야 적용된다.
        public static void SelectProfile(string name)
        {
            PlayerPrefs.SetString(ProfileKey, name ?? "");
            PlayerPrefs.Save();
        }
    }

    internal sealed class ProfileOption
    {
        public PlaytestFile File { get; }
        public BalanceProfile Profile { get; }
        public string Error { get; }

        public ProfileOption(PlaytestFile file, BalanceProfile profile, string error)
        {
            File = file;
            Profile = profile;
            Error = error;
        }
    }

    internal sealed class ScenarioOption
    {
        public PlaytestFile File { get; }
        public PlaytestScenario Scenario { get; }
        public string Error { get; }

        public ScenarioOption(PlaytestFile file, PlaytestScenario scenario, string error)
        {
            File = file;
            Scenario = scenario;
            Error = error;
        }
    }
}
#endif
