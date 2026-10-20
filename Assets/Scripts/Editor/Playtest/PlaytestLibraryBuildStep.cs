using BlackHole.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BlackHole.EditorTools
{
    // 빌드 전에 테스트 도구 목록(PlaytestLibrary)을 폴더에서 다시 모은다. 모으기를 잊어도 개발 빌드에 새 JSON이 들어간다.
    internal sealed class PlaytestLibraryBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(PlaytestLibrary)))
            {
                var library = AssetDatabase.LoadAssetAtPath<PlaytestLibrary>(AssetDatabase.GUIDToAssetPath(guid));

                if (library != null)
                    library.Collect();
            }

            AssetDatabase.SaveAssets();
        }
    }
}
