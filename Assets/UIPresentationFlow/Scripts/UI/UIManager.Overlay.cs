using System;
using System.Collections.Generic;
using UnityEngine;

// Overlay 층: Root·Panel 층 위, 화면 전환 덮개 아래. 토스트·알림·엔딩처럼 스택 순서와 무관하게 위에 얹히는 뷰가 산다.
// - 스택이 아니다: 여럿이 동시에 보이고, 나중에 보인 것이 위다. TopPanel·HasPanel에 들지 않는다.
// - 보이는 동안 테마·표시(DisplayContext)가 바뀌면 Panel처럼 다시 적용한다(ReapplyVisible).
// - 프리팹 없이 코드로 만드는 뷰는 OverlayLayer 아래에 직접 두면 같은 자리에 온다.
public sealed partial class UIManager
{
    private readonly Transform _overlayLayer;
    private readonly Dictionary<UIBase, UIPresentationSpec> _overlayPresentations = new();
    // 보이는 Overlay, 보인 순서대로.
    private readonly List<UIBase> _shownOverlays = new();

    // Overlay가 놓이는 층. 코드로 만드는 뷰(엔딩·알림 등)는 여기에 자식으로 붙인다.
    public Transform OverlayLayer => _overlayLayer;

    public bool HasOverlay => _shownOverlays.Count > 0;

    // Overlay를 보인다. 이미 보이는 중이면 맨 위로 올리고 Presentation만 다시 적용한다.
    public T ShowOverlay<T>(
        UIPresentationSpec presentation,
        Action<T> afterPresented = null)
        where T : UIBase, IUIOverlay
    {
        if (presentation == null)
            throw new ArgumentNullException(nameof(presentation));

        T overlay = Require<T>();

        if (!_shownOverlays.Contains(overlay))
            _shownOverlays.Add(overlay);

        _overlayPresentations[overlay] = presentation;

        Mount(overlay, _overlayLayer);
        ApplyPresentation(overlay, presentation, UnityDisplayContextProvider.GetCurrent());
        SetVisible(overlay, true);

        afterPresented?.Invoke(overlay);
        return overlay;
    }

    public bool IsOverlayShown<T>() where T : UIBase, IUIOverlay
    {
        T overlay = GetUI<T>();
        return overlay != null && _shownOverlays.Contains(overlay);
    }

    // Overlay를 숨긴다. 보이지 않았으면 아무것도 하지 않고 null.
    public UIBase HideOverlay<T>(Action<UIBase> afterClosed = null) where T : UIBase, IUIOverlay
    {
        T overlay = GetUI<T>();

        if (overlay == null || !_shownOverlays.Remove(overlay))
            return null;

        ClosePage(overlay);
        _overlayPresentations.Remove(overlay);
        SetVisible(overlay, false);

        afterClosed?.Invoke(overlay);
        return overlay;
    }

    public void HideAllOverlays(Action<UIBase> afterClosed = null)
    {
        for (int i = _shownOverlays.Count - 1; i >= 0; i--)
        {
            UIBase overlay = _shownOverlays[i];
            _shownOverlays.RemoveAt(i);
            ClosePage(overlay);
            _overlayPresentations.Remove(overlay);
            SetVisible(overlay, false);
            afterClosed?.Invoke(overlay);
        }
    }

    // 보이는 Overlay(와 그 Page)에 지금 표시(테마·언어·크기)를 다시 적용한다. 보인 순서대로.
    // 적용 중 OnPresented가 Overlay를 열고 닫아도 목록이 흔들리지 않게 복사본을 돈다.
    private void ReapplyOverlays(in DisplayContext display)
    {
        if (_shownOverlays.Count == 0)
            return;

        foreach (UIBase overlay in _shownOverlays.ToArray())
        {
            if (!_overlayPresentations.TryGetValue(overlay, out UIPresentationSpec presentation))
                continue;

            ApplyPresentation(overlay, presentation, display);
            ReapplyCurrentPage(overlay, display);
        }
    }

    // 층을 넘기지 않았을 때: Panel 층 바로 다음 형제로 꽉 채운 층을 만든다(화면 전환 덮개는 그 뒤에 만들어져 위에 온다).
    private static Transform CreateOverlayLayer(Transform panelLayer)
    {
        if (panelLayer == null)
            throw new ArgumentNullException(nameof(panelLayer), "[UIManager] Overlay 층을 만들 Panel 층이 없다.");

        var layer = new GameObject("OverlayLayer", typeof(RectTransform));
        layer.layer = panelLayer.gameObject.layer;

        var rect = (RectTransform)layer.transform;
        rect.SetParent(panelLayer.parent, false);
        rect.SetSiblingIndex(panelLayer.GetSiblingIndex() + 1);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
