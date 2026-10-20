using UnityEngine;

namespace BlackHole.Net.Unity
{
    // 게임 서버 설정 에셋(계약 2.3). GameBootstrap에 연결한다. 연결하지 않았거나 꺼 두면 항상 오프라인이다.
    // 주소 예는 AnalyticsSettings와 같다(에디터 http://localhost:8080, USB 연결 Android는 adb reverse).
    [CreateAssetMenu(fileName = "ServerSettings", menuName = "BlackHole/Server Settings")]
    public sealed class ServerSettings : ScriptableObject
    {
        [Tooltip("끄면 로그인하지 않고 항상 오프라인으로 실행한다.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("게임 서버 주소(경로 없이). 예: http://localhost:8080")]
        [SerializeField] private string _baseUrl = "http://localhost:8080";

        [Tooltip("인증 요청 하나를 기다리는 초.")]
        [Min(1)]
        [SerializeField] private int _requestTimeoutSeconds = 5;

        [Tooltip("앱 시작 시 로그인 전체(갱신 → 로그인 → 등록)에 쓰는 초. 넘으면 이번 실행은 오프라인이다.")]
        [Min(1)]
        [SerializeField] private int _loginBudgetSeconds = 8;

        [Tooltip("동기화 요청 하나를 기다리는 초.")]
        [Min(1)]
        [SerializeField] private int _syncTimeoutSeconds = 10;

        // 켜져 있고 주소가 있다.
        public bool Enabled => _enabled && !string.IsNullOrWhiteSpace(_baseUrl);
        public string BaseUrl => _baseUrl;
        public int RequestTimeoutSeconds => _requestTimeoutSeconds;
        public int LoginBudgetSeconds => _loginBudgetSeconds;
        public int SyncTimeoutSeconds => _syncTimeoutSeconds;
    }
}
