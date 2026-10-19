using System;
using System.Collections.Generic;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 업그레이드 화면의 트리 보기(uGUI).
    //
    // 좌표: 칸 (X, Y)의 노드는 트리 공간의 (X × 칸 크기, Y × 칸 크기)에 놓인다.
    // 보이기: 산 노드와 그 이웃(살 수 있음·Gold 모자람)은 노드 그림으로 그린다(이웃은 조금 어둡게).
    //   아직 닿지 않은 노드(Hidden)는 안개 설정(NodeTreeLook.ShowHidden)을 따른다 — 끄면 그리지 않고, 켜면 구역으로 나눈다.
    //   열린 구역(산 노드나 시작 노드가 든 구역)이거나 보이는 노드에서 선으로 몇 칸 안(NodeTreeLook.LockAhead)이면 자물쇠(올리면 이름만),
    //   아니면 먼 노드로 작고 흐린 점(올림·누름을 받지 않는다).
    //   구역에 들지 않은 노드는 열린 구역이 없는 것으로 본다. 선은 양 끝이 모두 보일 때만 그리고, 한쪽이라도 안개 속이면 안개 선 색.
    // 모양: 노드 그림·선 색·가격 색은 NodeTreeLook이 정한다(스탯마다의 노드 그림). 가격(다음 Rank 비용)은 노드 아래에 줄여 쓴다(1.5T).
    //   Look이 비어 있으면 예전처럼 상태 색 칸에 ID·가격을 쓴다.
    // Rank: Rank가 둘 이상인 노드는 오른쪽 위에 "산 Rank/최대 Rank" 딱지를 단다. 일부만 산 노드는 산 색을 엷게 섞는다.
    //   선 색은 Rank를 하나라도 산 노드를 산 것으로 본다(이웃이 드러나는 기준과 같다).
    // 조작: 빈 곳이나 노드 위를 끌면 이동. 휠(커서 중심)이나 두 손가락 벌리기·오므리기(손가락 사이 가운데 중심)로 확대·축소.
    // 열 때: 전체 대신 지금 살 수 있는 노드 쪽을 읽기 좋은 배율로 확대해 둔다(FrameFocus).
    // 올림: 포인터 아래의 노드가 바뀌면 NodeHovered·NodeLeft로 알린다(툴팁은 호스트가 띄운다). 터치는 올림이 없다.
    //   노드마다 입력 컴포넌트를 붙이지 않고 트리 영역이 포인터 이동을 받아 찾는다 — UI_EventHandler를 노드에 붙이면
    //   노드 위에서 시작한 끌기를 그것이 가져가 트리가 움직이지 않는다. 끌기·휠이 시작되면 올림을 푼다.
    // 누름: 노드에는 Button을 두지 않고 트리 영역이 클릭을 받는다(노드 칸에서 눌러 같은 칸에서 뗀 것만). 입력 장치로 가른다.
    //   - 마우스·펜: 살 수 있는 노드면 NodeClicked(호스트가 바로 산다). PC는 올림 툴팁으로 읽고 클릭으로 산다.
    //   - 터치: 먼 노드가 아니면 누르고 SelectDelaySeconds 뒤에 고르고 NodeTapped. 호스트가 패널을 고정한다 — 읽으려고 누른 손가락이 사지 않게.
    //     짧은 지연은 노드 위에서 시작한 끌기에 패널이 잠깐 떴다 닫히지 않게 한다(그 안에 끌기가 되면 띄우지 않는다).
    //     떼면(클릭) 한 번 더 NodeTapped해 산 뒤의 내용으로 갱신한다. 끌기로 바뀌면 선택이 풀리고 패널이 닫힌다(ViewMoved).
    //     선택한 노드는 올린 것처럼 칠한다(SetSelected). 빈 곳을 누르면 EmptyTapped(호스트가 선택을 푼다).
    //   - 터치로 꾹: 살 수 있는 노드를 HoldSeconds 이상 누르고(끌지 않고) 같은 칸에서 떼면 NodeHeld(호스트가 산다).
    //     시간이 차면 NodeHoldArmed로 먼저 알린다. 떼거나 끌면 NodeHoldReleased.
    //   - 연출(터치): 고른 노드는 그림은 평소대로 두고 흰 링만 두른다(아직 사지 않았으니 초록 강조 없음). 살 수 있는 노드를 누르면
    //     살짝 부풀고 흰 링 위로 초록 링이 시계 방향으로 차오른다. 꽉 찬 뒤 떼면 스프링으로 쫄깃하게 돌아오며 산다(초록 링은 잠깐 머물다 사라짐).
    //     중간에 떼면 초록 링이 비워지고 크기도 돌아온다. 그림은 Show가 산 상태로 칠한다.
    //
    // 업그레이드 화면(UpgradeScreen)을 호스트로 두는 페이지(UIPage)다.
    // ScreenFlow가 업그레이드 화면을 연 뒤 SwitchPage로 염.
    // 이 컴포넌트는 트리 영역(보이는 창)에 붙고, 그 영역은 호스트의 PageRoot 바로 아래 자식.
    //
    // 영역은 가운데 기준(pivot 0.5)이어야 확대 계산이 맞음.
    public sealed class NodeTreeView : UIPage<NodeTreeView.Refs>, IStyleView, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler,
        IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum Refs { }

        private const float CellSize = 150;
        private const float NodeSize = 96;
        private const float LinkWidth = 6;
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
        // 터치로 노드를 꾹 눌러 사는 데 필요한 시간(초).
        private const float HoldSeconds = 0.3f;
        // 고른 노드의 링: 노드보다 이만큼 크다(지름). 살 수 있는 노드(초록)와 고른 노드를 가르는 표시.
        private const float SelectRingExtra = 18;
        private const int RingTextureSize = 64;
        private const float RingThickness = 3;
        // 링: 고른 노드는 흰 링. 살 수 있는 노드를 꾹 누르면 그 위에 초록 링이 시계 방향으로 차오른다(HoldSeconds에 꽉 참 = 살 준비).
        // 떼서 사면 초록 링이 잠깐 머물다 사라진다. 중간에 떼면 비워진다.
        private static readonly Color SelectRingColor = new Color(1, 1, 1, 0.85f);
        private static readonly Color FillRingColor = new Color(0.55f, 0.85f, 0.6f, 1);
        private const float FillDrainSeconds = 0.12f;
        private const float FillFadeSeconds = 0.35f;
        // 크기: 살 수 있는 노드를 누르면 살짝 부풀고(PuffScale), 떼서 사면 스프링으로 쫄깃하게 돌아온다. 다른 노드는 살짝 눌린다(PressScale).
        private const float PuffScale = 1.08f;
        private const float PressScale = 0.94f;
        private const float PressSeconds = 0.07f;
        // 스프링: 한 번 살짝 지나쳤다 바로 멈춘다(길게 떨지 않게 감쇠를 높게).
        private const float SpringStiffness = 1100;
        private const float SpringDamping = 24;
        private const float SpringRest = 0.002f;
        // 사며 뗄 때: 아주 잠깐 더 부풀었다가(예비 동작) 고무줄처럼 찰싹 돌아온다. 찰싹은 스프링에 안쪽 속도를 준다.
        private const float AnticipateScale = 1.14f;
        private const float AnticipateSeconds = 0.06f;
        private const float SnapKick = 2.2f;
        // 게이지가 꽉 찬 순간: 아주 잠깐 한 번 더 부풀고(ArmedBump), 그림이 초록 쪽으로 살짝 물든다(ArmedTint).
        private const float ArmedBumpScale = 1.13f;
        private const float ArmedBumpSeconds = 0.09f;
        private const float ArmedTint = 0.3f;
        // 터치로 누른 뒤 패널을 띄우기까지의 지연(초). 끌기가 이 안에 시작되면 띄우지 않는다.
        private const float SelectDelaySeconds = 0.06f;
        // 축소 하한: 지도 전체가 창에 들어가는 배율(_fitZoom, 트리를 만들 때와 창 크기가 바뀔 때 다시 잰다)과 예전 하한(0.3) 중 작은 쪽.
        // 그보다 더 작게는 MinZoomFloor가 바닥이다.
        private const float MinZoomDefault = 0.3f;
        private const float MinZoomFloor = 0.08f;
        private const float MaxZoom = 2.5f;
        private const float FitZoomLimit = 1.5f;
        // 열 때의 배율: 노드가 화면에서 이만큼(mm)은 되게 한다. 큰 화면(PC)에서는 FocusMinZoom 아래로 내리지 않는다. [임시]
        private const float FocusNodeMillimeters = 9;
        private const float FocusMinZoom = 1;
        // 열 때 화면 가장자리에 남길 여백(트리 공간, 양쪽 합).
        private const float FocusMargin = CellSize * 2;
        // 두 손가락 사이가 이보다 가까우면(트리 영역 단위) 배율은 두고 이동만 한다.
        private const float MinPinchDistance = 20;

        private static readonly Color OwnedColor = new Color(0.22f, 0.58f, 0.32f);
        private static readonly Color PurchasableColor = new Color(0.85f, 0.66f, 0.18f);
        private static readonly Color RevealedColor = new Color(0.32f, 0.33f, 0.37f);
        private static readonly Color OwnedLinkColor = new Color(0.72f, 0.9f, 0.76f);
        private static readonly Color LinkColor = new Color(0.45f, 0.47f, 0.52f);

        [Tooltip("노드 그림과 선·가격 색. 비우면 상태 색 칸으로 그린다.")]
        [SerializeField] private NodeTreeLook _look;

        // 테마(다크 모드): 노드 그림은 Look의 그림을 테마 짝(UISpriteSet)으로, 선·가격·딱지 색은 팔레트 역할로 바꾼다.
        // 팔레트가 없으면 Look의 색을 쓴다. 설정이 바뀌면 UISettingsSync가 다시 넣는다.
        private UIStyle _style = UIStyle.Default;

        public void ApplyStyle(in UIStyle style)
        {
            _style = style;
            RepaintAll();
        }

        // 테마가 바뀌었다. 보이는 노드·선·딱지를 지금 테마로 다시 칠한다(상태는 그대로).
        private void RepaintAll()
        {
            foreach (NodeVisual node in _nodes.Values)
            {
                if (!node.Root.activeSelf)
                    continue;

                Paint(node);
                PaintBadges(node);
            }

            PaintLinks();
        }

        // 가격·Rank 딱지의 바탕과 Rank 글자 색.
        private void PaintBadges(NodeVisual node)
        {
            if (node.Badge != null && node.Badge.TryGetComponent(out Image priceBack))
                priceBack.color = BadgeBackColor();

            if (node.RankLabel != null)
            {
                node.RankLabel.color = PriceColorOf(NodeState.Purchasable);

                if (node.RankLabel.transform.parent.TryGetComponent(out Image rankBack))
                    rankBack.color = BadgeBackColor();
            }
        }

        private Color BadgeBackColor() =>
            _style.ColorFor(UIColorRole.Surface) ?? (_look != null ? _look.PriceBackColor : new Color(0.1f, 0.1f, 0.12f, 0.85f));

        private Color PriceColorOf(NodeState state)
        {
            if (_style.HasColors)
                return _style.ColorFor(state == NodeState.Purchasable ? UIColorRole.TextMain : UIColorRole.TextWeak).Value;

            return _look != null ? _look.PriceColorOf(state) : Color.white;
        }

        private Color LinkColorOf(NodeState a, NodeState b)
        {
            if (_style.HasColors)
            {
                if (a == NodeState.Owned && b == NodeState.Owned)
                    return _style.ColorFor(UIColorRole.Accent).Value;

                bool open = (a == NodeState.Owned && b == NodeState.Purchasable) || (b == NodeState.Owned && a == NodeState.Purchasable);
                return _style.ColorFor(open ? UIColorRole.TextMain : UIColorRole.Line).Value;
            }

            if (_look != null)
                return _look.LinkColorOf(a, b);

            return a == NodeState.Owned && b == NodeState.Owned ? OwnedLinkColor : LinkColor;
        }

        private readonly Dictionary<string, NodeVisual> _nodes = new Dictionary<string, NodeVisual>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, NodeVisual> _nodesByObject = new Dictionary<GameObject, NodeVisual>();
        private readonly List<LinkVisual> _links = new List<LinkVisual>();
        // 구역(우주 구역)이 하나라도 있는가. 구역은 그리지 않고, 보이는 창 가운데가 어느 구역인지 알리는 데(RegionViewed)와 열린 구역을 가리는 데 쓴다.
        private bool _hasRegions;
        // 열린 구역의 열쇠: 산 노드(Rank 하나라도)나 시작 노드가 든 구역. Show마다 다시 모은다.
        private readonly HashSet<string> _openRegions = new HashSet<string>(StringComparer.Ordinal);
        private string _viewedRegion;
        private Vector2 _viewedAt;
        private float _viewedZoom;

        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _linkLayer;
        private RectTransform _nodeLayer;
        private Canvas _canvas;
        private float _zoom = 1;
        private bool _framePending;
        // 지도 전체(모든 노드 + 여백)가 창에 꼭 맞는 배율. 축소의 하한이다. 노드가 없거나 창 크기가 0이면 1.
        private float _fitZoom = 1;
        private float MinZoom => Mathf.Max(MinZoomFloor, Mathf.Min(MinZoomDefault, _fitZoom));
        // 끄는 중인 포인터(손가락)와 화면 위치. 둘 이상이면 앞의 둘로 핀치한다.
        private readonly List<(int Id, Vector2 Position)> _drags = new List<(int Id, Vector2 Position)>(2);

        // 마우스·펜으로 살 수 있는 노드를 눌렀다(노드 ID).
        public event Action<string> NodeClicked;
        // 터치로 노드를 눌렀다(노드 ID, 노드 칸): 누르는 순간과 떼는 순간에 한 번씩. 먼 노드는 알리지 않는다. 선택은 이미 바뀐 뒤다.
        public event Action<string, RectTransform> NodeTapped;
        // 노드가 아닌 빈 곳을 눌렀다(어느 장치든).
        public event Action EmptyTapped;
        // 보는 곳을 옮기기 시작했다(끌기·휠·핀치). 선택은 이미 풀린 뒤다 — 고정 툴팁이 옛 자리에 남지 않게 호스트가 닫는다.
        public event Action ViewMoved;
        // 터치로 살 수 있는 노드를 꾹 눌러 시간이 찼다(노드 ID). 아직 사지 않았다 — 떼면 산다.
        public event Action<string> NodeHoldArmed;
        // 꾹 누름이 끝났다(노드 ID): 뗐거나 끌기로 바뀌었거나 페이지가 닫혔다. 산 경우에도 NodeHeld 앞에 온다.
        public event Action<string> NodeHoldReleased;
        // 터치로 꾹 눌렀다가 같은 칸에서 뗐다(노드 ID). 호스트가 산다.
        public event Action<string> NodeHeld;
        // 터치로 Gold가 모자란 노드(Revealed)를 HoldSeconds 이상 눌렀다(노드 ID) — 사려 했지만 못 산다. 호스트가 골드·구매 탭을 붉게 한다.
        // 떼면 NodeHoldReleased가 온다. 정보만 보려는 짧은 누름에는 아무것도 알리지 않는다.
        public event Action<string> NodeHoldShort;

        // 보이는 창의 가운데가 든 구역이 바뀜(구역 열쇠, 구역이 없으면 null). 이동·확대·처음 맞춤 뒤에 알린다.
        public event Action<string> RegionViewed;

        // 포인터가 노드 위에 올라감(노드 ID, 노드 칸)과 벗어남(노드 ID). 올라간 노드는 하나뿐이고, 벗어남이 먼저 온다.
        public event Action<string, RectTransform> NodeHovered;
        public event Action<string> NodeLeft;

        private NodeVisual _hovered;
        // 터치로 누르고 있는 노드(꾹 누름 판정). 끌기가 시작되면 버린다.
        private NodeVisual _pressed;
        private int _pressedPointer;
        private float _pressedAt;
        private bool _holdArmed;
        // 크기 연출 중인 노드. 한 번에 하나다. 닿아 있으면 목표(PuffScale/PressScale)로 곧장 가고, 떼면 스프링으로 1로 돌아온다.
        private NodeVisual _downNode;
        private int _downPointer;
        private bool _downHeld;
        private float _downScale = 1;
        private float _downVelocity;
        // 사며 뗀 뒤의 예비 동작이 끝나는 시각. 그 전에는 AnticipateScale로 부풀고, 끝나면 찰싹 돌아온다.
        private float _anticipateUntil;
        private bool _snapPending;
        // 게이지가 꽉 찬 순간의 작은 부풂이 끝나는 시각.
        private float _armedBumpUntil;
        // 초록 링이 사라지는 중인 노드(산 직후). 비우면 fillAmount 0.
        private NodeVisual _fading;
        private float _fadeElapsed;
        // 지연 뒤에 고를 노드(터치로 누른 직후). 끌기·뗌이 먼저 오면 버린다.
        private NodeVisual _pendingSelect;
        private float _pendingAt;
        // 터치로 고른 노드. 올린 것과 같은 그림·색으로 칠한다. 호스트가 SetSelected·ClearSelection으로 바꾼다.
        private NodeVisual _selected;

        // 터치로 고른 노드의 ID. 없으면 null.
        public string SelectedId => _selected?.Id;

        // 노드 칸(툴팁을 붙일 자리). 없으면 null.
        public RectTransform RectOf(string id) =>
            id != null && _nodes.TryGetValue(id, out NodeVisual node) ? (RectTransform)node.Root.transform : null;

        // 노드에 지금 그려진 그림(상태·테마 반영). 없으면 null. 호스트가 고정 패널의 제목 옆에 같은 그림을 보인다.
        public Sprite IconOf(string id) =>
            id != null && _nodes.TryGetValue(id, out NodeVisual node) ? node.Image.sprite : null;

        public void SetSelected(string id)
        {
            NodeVisual node = id != null && _nodes.TryGetValue(id, out NodeVisual found) ? found : null;
            SetSelected(node);
        }

        public void ClearSelection() => SetSelected((NodeVisual)null);

        protected override void OnInitialize()
        {
            _viewport = (RectTransform)transform;

            // 끌기·휠을 받으려면 트리 영역 자체가 레이캐스트 대상이어야 한다. 보이지 않는 이미지를 깜.
            if (GetComponent<Graphic>() == null)
                gameObject.AddComponent<Image>().color = Color.clear;

            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();

            _content = Layer(_viewport, "Content");
            _linkLayer = Layer(_content, "Links");
            _nodeLayer = Layer(_content, "Nodes");
        }

        // 트리를 새로 만든다. 선의 양 끝 가운데 하나라도 노드 목록에 없으면 그 선은 그리지 않음.
        // regions: 구역(비어 있어도 된다). 노드마다 든 구역을 기억해 두고, 보이는 창 가운데에서 가장 가까운 노드의 구역을 RegionViewed로 알린다.
        public void Build(IReadOnlyList<NodeItem> nodes, IReadOnlyList<(string A, string B)> links, IReadOnlyList<NodeRegionItem> regions = null)
        {
            Clear();

            foreach (NodeItem node in nodes)
            {
                NodeVisual visual = CreateNode(node);
                _nodes.Add(node.Id, visual);
                _nodesByObject.Add(visual.Root, visual);
            }

            foreach ((string a, string b) in links)
            {
                if (_nodes.TryGetValue(a, out NodeVisual from) && _nodes.TryGetValue(b, out NodeVisual to))
                {
                    _links.Add(CreateLink(from, to));
                    from.Neighbors.Add(to);
                    to.Neighbors.Add(from);
                }
            }

            if (regions != null)
            {
                foreach (NodeRegionItem region in regions)
                {
                    // 노드가 든 구역(둘 이상이면 먼저 나온 구역).
                    foreach (string id in region.Nodes)
                    {
                        if (_nodes.TryGetValue(id, out NodeVisual member) && member.Region == null)
                        {
                            member.Region = region.Key;
                            _hasRegions = true;
                        }
                    }
                }
            }

            _viewedRegion = null;
            _framePending = true;
            UpdateFitZoom();
        }

        // 창 크기가 바뀌면(회전·창 크기 조절) 전체가 들어가는 배율도 바뀐다. 지금 배율이 새 하한 아래면 끌어올린다.
        private void OnRectTransformDimensionsChange()
        {
            if (_viewport == null || _content == null)
                return;

            UpdateFitZoom();

            if (_zoom < MinZoom)
                SetZoom(MinZoom);
        }

        // 모든 노드(숨은 노드 포함)의 범위에 노드 크기와 칸 하나의 여백을 더해 창에 맞는 배율을 잰다.
        private void UpdateFitZoom()
        {
            Rect view = _viewport.rect;

            if (_nodes.Count == 0 || view.width <= 0 || view.height <= 0)
            {
                _fitZoom = 1;
                return;
            }

            Bounds(out Vector2 min, out Vector2 max);
            Vector2 size = max - min + new Vector2(NodeSize + CellSize, NodeSize + CellSize);
            _fitZoom = Mathf.Min(view.width / size.x, view.height / size.y);
        }

        private void Bounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            foreach (NodeVisual node in _nodes.Values)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }
        }

        // 보이는 창의 가운데(트리 공간)에서 가장 가까운 노드의 구역을 찾아 바뀌었으면 알린다. 이동·확대 뒤 LateUpdate가 부른다.
        // 구역의 경계 상자로 찾지 않는다 — 구역 모양이 들쭉날쭉하면 상자끼리 겹쳐 가운데 노드와 다른 구역을 알린다.
        private void NotifyRegion()
        {
            if (!_hasRegions || _content == null)
                return;

            Vector2 center = -_content.anchoredPosition / Mathf.Max(_zoom, 0.0001f);

            if (center == _viewedAt && _zoom == _viewedZoom && _viewedRegion != null)
                return;

            _viewedAt = center;
            _viewedZoom = _zoom;

            string nearest = null;
            float best = float.MaxValue;

            foreach (NodeVisual node in _nodes.Values)
            {
                if (node.Region == null)
                    continue;

                float distance = (node.Position - center).sqrMagnitude;

                if (distance < best)
                {
                    best = distance;
                    nearest = node.Region;
                }
            }

            if (nearest == _viewedRegion)
                return;

            _viewedRegion = nearest;
            RegionViewed?.Invoke(nearest);
        }

        // 노드마다 상태(와 다음 Rank 비용, 산 Rank)를 받아 칠한다. 바뀐 프레임에만 호출.
        // costs에 없는 노드(마지막 Rank까지 산 노드)는 가격을 바꾸지 않는다 — Owned면 가격을 쓰지 않는다.
        public void Show(IReadOnlyDictionary<string, NodeState> states, IReadOnlyDictionary<string, long> costs,
            IReadOnlyDictionary<string, int> ranks)
        {
            foreach (NodeVisual node in _nodes.Values)
            {
                node.State = states[node.Id];
                node.Rank = ranks[node.Id];

                if (costs.TryGetValue(node.Id, out long cost))
                    node.Price = cost;
            }

            // 열린 구역: 산 노드(Rank 하나라도)나 시작 노드가 든 구역.
            _openRegions.Clear();
            foreach (NodeVisual node in _nodes.Values)
            {
                if (node.Region != null && (node.Rank > 0 || node.Start))
                    _openRegions.Add(node.Region);
            }

            MarkAhead(_look != null ? _look.LockAhead : 0);

            foreach (NodeVisual node in _nodes.Values)
            {
                // 먼 노드: 열린 구역 밖이고 미리 보기 거리 밖인 안개 속 노드. 점으로만 그리고 올림·누름을 받지 않는다.
                node.Far = node.State == NodeState.Hidden && !node.Ahead && (node.Region == null || !_openRegions.Contains(node.Region));

                // 아직 닿지 않은 노드: 안개 설정(NodeTreeLook.ShowHidden)이 켜져 있으면 자물쇠나 점으로 보이고, 아니면 숨긴다.
                bool visible = node.State != NodeState.Hidden || ShowHidden;
                node.Root.SetActive(visible);

                if (!visible)
                    continue;

                Paint(node);
            }

            // 올라가 있던(고른) 노드가 숨거나 먼 노드가 되면 올림(선택)을 푼다(먼 노드는 툴팁을 띄우지 않는다).
            if (_hovered != null && (!_hovered.Root.activeSelf || _hovered.Far))
                SetHovered(null);

            if (_selected != null && (!_selected.Root.activeSelf || _selected.Far))
                SetSelected((NodeVisual)null);

            PaintLinks();
        }

        // 미리 보기: 보이는 노드(안개 밖)에서 선으로 reach칸 안의 안개 속 노드에 표시한다. 구역이 닫혀 있어도 자물쇠로 보인다
        // (다음 구역 입구 너머 몇 칸). reach가 0이면 표시하지 않는다.
        private void MarkAhead(int reach)
        {
            var frontier = new List<NodeVisual>();

            foreach (NodeVisual node in _nodes.Values)
            {
                node.Ahead = false;

                if (node.State != NodeState.Hidden)
                    frontier.Add(node);
            }

            for (int step = 0; step < reach && frontier.Count > 0; step++)
            {
                var next = new List<NodeVisual>();

                foreach (NodeVisual node in frontier)
                {
                    foreach (NodeVisual neighbor in node.Neighbors)
                    {
                        if (neighbor.State != NodeState.Hidden || neighbor.Ahead)
                            continue;

                        neighbor.Ahead = true;
                        next.Add(neighbor);
                    }
                }

                frontier = next;
            }
        }

        // 선: 양 끝이 보일 때만 보이고, 양 끝 상태에 따른 색. 한쪽이라도 안개 속이면 안개 선 색.
        private void PaintLinks()
        {
            foreach (LinkVisual link in _links)
            {
                bool fogged = link.From.State == NodeState.Hidden || link.To.State == NodeState.Hidden;
                bool visible = !fogged || ShowHidden;
                link.Root.SetActive(visible);

                if (!visible)
                    continue;

                link.Image.color = fogged ? _look.HiddenLinkColor : LinkColorOf(LinkStateOf(link.From), LinkStateOf(link.To));
            }
        }

        private bool ShowHidden => _look != null && _look.ShowHidden;

        // 모든 노드(숨은 노드 포함)의 범위가 보이게 맞춘다. 저작한 모양의 가운데가 화면 가운데임.
        public void FrameAll()
        {
            Rect view = _viewport.rect;

            if (_nodes.Count == 0 || view.width <= 0 || view.height <= 0)
            {
                SetZoom(1);
                _content.anchoredPosition = Vector2.zero;
                return;
            }

            UpdateFitZoom();
            Bounds(out Vector2 min, out Vector2 max);
            SetZoom(Mathf.Clamp(_fitZoom, MinZoom, FitZoomLimit));
            _content.anchoredPosition = -(min + max) * 0.5f * _zoom;
        }

        // 열 때 보여 줄 곳: 지금 살 수 있는 노드 쪽을 FocusZoom() 배율로 가운데에 둔다.
        // 대상 = 살 수 있는 노드, 없으면 드러났지만 Gold가 모자란 노드. 둘 다 없으면(다 샀다) 전체 맞춤.
        // 기준 = 대상 가운데 다음 Rank가 가장 싼 노드(다음에 살 가능성이 가장 높다). 기준에서 그 배율의 한 화면(여백 뺌) 안에 드는
        // 대상만 모아 그 범위의 가운데를 화면 가운데에 둔다 — 기준 노드와 모은 노드는 늘 화면 안이다.
        public void FrameFocus()
        {
            Rect view = _viewport.rect;
            NodeVisual anchor = CheapestIn(NodeState.Purchasable) ?? CheapestIn(NodeState.Revealed);

            if (anchor == null || view.width <= 0 || view.height <= 0)
            {
                FrameAll();
                return;
            }

            float zoom = FocusZoom();
            Vector2 reach = new Vector2(
                Mathf.Max(0, view.width / zoom - FocusMargin),
                Mathf.Max(0, view.height / zoom - FocusMargin)) * 0.5f;
            Vector2 min = anchor.Position;
            Vector2 max = anchor.Position;

            foreach (NodeVisual node in _nodes.Values)
            {
                if (node.State != anchor.State)
                    continue;

                Vector2 offset = node.Position - anchor.Position;

                if (Mathf.Abs(offset.x) > reach.x || Mathf.Abs(offset.y) > reach.y)
                    continue;

                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }

            SetZoom(zoom);
            _content.anchoredPosition = -(min + max) * 0.5f * _zoom;
        }

        // 노드가 화면에서 FocusNodeMillimeters쯤 되는 배율(휴대폰은 1배보다 크다). 화면 밀도를 모르면 FocusMinZoom.
        private float FocusZoom()
        {
            float dpi = Screen.dpi;

            if (dpi <= 0)
                return FocusMinZoom;

            float nodeMillimeters = NodeSize * ScaleFactor() / dpi * 25.4f;
            return Mathf.Clamp(FocusNodeMillimeters / nodeMillimeters, FocusMinZoom, MaxZoom);
        }

        // 그 상태의 노드 가운데 다음 Rank가 가장 싼 것. 값이 같으면 ID 순서. 없으면 null.
        private NodeVisual CheapestIn(NodeState state)
        {
            NodeVisual best = null;

            foreach (NodeVisual node in _nodes.Values)
            {
                if (node.State != state)
                    continue;

                if (best == null || node.Price < best.Price ||
                    (node.Price == best.Price && string.CompareOrdinal(node.Id, best.Id) < 0))
                    best = node;
            }

            return best;
        }

        // 화면이 열린 직후에는 영역 크기가 아직 0일 수 있다. 크기가 생긴 뒤 한 번 맞춤.
        // Build와 Show(노드 상태)는 화면을 여는 같은 프레임에 끝나 있다(ScreenFlow.ShowUpgrade).
        private void LateUpdate()
        {
            if (_framePending && _viewport.rect.width > 0)
            {
                _framePending = false;
                UpdateFitZoom();
                FrameFocus();
            }

            NotifyRegion();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            SetHovered(null);
            DropSelectionForMove();
            ReleasePress();
            _downHeld = false;
            _pendingSelect = null;
            RemoveDrag(eventData.pointerId);
            _drags.Add((eventData.pointerId, eventData.position));
        }

        // 한 손가락(마우스)은 이동. 두 손가락이면 앞의 둘로 핀치하고, 셋째부터의 움직임은 쓰지 않는다.
        // 손가락마다 따로 불리므로, 움직인 손가락 하나의 이전·지금 위치와 다른 손가락의 위치로 계산한다.
        public void OnDrag(PointerEventData eventData)
        {
            int index = DragIndex(eventData.pointerId);

            if (index >= 0 && _drags.Count >= 2)
            {
                Vector2 a = _drags[0].Position;
                Vector2 b = _drags[1].Position;
                _drags[index] = (eventData.pointerId, eventData.position);

                if (index < 2)
                    Pinch(a, b, _drags[0].Position, _drags[1].Position, eventData.pressEventCamera);

                return;
            }

            if (index >= 0)
                _drags[index] = (eventData.pointerId, eventData.position);

            _content.anchoredPosition += eventData.delta / ScaleFactor();
        }

        // 한 손가락을 떼면 남은 손가락은 이어서 이동한다(이동은 delta라 튀지 않는다).
        public void OnEndDrag(PointerEventData eventData) => RemoveDrag(eventData.pointerId);

        // 커서 아래의 점이 그대로 있도록 확대.
        public void OnScroll(PointerEventData eventData)
        {
            SetHovered(null);
            DropSelectionForMove();

            if (Mathf.Approximately(eventData.scrollDelta.y, 0))
                return;

            if (!ToViewport(eventData.position, eventData.enterEventCamera, out Vector2 cursor))
                return;

            ZoomAround(cursor, cursor, _zoom * (eventData.scrollDelta.y > 0 ? 1.1f : 1 / 1.1f));
        }

        // 두 손가락의 이전 위치(a0, b0)와 지금 위치(a1, b1, 화면 좌표). 손가락 사이가 벌어진 만큼 확대하고,
        // 손가락 사이 가운데 아래에 있던 점이 가운데를 따라가게 한다.
        private void Pinch(Vector2 a0, Vector2 b0, Vector2 a1, Vector2 b1, Camera eventCamera)
        {
            if (!ToViewport(a0, eventCamera, out Vector2 localA0) || !ToViewport(b0, eventCamera, out Vector2 localB0) ||
                !ToViewport(a1, eventCamera, out Vector2 localA1) || !ToViewport(b1, eventCamera, out Vector2 localB1))
                return;

            float before = Vector2.Distance(localA0, localB0);
            float zoom = before >= MinPinchDistance ? _zoom * Vector2.Distance(localA1, localB1) / before : _zoom;
            ZoomAround((localA0 + localB0) * 0.5f, (localA1 + localB1) * 0.5f, zoom);
        }

        // 트리 영역 좌표 from 아래에 있던 점이 to로 오도록 배율을 zoom(범위 안으로 자름)으로 바꾼다.
        private void ZoomAround(Vector2 from, Vector2 to, float zoom)
        {
            float before = _zoom;
            SetZoom(Mathf.Clamp(zoom, MinZoom, MaxZoom));
            _content.anchoredPosition = to - (from - _content.anchoredPosition) * (_zoom / before);
        }

        private bool ToViewport(Vector2 screen, Camera eventCamera, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, eventCamera, out local);

        private int DragIndex(int pointerId)
        {
            for (int i = 0; i < _drags.Count; i++)
            {
                if (_drags[i].Id == pointerId)
                    return i;
            }

            return -1;
        }

        private void RemoveDrag(int pointerId)
        {
            int index = DragIndex(pointerId);

            if (index >= 0)
                _drags.RemoveAt(index);
        }

        // 포인터 아래의 노드(레이캐스트를 받는 것은 노드 칸뿐이다). 끄는 중에는 바꾸지 않는다.
        public void OnPointerMove(PointerEventData eventData)
        {
            // 터치는 올림이 없다(손가락이 닿은 채 움직이는 것은 끌기의 시작이지 올림이 아니다).
            if (eventData.dragging || IsTouch(eventData))
                return;

            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            SetHovered(target != null && _nodesByObject.TryGetValue(target, out NodeVisual node) ? node : null);
        }

        // 트리 영역을 벗어났을 때. 영역 안의 노드로 옮겨 간 것이면 그대로 둔다.
        public void OnPointerExit(PointerEventData eventData)
        {
            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            if (target == null || !target.transform.IsChildOf(transform))
                SetHovered(null);
        }

        // 터치로 노드를 누르기 시작했다. 눌림(크기)은 누를 수 있는 노드 전부, 꾹 누름(초록 링 차오름)은 살 수 있는 노드만.
        // 시간은 Update가 센다. 다른 손가락이 이미 누르고 있으면 무시한다.
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pressed != null || !IsTouch(eventData))
                return;

            GameObject target = eventData.pointerPressRaycast.gameObject;

            if (target == null || !_nodesByObject.TryGetValue(target, out NodeVisual node) || node.Far)
                return;

            if (!_downHeld)
            {
                if (_downNode != null && _downNode != node)
                    ApplyScale(_downNode, 1);

                _downNode = node;
                _downPointer = eventData.pointerId;
                _downHeld = true;
                _downVelocity = 0;
                _anticipateUntil = 0;
                _snapPending = false;
            }

            // 잠깐 뒤에 고르고 설명을 띄운다(Update). 그 안에 끌기가 시작되면 OnBeginDrag가 버린다.
            _pendingSelect = node;
            _pendingAt = Time.unscaledTime;

            // 꾹 누름 판정은 살 수 있는 노드와 Gold가 모자란 노드(사려는 시도) 둘 다. 잠긴·최대 노드는 정보만.
            if (node.State != NodeState.Purchasable && node.State != NodeState.Revealed)
                return;

            _pressed = node;
            _pressedPointer = eventData.pointerId;
            _pressedAt = Time.unscaledTime;
            _holdArmed = false;
            StopFade(node);
            RefreshRing(node);
        }

        // 손을 뗐다. 초록 링이 꽉 찬 채 같은 칸에서 뗐으면 산다 — 크기는 스프링으로 돌아오고 초록 링은 잠깐 머물다 사라진다.
        // 클릭(NodeTapped)은 이 뒤에 따로 와서 패널을 다시 고정한다.
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_downHeld && eventData.pointerId == _downPointer)
                _downHeld = false;

            if (_pressed == null || eventData.pointerId != _pressedPointer)
                return;

            NodeVisual node = _pressed;
            bool buy = _holdArmed && node.State == NodeState.Purchasable && eventData.pointerCurrentRaycast.gameObject == node.Root;

            if (buy)
            {
                // 산 경우: 초록 링은 꽉 찬 채로 두고 사라지게 한다. ReleasePress가 비우지 않도록 먼저 건다.
                _fading = node;
                _fadeElapsed = 0;

                // 예비 동작 → 찰싹. 눌림 연출이 이 노드를 잡고 있을 때만(보통 그렇다).
                if (_downNode == node)
                {
                    _anticipateUntil = Time.unscaledTime + AnticipateSeconds;
                    _snapPending = true;
                }
            }

            ReleasePress();

            if (buy)
                NodeHeld?.Invoke(node.Id);
        }

        private void Update()
        {
            AdvanceAnimation();

            if (_pendingSelect != null && Time.unscaledTime - _pendingAt >= SelectDelaySeconds)
            {
                NodeVisual node = _pendingSelect;
                _pendingSelect = null;
                SetSelected(node);
                NodeTapped?.Invoke(node.Id, (RectTransform)node.Root.transform);
            }

            if (_pressed == null)
                return;

            // 살 수 있는 노드: 초록 링이 차오른다. 꽉 차면 살 준비. Gold가 모자란 노드: 링 없이 시간만 재고, 차면 "못 산다"를 알린다.
            bool affordable = _pressed.State == NodeState.Purchasable;
            float progress = Mathf.Clamp01((Time.unscaledTime - _pressedAt) / HoldSeconds);

            if (affordable)
                SetFill(_pressed, progress);

            if (_holdArmed || progress < 1)
                return;

            _holdArmed = true;

            if (!affordable)
            {
                NodeHoldShort?.Invoke(_pressed.Id);
                return;
            }

            _armedBumpUntil = Time.unscaledTime + ArmedBumpSeconds;
            Paint(_pressed);
            NodeHoldArmed?.Invoke(_pressed.Id);
        }

        // 크기: 닿아 있으면 목표로 곧장(PressSeconds), 떼면 스프링(살짝 지나쳤다 돌아온다). 산 직후의 초록 링은 서서히 사라진다.
        private void AdvanceAnimation()
        {
            float delta = Time.unscaledDeltaTime;

            if (_downNode != null)
            {
                if (_downHeld)
                {
                    // 살 수 있는 노드를 누르면 부풀고, 그 밖(정보만·Gold 부족)은 살짝 눌린다.
                    float target = _downNode == _pressed && _pressed.State == NodeState.Purchasable ? PuffScale : PressScale;

                    // 게이지가 꽉 찬 순간: 잠깐 더 부풀었다가 누름 크기로 돌아온다.
                    if (_downNode == _pressed && Time.unscaledTime < _armedBumpUntil)
                        target = ArmedBumpScale;

                    float speed = Mathf.Max(Mathf.Abs(target - 1), ArmedBumpScale - PuffScale) / PressSeconds;
                    _downScale = Mathf.MoveTowards(_downScale, target, delta * speed);
                    _downVelocity = 0;
                }
                else if (Time.unscaledTime < _anticipateUntil)
                {
                    // 예비 동작: 아주 잠깐 더 부푼다.
                    _downScale = Mathf.MoveTowards(_downScale, AnticipateScale, delta * (AnticipateScale - PuffScale) / AnticipateSeconds);
                    _downVelocity = 0;
                }
                else
                {
                    if (_snapPending)
                    {
                        // 찰싹: 안쪽으로 속도를 줘 고무줄처럼 돌아온다.
                        _downVelocity = -SnapKick;
                        _snapPending = false;
                    }

                    float step = Mathf.Min(delta, 1f / 30);
                    _downVelocity += (1 - _downScale) * SpringStiffness * step;
                    _downVelocity *= Mathf.Exp(-SpringDamping * step);
                    _downScale += _downVelocity * step;
                }

                ApplyScale(_downNode, _downScale);

                if (!_downHeld && Time.unscaledTime >= _anticipateUntil && Mathf.Abs(_downScale - 1) < SpringRest && Mathf.Abs(_downVelocity) < SpringRest)
                {
                    ApplyScale(_downNode, 1);
                    _downNode = null;
                    _downScale = 1;
                    _downVelocity = 0;
                    _snapPending = false;
                }
            }

            if (_fading != null)
            {
                _fadeElapsed += delta;
                float t = Mathf.Clamp01(_fadeElapsed / FillFadeSeconds);
                Color color = FillRingColor;
                color.a = 1 - t;
                SetFill(_fading, 1, color);

                if (t >= 1)
                    StopFade(_fading);
            }
        }

        private static void ApplyScale(NodeVisual node, float scale) =>
            node.Root.transform.localScale = new Vector3(scale, scale, 1);

        // 초록 링의 채움(0 ~ 1)과 색. 0이면 끈다.
        private static void SetFill(NodeVisual node, float amount, Color? color = null)
        {
            if (node.FillRing == null)
                return;

            bool on = amount > 0;

            if (node.FillRing.gameObject.activeSelf != on)
                node.FillRing.gameObject.SetActive(on);

            if (!on)
                return;

            node.FillRing.fillAmount = amount;
            node.FillRing.color = color ?? FillRingColor;
        }

        private void StopFade(NodeVisual node)
        {
            if (_fading != node)
                return;

            _fading = null;
            SetFill(node, 0);
        }

        // 흰 링의 켜짐: 고른 노드이거나 꾹 누르는 중인 노드.
        private void RefreshRing(NodeVisual node)
        {
            if (node.SelectRing == null)
                return;

            bool on = node == _selected || node == _pressed;

            if (node.SelectRing.gameObject.activeSelf != on)
                node.SelectRing.gameObject.SetActive(on);

            if (on)
                node.SelectRing.color = SelectRingColor;
        }

        private void ReleasePress()
        {
            if (_pressed == null)
                return;

            NodeVisual node = _pressed;
            _pressed = null;
            _holdArmed = false;

            // 사지 않고 끝났으면(끌기·밖에서 뗌·시간 전에 뗌) 초록 링을 비운다. 산 경우는 _fading이 맡는다.
            if (_fading != node)
                SetFill(node, 0);

            _armedBumpUntil = 0;
            // 살 준비 색을 되돌린다(산 경우는 Show가 다시 칠하지만, 취소면 여기서). Paint가 흰 링도 다시 맞춘다.
            Paint(node);
            NodeHoldReleased?.Invoke(node.Id);
        }

        // 노드 칸에서 눌러 같은 칸에서 뗀 것만 누름으로 본다(노드에 클릭 핸들러가 없어 눌림·뗌이 모두 이 영역으로 올라온다).
        // 끌기가 시작됐으면 EventSystem이 클릭을 주지 않는다.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
                return;

            GameObject pressed = eventData.pointerPressRaycast.gameObject;
            GameObject released = eventData.pointerCurrentRaycast.gameObject;

            if (pressed == null || pressed != released)
                return;

            if (_nodesByObject.TryGetValue(pressed, out NodeVisual node))
            {
                if (node.Far)
                    return;

                if (IsTouch(eventData))
                {
                    SetSelected(node);
                    NodeTapped?.Invoke(node.Id, (RectTransform)node.Root.transform);
                }
                else if (node.State == NodeState.Purchasable)
                {
                    NodeClicked?.Invoke(node.Id);
                }

                return;
            }

            if (pressed == gameObject)
                EmptyTapped?.Invoke();
        }

        // 입력 장치가 터치스크린인가. Input System의 UI 모듈이 주는 포인터 종류로 가른다(Android 여부가 아니라 — 폰 웹·터치 노트북도 맞게).
        private static bool IsTouch(PointerEventData eventData) =>
            eventData is UnityEngine.InputSystem.UI.ExtendedPointerEventData extended
                ? extended.pointerType == UnityEngine.InputSystem.UI.UIPointerType.Touch
                : eventData.pointerId >= 0;

        // 페이지가 닫히면(비활성) 올림·선택과 끄는 손가락을 푼다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable()
        {
            SetHovered(null);
            SetSelected((NodeVisual)null);
            ReleasePress();
            StopAnimation();
            _pendingSelect = null;
            _drags.Clear();
        }

        private void StopAnimation()
        {
            if (_downNode != null)
            {
                ApplyScale(_downNode, 1);
                _downNode = null;
            }

            _downHeld = false;
            _downScale = 1;
            _downVelocity = 0;
            _anticipateUntil = 0;
            _snapPending = false;

            if (_fading != null)
                StopFade(_fading);
        }

        // 보는 곳이 움직이면 선택을 풀고 알린다. 고정 툴팁은 노드 자리에 붙어 있어 따라오지 않으므로 닫는 쪽이 맞다.
        private void DropSelectionForMove()
        {
            if (_selected == null)
                return;

            SetSelected((NodeVisual)null);
            ViewMoved?.Invoke();
        }

        private void SetSelected(NodeVisual node)
        {
            if (node == _selected)
                return;

            NodeVisual before = _selected;
            _selected = node;

            if (before != null)
                Paint(before);

            if (node != null)
                Paint(node);
        }

        private void SetHovered(NodeVisual node)
        {
            if (node == _hovered)
                return;

            if (_hovered != null)
            {
                NodeVisual left = _hovered;
                _hovered = null;
                Paint(left);
                NodeLeft?.Invoke(left.Id);
            }

            if (node == null)
                return;

            _hovered = node;
            Paint(node);
            NodeHovered?.Invoke(node.Id, (RectTransform)node.Root.transform);
        }

        private void SetZoom(float zoom)
        {
            _zoom = zoom;
            _content.localScale = new Vector3(zoom, zoom, 1);
        }

        private float ScaleFactor()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>().rootCanvas;

            return _canvas.scaleFactor;
        }

        // 선 색을 고를 때의 상태: Rank를 하나라도 산 노드는 산 것으로 본다.
        private static NodeState LinkStateOf(NodeVisual node) => node.Rank > 0 ? NodeState.Owned : node.State;

        // 노드 하나를 지금 상태(와 올림)대로 칠한다. 가격은 마지막 Rank까지 사지 않은 노드에만 쓴다.
        private void Paint(NodeVisual node)
        {
            RefreshRing(node);

            if (node.State == NodeState.Hidden)
            {
                // 안개 속: 가격·Rank 딱지는 숨긴다. 열린 구역이면 흐린 자물쇠, 먼 노드면 작고 흐린 점(레이캐스트를 받지 않아 올림·누름이 없다).
                if (_look != null)
                {
                    node.Image.sprite = _style.SpriteFor(node.Far ? _look.FarSprite : _look.SpriteOf(node.Stat, node.State, false));
                    node.Image.color = node.Far ? _look.FarTint : TintOf(node, false);
                    SetSize(node, node.Far ? NodeSize * _look.FarScale : NodeSize);
                    node.Image.raycastTarget = !node.Far;
                }

                if (node.Badge != null && node.Badge.gameObject.activeSelf)
                    node.Badge.gameObject.SetActive(false);

                if (node.RankLabel != null && node.RankLabel.transform.parent.gameObject.activeSelf)
                    node.RankLabel.transform.parent.gameObject.SetActive(false);

                return;
            }

            SetSize(node, NodeSize);
            node.Image.raycastTarget = true;

            if (node.RankLabel != null && !node.RankLabel.transform.parent.gameObject.activeSelf)
                node.RankLabel.transform.parent.gameObject.SetActive(true);

            string price = NumberText.Compact(node.Price);
            bool partial = node.Rank > 0 && node.State != NodeState.Owned;

            if (node.RankLabel != null)
                node.RankLabel.text = node.Rank.ToString(CultureInfo.InvariantCulture) + "/" + node.MaxRank.ToString(CultureInfo.InvariantCulture);

            if (_look == null)
            {
                node.Image.sprite = null;
                Color color = ColorOf(node.State);
                node.Image.color = partial ? Color.Lerp(color, OwnedColor, PartialMix) : color;
                node.Label.color = Color.white;
                node.Label.text = node.State == NodeState.Owned ? $"{node.Id}\nowned" : $"{node.Id}\n{price}";
                return;
            }

            // 고른 노드는 그림(초록 강조)은 바꾸지 않고 — 아직 산 게 아니다 — 색만 온전한 밝기로 올린다. 링이 고름을 보인다. 올림(마우스)은 그림도 밝힌다.
            bool lit = node == _hovered;
            node.Image.sprite = _style.SpriteFor(_look.SpriteOf(node.Stat, node.State, lit));
            Color tint = TintOf(node, lit);
            node.Image.color = partial ? Color.Lerp(tint, OwnedColor, PartialMix) : tint;

            bool showPrice = node.State != NodeState.Owned;
            if (node.Badge != null && node.Badge.gameObject.activeSelf != showPrice)
                node.Badge.gameObject.SetActive(showPrice);

            if (!showPrice || node.Label.text == price)
            {
                node.Label.color = PriceColorOf(node.State);
                return;
            }

            node.Label.color = PriceColorOf(node.State);
            node.Label.text = price;

            if (node.Badge != null)
                node.Badge.sizeDelta = new Vector2(node.Label.GetPreferredValues(price).x + PricePadding, PriceHeight);
        }

        // 노드 색: 고른 노드(설명이 떠 있는 노드)는 상태와 무관하게 온전한 흰색(가장 밝고 불투명). 아니면 Look의 상태별 색.
        // 꾹 눌러 게이지가 꽉 찬 노드는 초록 쪽으로 살짝 물든다(살 준비).
        private Color TintOf(NodeVisual node, bool lit)
        {
            if (node == _pressed && _holdArmed && node.State == NodeState.Purchasable)
                return Color.Lerp(Color.white, FillRingColor, ArmedTint);

            return node == _selected ? Color.white : _look.TintOf(node.State, lit);
        }

        // 노드 칸 크기(먼 노드는 점으로 줄인다). 바뀔 때만 쓴다.
        private static void SetSize(NodeVisual node, float size)
        {
            var rect = (RectTransform)node.Root.transform;

            if (!Mathf.Approximately(rect.sizeDelta.x, size))
                rect.sizeDelta = new Vector2(size, size);
        }

        private static Color ColorOf(NodeState state)
        {
            switch (state)
            {
                case NodeState.Owned: return OwnedColor;
                case NodeState.Purchasable: return PurchasableColor;
                default: return RevealedColor;
            }
        }

        #region 만들기

        private NodeVisual CreateNode(NodeItem item)
        {
            RectTransform rect = Child(_nodeLayer, "Node " + item.Id);
            rect.sizeDelta = new Vector2(NodeSize, NodeSize);
            var position = new Vector2(item.X * CellSize, item.Y * CellSize);
            rect.anchoredPosition = position;

            // 누름은 트리 영역(OnPointerClick)이 받는다. 노드에 Button을 두면 클릭을 가져가 입력 장치를 가를 수 없다.
            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;

            // 고른 노드의 링: 노드 위에 그리는 얇은 흰 고리(가운데는 비어 노드가 보인다). 고를 때만 켠다.
            RectTransform ringRect = Child(rect, "Select");
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta = new Vector2(NodeSize + SelectRingExtra, NodeSize + SelectRingExtra);
            var ringImage = ringRect.gameObject.AddComponent<Image>();
            ringImage.sprite = RingSprite();
            ringImage.color = SelectRingColor;
            ringImage.raycastTarget = false;
            ringRect.gameObject.SetActive(false);

            // 초록 링: 흰 링과 같은 자리·같은 그림. 위에서 시작해 시계 방향으로 차오른다(Filled Radial360).
            RectTransform fillRect = Child(rect, "Fill");
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.sizeDelta = ringRect.sizeDelta;
            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = RingSprite();
            fillImage.color = FillRingColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.fillAmount = 0;
            fillImage.raycastTarget = false;
            fillRect.gameObject.SetActive(false);

            RectTransform badge = null;
            RectTransform labelRect;

            if (_look != null)
            {
                // 가격 딱지: 노드 바로 아래 가운데. 폭은 칠할 때 글자에 맞춘다.
                badge = Child(rect, "Price");
                badge.anchorMin = new Vector2(0.5f, 0);
                badge.anchorMax = new Vector2(0.5f, 0);
                badge.pivot = new Vector2(0.5f, 1);
                badge.sizeDelta = new Vector2(NodeSize, PriceHeight);
                var back = badge.gameObject.AddComponent<Image>();
                back.color = BadgeBackColor();
                back.raycastTarget = false;
                labelRect = Child(badge, "Label");
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
            }
            else
            {
                labelRect = Child(rect, "Label");
            }

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_Text rankLabel = item.MaxRank > 1 ? CreateRankBadge(rect) : null;

            if (_look != null)
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

            return new NodeVisual(item.Id, item.Stat, item.Price, item.MaxRank, item.Start, position, rect.gameObject, image, label, badge, rankLabel,
                ringImage, fillImage);
        }

        private static Sprite _ringSprite;

        // 흰 고리 그림을 한 번 만든다(프로젝트에 고리 스프라이트가 없다). 가장자리는 1px 부드럽게.
        private static Sprite RingSprite()
        {
            if (_ringSprite != null)
                return _ringSprite;

            int size = RingTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float center = (size - 1) * 0.5f;
            float outer = center - 1;
            float inner = outer - RingThickness;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = Mathf.Clamp01(outer + 0.5f - distance) * Mathf.Clamp01(distance - inner + 0.5f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }

            texture.Apply();
            _ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
            return _ringSprite;
        }

        // Rank 딱지: 노드 오른쪽 위 모서리에 걸친다. 글자는 칠할 때 쓴다("3/10").
        private TMP_Text CreateRankBadge(RectTransform node)
        {
            RectTransform badge = Child(node, "Rank");
            badge.anchorMin = new Vector2(1, 1);
            badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(1, 1);
            badge.anchoredPosition = new Vector2(RankPadding, RankPadding);
            badge.sizeDelta = new Vector2(NodeSize * 0.6f, RankHeight);

            var back = badge.gameObject.AddComponent<Image>();
            back.color = BadgeBackColor();
            back.raycastTarget = false;

            RectTransform textRect = Child(badge, "Label");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = RankSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = PriceColorOf(NodeState.Purchasable);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        // 두 노드 중심을 잇는 얇은 막대: 가운데에 놓고, 길이만큼 늘리고, 방향만큼 돌린다.
        private LinkVisual CreateLink(NodeVisual from, NodeVisual to)
        {
            RectTransform rect = Child(_linkLayer, $"Link {from.Id}-{to.Id}");
            Vector2 delta = to.Position - from.Position;
            rect.anchoredPosition = (from.Position + to.Position) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, LinkWidth);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return new LinkVisual(from, to, rect.gameObject, image);
        }

        private void Clear()
        {
            SetHovered(null);
            _selected = null;
            _pressed = null;
            _downNode = null;
            _downHeld = false;
            _downScale = 1;
            _downVelocity = 0;
            _anticipateUntil = 0;
            _snapPending = false;
            _fading = null;
            _pendingSelect = null;

            foreach (NodeVisual node in _nodes.Values)
                Destroy(node.Root);

            foreach (LinkVisual link in _links)
                Destroy(link.Root);

            _nodes.Clear();
            _nodesByObject.Clear();
            _links.Clear();
            _hasRegions = false;
            _openRegions.Clear();
            _viewedRegion = null;
        }

        private static RectTransform Layer(RectTransform parent, string name)
        {
            RectTransform rect = Child(parent, name);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        // 부모 가운데에 붙는 자식.
        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private sealed class NodeVisual
        {
            public readonly string Id;
            public readonly string Stat;
            public readonly int MaxRank;
            // 시작 노드. 그 구역은 처음부터 열린 구역이다.
            public readonly bool Start;
            // 든 구역의 열쇠. 구역에 들지 않았으면 null.
            public string Region;
            // 선으로 이어진 노드.
            public readonly List<NodeVisual> Neighbors = new List<NodeVisual>();
            // 미리 보기 거리 안의 안개 속 노드(MarkAhead).
            public bool Ahead;
            // 먼 노드(열린 구역 밖이고 미리 보기 거리 밖인 안개 속 노드). Show가 정한다.
            public bool Far;
            // 다음 Rank의 비용.
            public long Price;
            // 산 Rank.
            public int Rank;
            public readonly Vector2 Position;
            public readonly GameObject Root;
            public readonly Image Image;
            public readonly TMP_Text Label;
            // 가격 딱지(Look이 있을 때만). 샀으면 숨긴다.
            public readonly RectTransform Badge;
            // Rank 딱지의 글자(Rank가 둘 이상인 노드만).
            public readonly TMP_Text RankLabel;
            // 흰 링(고름·꾹 누름)과 그 위에 차오르는 초록 링(꾹 누름 진행 → 살 준비 → 산 뒤 사라짐).
            public readonly Image SelectRing;
            public readonly Image FillRing;
            public NodeState State = NodeState.Hidden;

            public NodeVisual(string id, string stat, long price, int maxRank, bool start, Vector2 position, GameObject root, Image image,
                TMP_Text label, RectTransform badge, TMP_Text rankLabel, Image selectRing, Image fillRing)
            {
                Id = id;
                Stat = stat;
                Price = price;
                MaxRank = maxRank;
                Start = start;
                RankLabel = rankLabel;
                Position = position;
                Root = root;
                Image = image;
                Label = label;
                SelectRing = selectRing;
                FillRing = fillRing;
                Badge = badge;
            }
        }

        private sealed class LinkVisual
        {
            public readonly NodeVisual From;
            public readonly NodeVisual To;
            public readonly GameObject Root;
            public readonly Image Image;

            public LinkVisual(NodeVisual from, NodeVisual to, GameObject root, Image image)
            {
                From = from;
                To = to;
                Root = root;
                Image = image;
            }
        }

        #endregion
    }
}
