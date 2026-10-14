using UnityEditor;
using UnityEngine;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 판 실행기 스모크 실행 창: BlackHole > Sync Fixtures > Run Window...
    // 테스터가 판 수, 판 상한, 막힘 기준을 정해 돌린다. 판 루프는 백그라운드에서 돌아 에디터가 멈추지 않고, 끝나면 보고서를 창에 보여 준다.
    // 값은 에디터를 다시 열어도 남는다(EditorPrefs).
    internal sealed class SyncFixturesWindow : EditorWindow
    {
        private const string Key = "BlackHole.SyncFixtures.";

        private int _battles = 30;
        private int _maxBattleSeconds = 600;
        private int _stallBattles = 300;
        private int _printEvery;
        private string _message = "";
        private Vector2 _scroll;
        private bool _wasRunning;

        [MenuItem("BlackHole/Sync Fixtures/Run Window...")]
        private static void Open() => GetWindow<SyncFixturesWindow>("Sync Fixtures").Show();

        private void OnEnable()
        {
            _battles = EditorPrefs.GetInt(Key + "Battles", 30);
            _maxBattleSeconds = EditorPrefs.GetInt(Key + "MaxBattleSeconds", 600);
            _stallBattles = EditorPrefs.GetInt(Key + "StallBattles", 300);
            _printEvery = EditorPrefs.GetInt(Key + "PrintEvery", 0);
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorPrefs.SetInt(Key + "Battles", _battles);
            EditorPrefs.SetInt(Key + "MaxBattleSeconds", _maxBattleSeconds);
            EditorPrefs.SetInt(Key + "StallBattles", _stallBattles);
            EditorPrefs.SetInt(Key + "PrintEvery", _printEvery);
        }

        // 돌고 있는 동안 진행 상황을 다시 그리고, 끝난 순간에도 한 번 그린다.
        private void Tick()
        {
            bool running = SyncFixturesMenu.Running;

            if (running || _wasRunning)
                Repaint();

            _wasRunning = running;
        }

        private void OnGUI()
        {
            bool running = SyncFixturesMenu.Running;

            using (new EditorGUI.DisabledScope(running))
            {
                _battles = Mathf.Max(1, EditorGUILayout.IntField(
                    new GUIContent("판 수", "돌릴 판의 최대 개수. 막힘 기준에 걸리거나 마지막 이정표에 닿으면 그전에 끝난다."), _battles));
                _maxBattleSeconds = Mathf.Max(1, EditorGUILayout.IntField(
                    new GUIContent("판 상한(초)", "판 하나의 상한(판 시간). 끝나지 않는 판을 이 시간에서 끊는다."), _maxBattleSeconds));
                _stallBattles = Mathf.Max(0, EditorGUILayout.IntField(
                    new GUIContent("막힘 기준(판)", "이정표에 닿지 못한 채 이만큼 판이 이어지면 멈춘다. 0이면 쓰지 않는다."), _stallBattles));
                _printEvery = Mathf.Max(0, EditorGUILayout.IntField(
                    new GUIContent("출력 간격(판)", "판 줄을 이 간격으로만 적는다(이정표에 닿은 판과 마지막 판은 늘 적는다). 0이면 판 수에 맞춰 자동."), _printEvery));
            }

            if (_stallBattles == 0 && _battles > 1000)
                EditorGUILayout.HelpBox("막힘 기준이 꺼져 있다. 봇이 마지막 이정표를 못 넘으면 Gold가 long 상한을 넘어 오류가 날 수 있다.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(running))
            {
                if (GUILayout.Button("실행", GUILayout.Height(24)))
                    Run();
            }

            using (new EditorGUI.DisabledScope(!running))
            {
                if (GUILayout.Button("취소", GUILayout.Height(24)))
                    SyncFixturesMenu.CancelRequested = true;
            }

            EditorGUILayout.EndHorizontal();

            if (running)
                EditorGUILayout.HelpBox(SyncFixturesMenu.Progress, MessageType.Info);
            else if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Warning);

            string report = SyncFixturesMenu.BackgroundReport;

            if (!running && !string.IsNullOrEmpty(report))
            {
                if (GUILayout.Button("보고서 복사"))
                    GUIUtility.systemCopyBuffer = report;

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                EditorGUILayout.SelectableLabel(report, EditorStyles.textArea, GUILayout.MinHeight(position.height - 170));
                EditorGUILayout.EndScrollView();
            }
        }

        private void Run()
        {
            // 판 수가 많으면 줄이 길어지므로 판 수에 맞춰 약 25줄만 적는다.
            int printEvery = _printEvery > 0 ? _printEvery : Mathf.Max(1, _battles / 25);
            _message = "";
            string message = SyncFixturesMenu.StartBackgroundSmoke(_battles, _maxBattleSeconds, printEvery, _stallBattles);

            if (!SyncFixturesMenu.Running)
                _message = message;
        }
    }
}
