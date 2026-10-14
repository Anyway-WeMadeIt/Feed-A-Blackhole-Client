#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BlackHole.Unity
{
    // 진행 저장 폴더와 웹(브라우저) 저장소 동기화.
    // - 웹의 persistentDataPath는 /idbfs/<페이지 폴더 URL의 해시>다. itch.io는 빌드를 올릴 때마다 페이지 폴더 URL이 바뀌어
    //   새 빌드가 이전 저장을 찾지 못한다. 그래서 웹은 바뀌지 않는 고정 폴더(/idbfs/feed-a-blackhole)를 쓴다.
    // - 웹의 파일 쓰기는 먼저 메모리에만 남고, 브라우저 IndexedDB로 옮겨야(동기화) 새로고침·재방문 뒤에도 남는다.
    //   파일을 쓰거나 옮기거나 지운 뒤 Flush를 부른다. 동기화는 비동기라 끝나기 전에 탭을 닫으면 마지막 쓰기를 잃을 수 있다.
    // - 웹이 아니면 persistentDataPath를 그대로 쓰고 Flush는 아무것도 하지 않는다.
    public static class SaveLocation
    {
        private const string WebRoot = "/idbfs/feed-a-blackhole";

        public static string Root(string persistentDataPath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebRoot;
#else
            return persistentDataPath;
#endif
        }

        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BlackHoleSyncFileSystem();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Assets/Plugins/WebGL/BlackHoleFileSystem.jslib
        [DllImport("__Internal")]
        private static extern void BlackHoleSyncFileSystem();
#endif
    }
}
