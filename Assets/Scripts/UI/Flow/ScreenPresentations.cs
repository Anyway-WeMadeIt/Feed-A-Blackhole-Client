using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // 화면마다 쓰는 Presentation 묶음. 어느 화면에 어느 Presentation을 쓰는지는 화면 흐름(ScreenFlow)이 정한다.
    // GameBootstrap은 이것 하나만 받아 화면 흐름에 넘긴다. 모든 칸이 필수다 — 바꿀 것이 없는 화면도 빈 Presentation 에셋을 연결한다.
    [CreateAssetMenu(fileName = "ScreenPresentations", menuName = "BlackHole/Screen Presentations")]
    public sealed class ScreenPresentations : ScriptableObject
    {
        [Header("Root")]
        [SerializeField] private UIPresentationSpec _title;
        [SerializeField] private UIPresentationSpec _upgrade;
        [SerializeField] private UIPresentationSpec _battle;
        [SerializeField] private UIPresentationSpec _settlement;

        [Header("Page")]
        [Tooltip("업그레이드 화면 안의 노드 트리 페이지.")]
        [SerializeField] private UIPresentationSpec _nodeTree;

        [Header("Panel")]
        [SerializeField] private UIPresentationSpec _modeSelect;
        [SerializeField] private UIPresentationSpec _settings;
        [SerializeField] private UIPresentationSpec _pause;
        [SerializeField] private UIPresentationSpec _confirm;

        public UIPresentationSpec Title => _title;
        public UIPresentationSpec Upgrade => _upgrade;
        public UIPresentationSpec Battle => _battle;
        public UIPresentationSpec Settlement => _settlement;
        public UIPresentationSpec NodeTree => _nodeTree;
        public UIPresentationSpec ModeSelect => _modeSelect;
        public UIPresentationSpec Settings => _settings;
        public UIPresentationSpec Pause => _pause;
        public UIPresentationSpec Confirm => _confirm;

        // 연결하지 않은 칸. 비어 있어야 화면 흐름을 조립한다(GameBootstrap).
        internal IReadOnlyList<string> MissingReferences()
        {
            var missing = new List<string>();

            if (_title == null) missing.Add("Title");
            if (_upgrade == null) missing.Add("Upgrade");
            if (_battle == null) missing.Add("Battle");
            if (_settlement == null) missing.Add("Settlement");
            if (_nodeTree == null) missing.Add("Node Tree");
            if (_modeSelect == null) missing.Add("Mode Select");
            if (_settings == null) missing.Add("Settings");
            if (_pause == null) missing.Add("Pause");
            if (_confirm == null) missing.Add("Confirm");

            return missing;
        }
    }
}
