using System;
using System.Collections.Generic;
using UnityEngine;

// Coordinates UI lifecycle and presentation application.
// Presentation decisions remain in the resolver.
public sealed partial class UIManager
{
    private readonly Dictionary<Type, UIBase> _views = new();
    // View data key (UIViewKey) → type. Two types with one key would read each other's bindings.
    private readonly Dictionary<string, Type> _typesByKey = new();
    private readonly Stack<UIBase> _panelStack = new();

    private readonly Transform _rootLayer;
    private readonly Transform _panelLayer;
    // _overlayLayer: UIManager.Overlay.cs
    private readonly UIResolver _resolver;
    private readonly UIPresentationApplier _presentationApplier;

    private readonly int _panelKeepAliveDepth;
    private readonly float _coveredPanelAlpha;

    public UIBase CurrentRoot { get; private set; }
    public bool HasPanel => _panelStack.Count > 0;
    // 맨 위에 쌓인 Panel. 없으면 null.
    public UIBase TopPanel => _panelStack.Count > 0 ? _panelStack.Peek() : null;

    public DisplayContext LastDisplay { get; private set; }
    public UIResolveResult LastResult { get; private set; }

    // overlayLayer: Overlay 층(Root·Panel 위). 비우면 Panel 층 다음 형제로 만든다.
    public UIManager(
        Transform rootLayer,
        Transform panelLayer,
        UIResolver resolver,
        UIPresentationApplier presentationApplier,
        int panelKeepAliveDepth = 2,
        float coveredPanelAlpha = 0.95f,
        Transform overlayLayer = null)
    {
        _rootLayer = rootLayer;
        _panelLayer = panelLayer;
        _overlayLayer = overlayLayer != null ? overlayLayer : CreateOverlayLayer(panelLayer);
        _resolver = resolver;
        _presentationApplier = presentationApplier;

        _panelKeepAliveDepth = Mathf.Max(1, panelKeepAliveDepth);
        _coveredPanelAlpha = Mathf.Clamp01(coveredPanelAlpha);
    }

    public void Register(UIBase view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        Type type = view.GetType();
        if (_views.TryGetValue(type, out UIBase existing) && existing != view)
        {
            throw new InvalidOperationException(
                $"[UIManager] Duplicate View type '{type.Name}'. " +
                "One concrete View type must identify one registered View instance.");
        }

        string key = UIViewKey.Of(type);
        if (_typesByKey.TryGetValue(key, out Type other) && other != type)
        {
            throw new InvalidOperationException(
                $"[UIManager] View types '{other.FullName}' and '{type.FullName}' share the key '{key}'. " +
                "Rename one or pin a key with [UIViewKey].");
        }

        view.EnsureInitialized();
        // Editor and development builds: report prefab children that the View's Refs expect but are missing.
        UIRefCheck.WarnMissing(view);
        _views[type] = view;
        _typesByKey[key] = type;
    }

    public T GetUI<T>() where T : UIBase
    {
        return _views.TryGetValue(typeof(T), out UIBase view)
            ? (T)view
            : null;
    }

    private T Require<T>() where T : UIBase
    {
        T view = GetUI<T>();
        if (view != null)
            return view;

        throw new InvalidOperationException(
            $"[UIManager] View '{typeof(T).Name}' is not registered.");
    }

    private static void SetVisible(UIBase view, bool visible)
    {
        if (view.gameObject.activeSelf != visible)
            view.gameObject.SetActive(visible);
    }

    private static void Mount(UIBase view, Transform layer)
    {
        if (view.transform.parent != layer)
            view.transform.SetParent(layer, worldPositionStays: false);

        view.transform.SetAsLastSibling();
    }

    private static void ApplyPanelState(
        UIBase panel,
        bool active,
        bool interactable,
        bool blocksRaycasts,
        float alpha)
    {
        if (panel == null)
            return;

        if (panel.gameObject.activeSelf != active)
            panel.gameObject.SetActive(active);

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = active ? alpha : 0f;
        canvasGroup.interactable = active && interactable;
        canvasGroup.blocksRaycasts = active && blocksRaycasts;
    }
}
