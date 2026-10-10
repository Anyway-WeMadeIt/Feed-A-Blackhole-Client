#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BlackHole.Unity
{
    // 밸런스 프로필: 콘텐츠 값을 덮어쓰는 패치 묶음(JSON). 원본 에셋은 건드리지 않는다.
    // 경로 문법과 적용은 BalanceProfilePatcher가 맡는다. 이름은 그대로 전투 요약의 contentVersion이 된다(ContentTag).
    // 예: { "name": "golden-x5", "note": "황금 기본 배율 x50 -> x5", "patches": [ { "path": "enemy/asteroid/trait/golden/multiplier", "value": 5 } ] }
    // 패치마다 이유와 근거 메모를 선택적으로 남길 수 있다.
    [Serializable]
    internal sealed class BalanceProfile
    {
        // contentVersion은 64자까지다. 테스트 표시(+test)가 붙을 자리를 남긴다.
        public const int MaxNameLength = 58;

        private static readonly Regex NamePattern = new("^[A-Za-z0-9._-]+$");

        // 필드 이름이 곧 JSON 키다.
        public string name;
        public string note;
        public List<Patch> patches = new();

        [Serializable]
        public sealed class Patch
        {
            public string path;
            public double value;
            // 왜 바꾸나(한 줄). 승격하면 변경 기록에 남는다.
            public string reason;
            // 근거가 된 느낌 메모 ID(n-…).
            public List<string> noteIds = new();
        }

        // JSON을 읽는다. 형식이 틀리면 null이고 error에 이유가 있다.
        public static BalanceProfile Parse(string json, out string error)
        {
            BalanceProfile profile;

            try
            {
                profile = JsonUtility.FromJson<BalanceProfile>(json);
            }
            catch (ArgumentException exception)
            {
                error = $"JSON을 읽지 못했다: {exception.Message}";
                return null;
            }

            if (profile == null)
            {
                error = "빈 JSON이다.";
                return null;
            }

            if (string.IsNullOrEmpty(profile.name) || profile.name.Length > MaxNameLength || !NamePattern.IsMatch(profile.name))
            {
                error = $"name은 영문·숫자·. _ - 로 {MaxNameLength}자까지다. 받은 값: '{profile.name}'.";
                return null;
            }

            if (profile.patches == null || profile.patches.Count == 0)
            {
                error = "patches가 비어 있다.";
                return null;
            }

            error = null;
            return profile;
        }
    }
}
#endif
