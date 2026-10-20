using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // 테스트 도구가 쓰는 밸런스 프로필·시나리오 JSON 목록. GameBootstrap에 연결한다.
    // 파일은 Assets/Playtest/Profiles, Assets/Playtest/Scenarios에 두고, 에셋 ⋮ 메뉴 > "폴더에서 다시 모으기"로 목록을 채운다.
    // 개발 빌드는 기기의 persistentDataPath/playtest/{profiles,scenarios}도 함께 읽는다(PlaytestFiles).
    // 테스트 도구는 에디터·개발 빌드에만 있지만, 장면이 이 에셋을 가리키므로 클래스는 릴리스에도 남는다(쓰지 않는다).
    [CreateAssetMenu(fileName = "PlaytestLibrary", menuName = "BlackHole/Playtest Library")]
    public sealed class PlaytestLibrary : ScriptableObject
    {
        public const string ProfilesFolder = "Assets/Playtest/Profiles";
        public const string ScenariosFolder = "Assets/Playtest/Scenarios";

        [SerializeField] private TextAsset[] _profiles = Array.Empty<TextAsset>();
        [SerializeField] private TextAsset[] _scenarios = Array.Empty<TextAsset>();
        [Tooltip("개발 패널 글꼴. 비워 두면 기본 글꼴이고, 한글은 OS 대체 글꼴로 그린다. 기기에서 한글이 네모로 보이면 한글 TTF를 넣는다.")]
        [SerializeField] private Font _font;

        public IReadOnlyList<TextAsset> Profiles => _profiles;
        public IReadOnlyList<TextAsset> Scenarios => _scenarios;
        public Font Font => _font;

#if UNITY_EDITOR
        // 에셋 인스펙터의 ⋮ 메뉴: 두 폴더의 JSON을 이름 순으로 다시 모은다. 빌드 전에도 자동으로 부른다(PlaytestLibraryBuildStep).
        [ContextMenu("폴더에서 다시 모으기")]
        internal void Collect()
        {
            _profiles = CollectFrom(ProfilesFolder);
            _scenarios = CollectFrom(ScenariosFolder);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[테스트] 프로필 {_profiles.Length}개, 시나리오 {_scenarios.Length}개를 모았다.", this);
        }

        private static TextAsset[] CollectFrom(string folder)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                return Array.Empty<TextAsset>();

            var assets = new List<TextAsset>();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

                // AI 초안(ai-draft.json)은 작업 중인 파일이라 빌드에 넣지 않는다(git도 무시한다).
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith("/ai-draft.json", StringComparison.OrdinalIgnoreCase))
                    assets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            }

            assets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return assets.ToArray();
        }
#endif
    }
}
