using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 창(이 RectTransform) 안에서 내용(Content)을 끌어 옮기고 확대·축소하는 뷰포트. 게임을 모른다 — 내용이 무엇인지는 붙이는 쪽이 정한다.
    // - 한 손가락(마우스)은 이동, 두 손가락은 핀치, 휠은 커서 아래 점을 둔 채 확대.
    // - 축소 하한: 내용 전체가 창에 들어가는 배율(FitZoom)과 MinZoomDefault 중 작은 쪽. MinZoomFloor 아래로는 안 간다.
    //   FitZoom은 SetContentBounds와 창 크기 변화 때 다시 잰다.
    // - 끌기·휠을 받으려면 창 자체가 레이캐스트 대상이어야 하므로 Graphic이 없으면 보이지 않는 Image를 깐다. 내용은 RectMask2D로 창에 자른다.
    // 같은 GameObject의 다른 컴포넌트도 포인터 사건을 그대로 받는다(EventSystem은 사건을 모든 핸들러에 보낸다).
    [DisallowMultipleComponent]
    public sealed class PanZoomView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float WheelStep = 1.1f;

        // 옮기고 키우는 내용. 창의 자식 RectTransform. 붙이는 쪽이 넣는다.
        public RectTransform Content { get; set; }

        public float MinZoomDefault { get; set; } = 0.3f;
        public float MinZoomFloor { get; set; } = 0.08f;
        public float MaxZoom { get; set; } = 2.5f;
        // FrameAll이 쓰는 배율의 상한(내용이 작아도 이보다 크게 보이지 않는다).
        public float FitZoomLimit { get; set; } = 1.5f;
        // 두 손가락 사이가 이보다 가까우면(창 좌표) 배율은 두고 이동만 한다.
        public float MinPinchDistance { get; set; } = 20f;

        public float Zoom { get; private set; } = 1f;
        // 내용 전체가 창에 들어가는 배율. 내용 범위가 없거나 창 크기가 0이면 1.
        public float FitZoom { get; private set; } = 1f;
        public float MinZoom => Mathf.Max(MinZoomFloor, Mathf.Min(MinZoomDefault, FitZoom));

        // 보는 곳이 움직이기 시작했다(끌기·휠·핀치). 붙이는 쪽이 올림·선택·누름을 푼다.
        public event Action MoveStarted;

        private RectTransform _viewport;
        private Canvas _canvas;
        private bool _hasBounds;
        private Vector2 _boundsMin;
        private Vector2 _boundsMax;
        // 끄는 중인 포인터(손가락)와 화면 위치. 둘 이상이면 앞의 둘로 핀치한다.
        private readonly List<(int Id, Vector2 Position)> _drags = new(2);

        public RectTransform Viewport => _viewport ??= (RectTransform)transform;

        // 루트 캔버스의 배율(화면 픽셀 → 캔버스 단위).
        public float CanvasScaleFactor
        {
            get
            {
                if (_canvas == null)
                    _canvas = GetComponentInParent<Canvas>().rootCanvas;

                return _canvas.scaleFactor;
            }
        }

        private void Awake()
        {
            if (GetComponent<Graphic>() == null)
                gameObject.AddComponent<Image>().color = Color.clear;

            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();
        }

        // 내용의 범위(내용 좌표, 여백 포함). FitZoom을 다시 잰다. 범위가 없으면 Clear로 지운다.
        public void SetContentBounds(Vector2 min, Vector2 max)
        {
            _hasBounds = true;
            _boundsMin = min;
            _boundsMax = max;
            RefreshFit();
        }

        public void ClearContentBounds()
        {
            _hasBounds = false;
            RefreshFit();
        }

        // 창 크기로 FitZoom을 다시 잰다. 창이 열린 직후 크기가 0에서 생긴 뒤에 한 번 더 부른다.
        public void RefreshFit()
        {
            Rect view = Viewport.rect;

            if (!_hasBounds || view.width <= 0 || view.height <= 0)
            {
                FitZoom = 1f;
                return;
            }

            Vector2 size = _boundsMax - _boundsMin;
            FitZoom = Mathf.Min(view.width / Mathf.Max(size.x, 0.0001f), view.height / Mathf.Max(size.y, 0.0001f));
        }

        // 내용 전체가 보이게 맞춘다. 범위의 가운데가 창 가운데에 온다. 범위가 없으면 배율 1, 원점.
        public void FrameAll()
        {
            RefreshFit();

            if (!_hasBounds || Viewport.rect.width <= 0 || Viewport.rect.height <= 0)
            {
                Frame(Vector2.zero, 1f);
                return;
            }

            Frame((_boundsMin + _boundsMax) * 0.5f, Mathf.Clamp(FitZoom, MinZoom, FitZoomLimit));
        }

        // 내용 좌표 center가 창 가운데에 오도록 zoom 배율로 둔다. zoom은 자르지 않는다 — 부르는 쪽이 범위를 정한다.
        public void Frame(Vector2 center, float zoom)
        {
            SetZoom(zoom);

            if (Content != null)
                Content.anchoredPosition = -center * Zoom;
        }

        // 지금 창 가운데에 있는 내용 좌표.
        public Vector2 CenterInContent =>
            Content != null ? -Content.anchoredPosition / Mathf.Max(Zoom, 0.0001f) : Vector2.zero;

        // 창 크기가 바뀌면(회전·창 크기 조절) 하한도 바뀐다. 지금 배율이 새 하한 아래면 끌어올린다.
        private void OnRectTransformDimensionsChange()
        {
            if (Content == null)
                return;

            RefreshFit();

            if (Zoom < MinZoom)
                SetZoom(MinZoom);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            MoveStarted?.Invoke();
            RemoveDrag(eventData.pointerId);
            _drags.Add((eventData.pointerId, eventData.position));
        }

        // 한 손가락(마우스)은 이동. 두 손가락이면 앞의 둘로 핀치하고, 셋째부터의 움직임은 쓰지 않는다.
        // 손가락마다 따로 불리므로, 움직인 손가락 하나의 이전·지금 위치와 다른 손가락의 위치로 계산한다.
        public void OnDrag(PointerEventData eventData)
        {
            if (Content == null)
                return;

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

            Content.anchoredPosition += eventData.delta / CanvasScaleFactor;
        }

        // 한 손가락을 떼면 남은 손가락은 이어서 이동한다(이동은 delta라 튀지 않는다).
        public void OnEndDrag(PointerEventData eventData) => RemoveDrag(eventData.pointerId);

        // 커서 아래의 점이 그대로 있도록 확대.
        public void OnScroll(PointerEventData eventData)
        {
            MoveStarted?.Invoke();

            if (Content == null || Mathf.Approximately(eventData.scrollDelta.y, 0))
                return;

            if (!ToViewport(eventData.position, eventData.enterEventCamera, out Vector2 cursor))
                return;

            ZoomAround(cursor, cursor, Zoom * (eventData.scrollDelta.y > 0 ? WheelStep : 1 / WheelStep));
        }

        // 페이지가 닫히면 끄는 손가락을 잊는다.
        private void OnDisable() => _drags.Clear();

        // 두 손가락의 이전 위치(a0, b0)와 지금 위치(a1, b1, 화면 좌표). 손가락 사이가 벌어진 만큼 확대하고,
        // 손가락 사이 가운데 아래에 있던 점이 가운데를 따라가게 한다.
        private void Pinch(Vector2 a0, Vector2 b0, Vector2 a1, Vector2 b1, Camera eventCamera)
        {
            if (!ToViewport(a0, eventCamera, out Vector2 localA0) || !ToViewport(b0, eventCamera, out Vector2 localB0) ||
                !ToViewport(a1, eventCamera, out Vector2 localA1) || !ToViewport(b1, eventCamera, out Vector2 localB1))
                return;

            float before = Vector2.Distance(localA0, localB0);
            float zoom = before >= MinPinchDistance ? Zoom * Vector2.Distance(localA1, localB1) / before : Zoom;
            ZoomAround((localA0 + localB0) * 0.5f, (localA1 + localB1) * 0.5f, zoom);
        }

        // 창 좌표 from 아래에 있던 점이 to로 오도록 배율을 zoom(범위 안으로 자름)으로 바꾼다.
        private void ZoomAround(Vector2 from, Vector2 to, float zoom)
        {
            float before = Zoom;
            SetZoom(Mathf.Clamp(zoom, MinZoom, MaxZoom));
            Content.anchoredPosition = to - (from - Content.anchoredPosition) * (Zoom / before);
        }

        private void SetZoom(float zoom)
        {
            Zoom = zoom;

            if (Content != null)
                Content.localScale = new Vector3(zoom, zoom, 1);
        }

        private bool ToViewport(Vector2 screen, Camera eventCamera, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, screen, eventCamera, out local);

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
    }
}
