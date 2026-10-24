using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;
using BlackHole.Systems.Numbers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 노드를 어떻게 칠할지 고르는 재료: 모양(NodeTreeLook)과 지금 테마(팔레트·그림 변형). 팔레트가 있으면 팔레트의 역할 색, 없으면 Look의 색.
    // Look이 없으면(디버그) 상태 색 칸으로 그린다.
    internal readonly struct NodeTreeStyle
    {
        private static readonly Color OwnedLinkColor = new Color(0.72f, 0.9f, 0.76f);
        private static readonly Color LinkColor = new Color(0.45f, 0.47f, 0.52f);
        private static readonly Color DefaultBadgeBackColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);

        public readonly NodeTreeLook Look;
        public readonly UIAppliedTheme Theme;

        public NodeTreeStyle(NodeTreeLook look, in UIAppliedTheme theme)
        {
            Look = look;
            Theme = theme;
        }

        public bool HasLook => Look != null;
        public bool ShowHidden => Look != null && Look.ShowHidden;

        public Sprite SpriteFor(Sprite sprite) => Theme.SpriteFor(sprite);

        // 가격·Rank 딱지의 바탕.
        public Color BadgeBackColor =>
            Theme.ColorFor(UIColorRole.Surface) ?? (Look != null ? Look.PriceBackColor : DefaultBadgeBackColor);

        // 가격·Rank 글자 색: 살 수 있으면 본문색, 아니면 흐린 글자색.
        public Color PriceColorOf(NodeState state)
        {
            if (Theme.HasColors)
                return Theme.ColorFor(state == NodeState.Purchasable ? UIColorRole.TextMain : UIColorRole.TextWeak).Value;

            return Look != null ? Look.PriceColorOf(state) : Color.white;
        }

        // 선 색: 양 끝을 샀으면 강조색, 한쪽을 샀고 다른 쪽을 살 수 있으면 본문색, 그 밖은 선 색.
        public Color LinkColorOf(NodeState a, NodeState b)
        {
            if (Theme.HasColors)
            {
                if (a == NodeState.Owned && b == NodeState.Owned)
                    return Theme.ColorFor(UIColorRole.Accent).Value;

                bool open = (a == NodeState.Owned && b == NodeState.Purchasable) || (b == NodeState.Owned && a == NodeState.Purchasable);
                return Theme.ColorFor(open ? UIColorRole.TextMain : UIColorRole.Line).Value;
            }

            if (Look != null)
                return Look.LinkColorOf(a, b);

            return a == NodeState.Owned && b == NodeState.Owned ? OwnedLinkColor : LinkColor;
        }
    }

    // 노드가 지금 어떤 손길을 받고 있는가. 칠할 때 링과 밝기를 정한다.
    [Flags]
    internal enum NodeHighlight
    {
        None = 0,
        // 포인터가 올라가 있다(PC): 흐린 링, 온전한 밝기.
        Hovered = 1,
        // 고른 노드(설명이 떠 있다): 흰 링, 온전한 흰색.
        Selected = 2,
        // 터치로 누르는 중: 흰 링.
        Pressed = 4,
        // 꾹 눌러 게이지가 꽉 찼다(살 준비): 그림이 초록 쪽으로 살짝 물든다.
        Armed = 8,
    }

    // 트리의 노드 하나: 데이터(ID·스탯·가격·Rank·상태·구역)와 그것을 그리는 UI(그림, 가격·Rank 딱지, 링 둘).
    // 자기를 칠하고(Paint), 크기·초록 링을 바꾼다. 상태를 정하고 손길을 재는 것은 NodeTreeView다.
    internal sealed class NodeVisual
    {
        public const float CellSize = 150;
        public const float NodeSize = 96;
        // 노드 아래 가격 딱지(글자 폭 + 여백). 선이 지나가도 가격이 읽히도록 바탕을 깐다.
        private const float PriceHeight = 30;
        private const float PriceSize = 24;
        private const float PricePadding = 16;
        // 노드 오른쪽 위 Rank 딱지.
        private const float RankHeight = 26;
        private const float RankSize = 18;
        private const float RankPadding = 12;
        // 일부만 산 노드에 산 색을 섞는 비율.
        private const float PartialMix = 0.35f;
        // 링: 노드보다 이만큼 크다(지름). 고른 노드는 흰 링, 살 수 있는 노드를 꾹 누르면 그 위에 초록 링이 시계 방향으로 차오른다.
        private const float SelectRingExtra = 18;
        private static readonly Color SelectRingColor = new Color(1, 1, 1, 0.85f);
        private static readonly Color HoverRingColor = new Color(1, 1, 1, 0.55f);
        public static readonly Color FillRingColor = new Color(0.55f, 0.85f, 0.6f, 1);
        // 게이지가 꽉 찬 노드의 그림이 초록 쪽으로 물드는 비율.
        private const float ArmedTint = 0.3f;
        // Look이 없을 때의 상태 색.
        private static readonly Color OwnedColor = new Color(0.22f, 0.58f, 0.32f);
        private static readonly Color PurchasableColor = new Color(0.85f, 0.66f, 0.18f);
        private static readonly Color RevealedColor = new Color(0.32f, 0.33f, 0.37f);

        public readonly string Id;
        public readonly string Stat;
        public readonly int MaxRank;
        // 시작 노드. 그 구역은 처음부터 열린 구역이다.
        public readonly bool Start;
        public readonly Vector2 Position;
        // 든 구역의 열쇠. 구역에 들지 않았으면 null.
        public string Region;
        // 선으로 이어진 노드.
        public readonly List<NodeVisual> Neighbors = new();
        // 미리 보기 거리 안의 안개 속 노드(NodeTreeView.MarkAhead).
        public bool Ahead;
        // 먼 노드(열린 구역 밖이고 미리 보기 거리 밖인 안개 속 노드). NodeTreeView.Show가 정한다.
        public bool Far;
        // 다음 Rank의 비용.
        public long Price;
        // 산 Rank.
        public int Rank;
        public NodeState State = NodeState.Hidden;

        public readonly GameObject Root;
        private readonly Image _image;
        private readonly TMP_Text _label;
        // 가격 딱지(Look이 있을 때만). 샀으면 숨긴다.
        private readonly RectTransform _badge;
        // Rank 딱지(Rank가 둘 이상인 노드만)와 그 글자.
        private readonly RectTransform _rankBadge;
        private readonly TMP_Text _rankLabel;
        // 흰 링(고름·꾹 누름·올림)과 그 위에 차오르는 초록 링(꾹 누름 진행 → 살 준비 → 산 뒤 사라짐).
        private readonly Image _selectRing;
        private readonly Image _fillRing;

        public RectTransform Rect => (RectTransform)Root.transform;
        public bool IsActive => Root.activeSelf;
        // 지금 그려진 그림(상태·테마 반영).
        public Sprite Sprite => _image.sprite;

        private NodeVisual(NodeItem item, Vector2 position, GameObject root, Image image, TMP_Text label, RectTransform badge,
            RectTransform rankBadge, TMP_Text rankLabel, Image selectRing, Image fillRing)
        {
            Id = item.Id;
            Stat = item.Stat;
            Price = item.Price;
            MaxRank = item.MaxRank;
            Start = item.Start;
            Position = position;
            Root = root;
            _image = image;
            _label = label;
            _badge = badge;
            _rankBadge = rankBadge;
            _rankLabel = rankLabel;
            _selectRing = selectRing;
            _fillRing = fillRing;
        }

        public void SetActive(bool active)
        {
            if (Root.activeSelf != active)
                Root.SetActive(active);
        }

        // 노드 하나를 지금 상태와 손길대로 칠한다. 가격은 마지막 Rank까지 사지 않은 노드에만 쓴다.
        public void Paint(in NodeTreeStyle style, NodeHighlight highlight)
        {
            ShowRing(highlight);

            if (State == NodeState.Hidden)
            {
                // 안개 속: 가격·Rank 딱지는 숨긴다. 열린 구역이면 흐린 자물쇠, 먼 노드면 작고 흐린 점(레이캐스트를 받지 않아 올림·누름이 없다).
                if (style.HasLook)
                {
                    _image.sprite = style.SpriteFor(Far ? style.Look.FarSprite : style.Look.SpriteOf(Stat, State));
                    _image.color = Far ? style.Look.FarTint : TintOf(style, highlight);
                    SetSize(Far ? NodeSize * style.Look.FarScale : NodeSize);
                    _image.raycastTarget = !Far;
                }

                Show(_badge, false);
                Show(_rankBadge, false);
                return;
            }

            SetSize(NodeSize);
            _image.raycastTarget = true;
            Show(_rankBadge, true);

            string price = NumberText.Compact(Price);
            bool partial = Rank > 0 && State != NodeState.Owned;

            if (_rankLabel != null)
                _rankLabel.text = Rank.ToString(CultureInfo.InvariantCulture) + "/" + MaxRank.ToString(CultureInfo.InvariantCulture);

            if (!style.HasLook)
            {
                _image.sprite = null;
                Color color = ColorOf(State);
                _image.color = partial ? Color.Lerp(color, OwnedColor, PartialMix) : color;
                _label.color = Color.white;
                _label.text = State == NodeState.Owned ? $"{Id}\nowned" : $"{Id}\n{price}";
                return;
            }

            // 고른 노드는 그림은 바꾸지 않고 — 아직 산 게 아니다 — 색만 온전한 밝기로 올린다. 링이 고름을 보인다.
            // 올림(마우스)도 그림은 그대로이고 흐린 링과 온전한 밝기로 보인다.
            _image.sprite = style.SpriteFor(style.Look.SpriteOf(Stat, State));
            Color tint = TintOf(style, highlight);
            _image.color = partial ? Color.Lerp(tint, OwnedColor, PartialMix) : tint;

            bool showPrice = State != NodeState.Owned;
            Show(_badge, showPrice);
            _label.color = style.PriceColorOf(State);

            if (!showPrice || _label.text == price)
                return;

            _label.text = price;

            if (_badge != null)
                _badge.sizeDelta = new Vector2(_label.GetPreferredValues(price).x + PricePadding, PriceHeight);
        }

        // 가격·Rank 딱지의 바탕과 Rank 글자 색(테마가 바뀌었을 때).
        public void PaintBadges(in NodeTreeStyle style)
        {
            if (_badge != null && _badge.TryGetComponent(out Image priceBack))
                priceBack.color = style.BadgeBackColor;

            if (_rankBadge != null && _rankBadge.TryGetComponent(out Image rankBack))
                rankBack.color = style.BadgeBackColor;

            if (_rankLabel != null)
                _rankLabel.color = style.PriceColorOf(NodeState.Purchasable);
        }

        // 눌림·스프링 연출의 크기.
        public void SetScale(float scale) => Root.transform.localScale = new Vector3(scale, scale, 1);

        // 초록 링의 채움(0 ~ 1)과 알파. 0이면 끈다.
        public void SetFill(float amount, float alpha = 1)
        {
            if (_fillRing == null)
                return;

            bool on = amount > 0;

            if (_fillRing.gameObject.activeSelf != on)
                _fillRing.gameObject.SetActive(on);

            if (!on)
                return;

            Color color = FillRingColor;
            color.a = alpha;
            _fillRing.fillAmount = amount;
            _fillRing.color = color;
        }

        // 흰 링의 켜짐: 고른 노드이거나 꾹 누르는 중인 노드, 또는 손을 올린 노드(조금 흐리게).
        private void ShowRing(NodeHighlight highlight)
        {
            if (_selectRing == null)
                return;

            bool chosen = (highlight & (NodeHighlight.Selected | NodeHighlight.Pressed)) != 0;
            bool on = chosen || (highlight & NodeHighlight.Hovered) != 0;

            if (_selectRing.gameObject.activeSelf != on)
                _selectRing.gameObject.SetActive(on);

            if (on)
                _selectRing.color = chosen ? SelectRingColor : HoverRingColor;
        }

        // 노드 색: 고른 노드(설명이 떠 있는 노드)는 상태와 무관하게 온전한 흰색(가장 밝고 불투명). 아니면 Look의 상태별 색.
        // 꾹 눌러 게이지가 꽉 찬 노드는 초록 쪽으로 살짝 물든다(살 준비).
        private Color TintOf(in NodeTreeStyle style, NodeHighlight highlight)
        {
            if ((highlight & NodeHighlight.Armed) != 0 && State == NodeState.Purchasable)
                return Color.Lerp(Color.white, FillRingColor, ArmedTint);

            if ((highlight & NodeHighlight.Selected) != 0)
                return Color.white;

            return style.Look.TintOf(State, (highlight & NodeHighlight.Hovered) != 0);
        }

        // 노드 칸 크기(먼 노드는 점으로 줄인다). 바뀔 때만 쓴다.
        private void SetSize(float size)
        {
            RectTransform rect = Rect;

            if (!Mathf.Approximately(rect.sizeDelta.x, size))
                rect.sizeDelta = new Vector2(size, size);
        }

        private static void Show(RectTransform part, bool visible)
        {
            if (part != null && part.gameObject.activeSelf != visible)
                part.gameObject.SetActive(visible);
        }

        private static Color ColorOf(NodeState state) => state switch
        {
            NodeState.Owned => OwnedColor,
            NodeState.Purchasable => PurchasableColor,
            _ => RevealedColor,
        };

        // 노드 UI를 parent 아래에 만든다. 누름은 트리 영역이 받는다 — 노드에 Button을 두면 클릭을 가져가 입력 장치를 가를 수 없다.
        public static NodeVisual Create(NodeItem item, RectTransform parent, in NodeTreeStyle style)
        {
            NodeTreeLook look = style.Look;
            RectTransform rect = Child(parent, "Node " + item.Id);
            rect.sizeDelta = new Vector2(NodeSize, NodeSize);
            var position = new Vector2(item.X * CellSize, item.Y * CellSize);
            rect.anchoredPosition = position;

            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;

            // 링 그림은 Look이 만든다. Look이 없으면 링은 없다(디버그 그리기).
            Sprite ringSprite = look != null ? look.RingSprite : null;
            Image selectRing = ringSprite != null ? CreateRing(rect, "Select", ringSprite, SelectRingColor) : null;
            Image fillRing = ringSprite != null ? CreateRing(rect, "Fill", ringSprite, FillRingColor) : null;

            if (fillRing != null)
            {
                // 초록 링: 위에서 시작해 시계 방향으로 차오른다(Filled Radial360).
                fillRing.type = Image.Type.Filled;
                fillRing.fillMethod = Image.FillMethod.Radial360;
                fillRing.fillOrigin = (int)Image.Origin360.Top;
                fillRing.fillClockwise = true;
                fillRing.fillAmount = 0;
            }

            RectTransform badge = null;
            RectTransform labelRect;

            if (look != null)
            {
                // 가격 딱지: 노드 바로 아래 가운데. 폭은 칠할 때 글자에 맞춘다.
                badge = Child(rect, "Price");
                badge.anchorMin = new Vector2(0.5f, 0);
                badge.anchorMax = new Vector2(0.5f, 0);
                badge.pivot = new Vector2(0.5f, 1);
                badge.sizeDelta = new Vector2(NodeSize, PriceHeight);
                var back = badge.gameObject.AddComponent<Image>();
                back.color = style.BadgeBackColor;
                back.raycastTarget = false;
                labelRect = Stretch(Child(badge, "Label"));
            }
            else
            {
                labelRect = Child(rect, "Label");
            }

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();

            if (look != null)
            {
                label.fontSize = PriceSize;
            }
            else
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(4, 4);
                labelRect.offsetMax = new Vector2(-4, -4);
                // 긴 ID도 가격 줄까지 보이도록 칸에 맞게 글자를 줄인다.
                label.enableAutoSizing = true;
                label.fontSizeMin = 10;
                label.fontSizeMax = 18;
            }

            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            RectTransform rankBadge = null;
            TMP_Text rankLabel = null;

            if (item.MaxRank > 1)
                rankBadge = CreateRankBadge(rect, style, out rankLabel);

            return new NodeVisual(item, position, rect.gameObject, image, label, badge, rankBadge, rankLabel, selectRing, fillRing);
        }

        // 노드 위에 그리는 얇은 고리(가운데는 비어 노드가 보인다). 처음엔 꺼 둔다.
        private static Image CreateRing(RectTransform node, string name, Sprite sprite, Color color)
        {
            RectTransform rect = Child(node, name);
            rect.sizeDelta = new Vector2(NodeSize + SelectRingExtra, NodeSize + SelectRingExtra);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return image;
        }

        // Rank 딱지: 노드 오른쪽 위 모서리에 걸친다. 글자는 칠할 때 쓴다("3/10").
        private static RectTransform CreateRankBadge(RectTransform node, in NodeTreeStyle style, out TMP_Text label)
        {
            RectTransform badge = Child(node, "Rank");
            badge.anchorMin = new Vector2(1, 1);
            badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(1, 1);
            badge.anchoredPosition = new Vector2(RankPadding, RankPadding);
            badge.sizeDelta = new Vector2(NodeSize * 0.6f, RankHeight);

            var back = badge.gameObject.AddComponent<Image>();
            back.color = style.BadgeBackColor;
            back.raycastTarget = false;

            label = Stretch(Child(badge, "Label")).gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = RankSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = style.PriceColorOf(NodeState.Purchasable);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return badge;
        }

        // 부모 가운데에 붙는 자식.
        public static RectTransform Child(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }
    }

    // 두 노드를 잇는 선. 양 끝이 보일 때만 보이고, 양 끝 상태에 따른 색. 한쪽이라도 안개 속이면 안개 선 색.
    internal sealed class LinkVisual
    {
        private const float LinkWidth = 6;

        public readonly NodeVisual From;
        public readonly NodeVisual To;
        public readonly GameObject Root;
        private readonly Image _image;

        private LinkVisual(NodeVisual from, NodeVisual to, GameObject root, Image image)
        {
            From = from;
            To = to;
            Root = root;
            _image = image;
        }

        public void Paint(in NodeTreeStyle style)
        {
            bool fogged = From.State == NodeState.Hidden || To.State == NodeState.Hidden;
            bool visible = !fogged || style.ShowHidden;

            if (Root.activeSelf != visible)
                Root.SetActive(visible);

            if (!visible)
                return;

            _image.color = fogged ? style.Look.HiddenLinkColor : style.LinkColorOf(LinkStateOf(From), LinkStateOf(To));
        }

        // 선 색을 고를 때의 상태: Rank를 하나라도 산 노드는 산 것으로 본다.
        private static NodeState LinkStateOf(NodeVisual node) => node.Rank > 0 ? NodeState.Owned : node.State;

        // 두 노드 중심을 잇는 얇은 막대: 가운데에 놓고, 길이만큼 늘리고, 방향만큼 돌린다.
        public static LinkVisual Create(NodeVisual from, NodeVisual to, RectTransform parent)
        {
            RectTransform rect = NodeVisual.Child(parent, $"Link {from.Id}-{to.Id}");
            Vector2 delta = to.Position - from.Position;
            rect.anchoredPosition = (from.Position + to.Position) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, LinkWidth);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return new LinkVisual(from, to, rect.gameObject, image);
        }
    }
}
