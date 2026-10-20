using System;
using System.Collections.Generic;

public sealed partial class UIManager
{
    private sealed class PageState
    {
        public UIBase Page;
        public UIPresentationSpec Presentation;
    }

    private readonly Dictionary<UIBase, PageState> _pagesByOwner = new();

    public TPage SwitchPage<TPage>(
        UIBase owner,
        UIPresentationSpec presentation,
        Action<TPage> afterPresented = null,
        Action<UIBase> afterClosed = null)
        where TPage : UIBase, IUIPage
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        if (presentation == null)
            throw new ArgumentNullException(nameof(presentation));

        if (owner is not IUIPageOwner pageOwner)
        {
            throw new InvalidOperationException(
                $"[UIManager] View '{owner.GetType().Name}' does not support Pages.");
        }

        if (!IsLivePageOwner(owner))
        {
            throw new InvalidOperationException(
                $"[UIManager] View '{owner.GetType().Name}' is not a live Page Owner.");
        }

        TPage page = Require<TPage>();

        ValidatePageOwner(page, pageOwner);

        if (_pagesByOwner.TryGetValue(owner, out PageState current) &&
            current.Page != page)
        {
            SetVisible(current.Page, false);
            afterClosed?.Invoke(current.Page);
        }

        _pagesByOwner[owner] = new PageState
        {
            Page = page,
            Presentation = presentation
        };

        ApplyPresentation(
            page,
            presentation,
            UnityDisplayContextProvider.GetCurrent());

        SetVisible(page, true);

        afterPresented?.Invoke(page);

        return page;
    }
    
    public UIBase ClosePage(
        UIBase owner,
        Action<UIBase> afterClosed = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        if (!_pagesByOwner.TryGetValue(owner, out PageState state))
            return null;

        _pagesByOwner.Remove(owner);

        SetVisible(state.Page, false);

        afterClosed?.Invoke(state.Page);

        return state.Page;
    }
    
    private bool ReapplyCurrentPage(
        UIBase owner,
        in DisplayContext display)
    {
        if (!_pagesByOwner.TryGetValue(owner, out PageState state))
            return false;

        ApplyPresentation(
            state.Page,
            state.Presentation,
            display);

        return true;
    }
    
    private bool IsLivePageOwner(UIBase owner)
    {
        return CurrentRoot == owner ||
               _panelStack.Contains(owner);
    }

    private static void ValidatePageOwner(
        UIBase page,
        IUIPageOwner owner)
    {
        if (page.transform.parent == owner.PageRoot)
            return;

        throw new InvalidOperationException(
            $"[UIManager] Page '{page.GetType().Name}' must be a child of " +
            $"'{owner.PageRoot.name}'.");
    }
}