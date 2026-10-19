using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 업그레이드 트리의 모양: 스탯마다의 노드 그림(BlackHoleGUI Stat Nodes)과 선·가격 글자의 색.
    // 노드는 Rank 1 첫 효과의 StatId(UpgradeStats 시트)로 그림을 찾는다. 모르는 스탯은 기본 그림을 쓴다.
    // 보이는 단계: 산 노드 → 그 이웃(살 수 있음·Gold 모자람, 노드 그림을 조금 어둡게) → 열린 구역의 닿지 않은 노드(자물쇠)
    //   → 먼 구역의 닿지 않은 노드(흐린 점). 열린 구역은 산 노드나 시작 노드가 든 구역이다(NodeTreeView가 가린다).
    //   보이는 노드에서 선으로 LockAhead칸 안의 노드는 구역이 닫혀 있어도 자물쇠다(다음 구역 입구 너머 미리 보기).
    // 트리 보기(NodeTreeView)가 들고 있고, 비어 있으면 트리 보기는 예전처럼 색 칸으로 그린다.
    [CreateAssetMenu(fileName = "NodeTreeLook", menuName = "BlackHole/Node Tree Look")]
    public sealed class NodeTreeLook : ScriptableObject
    {
        [Serializable]
        public sealed class NodeSprites
        {
            [Tooltip("스탯 키. 예: breaker.damage")]
            public string stat;
            [Tooltip("살 수 있음. 손을 올려도(PC) 이 그림이고, 둘레에 링이 생긴다(NodeTreeView).")]
            public Sprite available;
            [Tooltip("샀음.")]
            public Sprite purchased;
            [Tooltip("잠김: 내용 대신 자물쇠. 모든 노드가 공용 자물쇠 그림 하나를 쓴다.")]
            public Sprite locked;
        }

        [Tooltip("스탯을 모르거나 그 상태의 그림이 비었을 때 쓰는 그림.")]
        [SerializeField] private NodeSprites _fallback = new NodeSprites();
        [SerializeField] private List<NodeSprites> _stats = new List<NodeSprites>();

        [Header("지금 살 수 있는 노드(Purchasable)")]
        [Tooltip("살 수 있음 그림에 곱하는 색. 산 노드보다 조금 어둡게. 손을 올리면 원래 밝기로.")]
        [SerializeField] private Color _purchasableTint = new Color(0.88f, 0.88f, 0.88f, 1);

        [Header("보이지만 Gold가 모자란 노드(Revealed)")]
        [Tooltip("살 수 있음 그림에 곱하는 색. 살 수 있는 노드보다 더 어둡게.")]
        [SerializeField] private Color _revealedTint = new Color(0.68f, 0.68f, 0.68f, 1);
        [Tooltip("켜면 흐린 그림 대신 자물쇠(locked) 그림을 쓴다. 무엇인지 숨기고 싶을 때.")]
        [SerializeField] private bool _revealedAsLocked;

        [Header("아직 닿지 않은 노드(Hidden) — 안개")]
        [Tooltip("켜면 아직 닿지 않은 노드도 보인다(열린 구역은 자물쇠, 먼 구역은 흐린 점). 끄면 원작처럼 이웃을 사야 나타난다.")]
        [SerializeField] private bool _showHidden = true;
        [Tooltip("열린 구역(산 노드나 시작 노드가 든 구역)의 닿지 않은 노드: 자물쇠(locked) 그림에 곱하는 색. 올리면 이름만 보인다.")]
        [SerializeField] private Color _lockedTint = new Color(1, 1, 1, 0.6f);
        [Tooltip("보이는 노드(산 노드와 다음 노드)에서 선으로 이 칸 수 안의 닿지 않은 노드는 구역이 닫혀 있어도 자물쇠로 보인다. 0이면 구역만 본다.")]
        [Min(0)]
        [SerializeField] private int _lockAhead = 1;
        [Tooltip("먼 구역(아직 열리지 않은 구역)의 노드를 그리는 점. 비우면 기본 그림의 locked를 쓴다. 올림·누름을 받지 않는다.")]
        [SerializeField] private Sprite _farSprite;
        [Tooltip("먼 노드 점의 크기(노드 크기에 대한 비율).")]
        [Range(0.1f, 1)]
        [SerializeField] private float _farScale = 0.3f;
        [Tooltip("먼 노드 점에 곱하는 색. 알파를 낮춰 흐리게.")]
        [SerializeField] private Color _farTint = new Color(1, 1, 1, 0.35f);
        [Tooltip("안개 속 선의 색(알파 포함). 한쪽 끝이라도 닿지 않은 노드인 선.")]
        [SerializeField] private Color _hiddenLinkColor = new Color(0.722f, 0.729f, 0.725f, 0.18f);

        [Header("선")]
        [Tooltip("양 끝을 모두 샀다.")]
        [SerializeField] private Color _ownedLinkColor = new Color(0.341f, 0.592f, 0.412f);
        [Tooltip("한쪽을 샀고 다른 쪽을 지금 살 수 있다.")]
        [SerializeField] private Color _openLinkColor = new Color(0.125f, 0.129f, 0.141f);
        [Tooltip("그 밖(Gold가 모자람 등).")]
        [SerializeField] private Color _lockedLinkColor = new Color(0.722f, 0.729f, 0.725f);

        [Header("가격 글자")]
        [SerializeField] private Color _priceColor = new Color(0.125f, 0.129f, 0.141f);
        [SerializeField] private Color _unaffordablePriceColor = new Color(0.42f, 0.435f, 0.424f);
        [Tooltip("가격 딱지의 바탕. 선이 지나가도 가격이 읽히게 한다.")]
        [SerializeField] private Color _priceBackColor = new Color(0.953f, 0.945f, 0.91f);

        public Color PriceBackColor => _priceBackColor;

        private Dictionary<string, NodeSprites> _byStat;

        // 아직 닿지 않은 노드를 안개로 보이는가.
        public bool ShowHidden => _showHidden;

        public Color HiddenLinkColor => _hiddenLinkColor;

        // 먼 노드(열리지 않은 구역의 안개 속 노드)의 점: 그림, 곱하는 색, 크기 비율.
        public Sprite FarSprite => _farSprite != null ? _farSprite : _fallback.locked;
        public Color FarTint => _farTint;
        public float FarScale => _farScale;
        // 보이는 노드에서 선으로 이 칸 수 안의 닿지 않은 노드는 자물쇠(구역과 상관없이).
        public int LockAhead => _lockAhead;

        // 노드 그림. 손을 올린 노드도 그림은 같다 — 올림은 링과 밝기로 보인다(NodeTreeView).
        public Sprite SpriteOf(string stat, NodeState state)
        {
            NodeSprites sprites = Find(stat);

            switch (state)
            {
                case NodeState.Owned:
                    return Pick(sprites?.purchased, _fallback.purchased);
                case NodeState.Hidden:
                case NodeState.Revealed when _revealedAsLocked:
                    return Pick(sprites?.locked, _fallback.locked);
                default:
                    return Pick(sprites?.available, _fallback.available);
            }
        }

        // 그림에 곱하는 색. 산 노드는 그대로, 다음 노드는 조금 어둡게(손을 올린 살 수 있는 노드는 그대로, Gold가 모자란 노드는 더),
        // 열린 구역의 자물쇠는 흐리게. 먼 노드의 점은 FarTint.
        public Color TintOf(NodeState state, bool hovered)
        {
            switch (state)
            {
                case NodeState.Owned:
                    return Color.white;
                case NodeState.Purchasable:
                    return hovered ? Color.white : _purchasableTint;
                case NodeState.Revealed:
                    return _revealedAsLocked ? Color.white : _revealedTint;
                default:
                    return _lockedTint;
            }
        }

        public Color LinkColorOf(NodeState a, NodeState b)
        {
            if (a == NodeState.Owned && b == NodeState.Owned)
                return _ownedLinkColor;

            bool open = (a == NodeState.Owned && b == NodeState.Purchasable) || (b == NodeState.Owned && a == NodeState.Purchasable);
            return open ? _openLinkColor : _lockedLinkColor;
        }

        public Color PriceColorOf(NodeState state) =>
            state == NodeState.Purchasable ? _priceColor : _unaffordablePriceColor;

        private static Sprite Pick(Sprite sprite, Sprite fallback) => sprite != null ? sprite : fallback;

        private NodeSprites Find(string stat)
        {
            if (string.IsNullOrEmpty(stat))
                return null;

            if (_byStat == null)
            {
                _byStat = new Dictionary<string, NodeSprites>(StringComparer.Ordinal);
                foreach (NodeSprites entry in _stats)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.stat) && !_byStat.ContainsKey(entry.stat))
                        _byStat.Add(entry.stat, entry);
                }
            }

            return _byStat.TryGetValue(stat, out NodeSprites found) ? found : null;
        }

        // 인스펙터에서 목록을 고치면 다시 찾는다.
        private void OnValidate() => _byStat = null;
    }
}
