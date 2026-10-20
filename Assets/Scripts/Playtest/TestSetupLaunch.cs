#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlackHole.Unity
{
    // 테스트 세팅 창(에디터)의 "▶ 플레이": 세팅을 맡겨 두고 플레이 모드에 들어가면, 개발 패널이 첫 Update에서 꺼내 판을 바로 시작한다.
    // 플레이 모드에 들어가며 도메인을 다시 불러도 남도록 SessionState(에디터를 끌 때까지 유지)에 JSON으로 둔다.
    internal static class TestSetupLaunch
    {
        private const string Key = "BlackHole.TestSetup.Launch";

        public static void Request(PlaytestScenario setup) => SessionState.SetString(Key, JsonUtility.ToJson(setup));

        // 맡긴 세팅을 꺼낸다(한 번만). 없거나 읽지 못하면 null.
        public static PlaytestScenario Take()
        {
            string json = SessionState.GetString(Key, string.Empty);
            SessionState.EraseString(Key);

            if (string.IsNullOrEmpty(json))
                return null;

            PlaytestScenario setup = PlaytestScenario.Parse(json, out string error);

            if (setup == null)
                Debug.LogError($"[테스트] 맡긴 세팅을 읽지 못했다: {error}");

            return setup;
        }
    }
}
#endif
