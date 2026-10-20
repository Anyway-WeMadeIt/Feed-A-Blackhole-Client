using System;
using System.Collections.Generic;

public sealed partial class UIManager
{
    private readonly Dictionary<UIBase, UIPresentationSpec>
        _panelPresentations = new();

    // If the panel already exists in the stack, pop back to it.
    // A -> B -> C, then Push(B) becomes A -> B.
    //
    // afterPopped handles cleanup for panels removed during the pop.
    public T PushPanel<T>(
        UIPresentationSpec presentation,
        Action<T> afterPresented = null,
        Action<UIBase> afterClosed = null)
        where T : UIBase, IUIPanel
    {
        if (presentation == null)
            throw new ArgumentNullException(nameof(presentation));

        T panel = Require<T>();

        if (_panelStack.Contains(panel))
        {
            PopUntil(panel, afterClosed);
        }
        else
        {
            _panelStack.Push(panel);
        }

        _panelPresentations[panel] = presentation;

        Mount(panel, _panelLayer);

        DisplayContext display = UnityDisplayContextProvider.GetCurrent();
        ApplyPresentation(panel, presentation, display);

        ApplyPanelStackState();

        afterPresented?.Invoke(panel);
        return panel;
    }

    public UIBase PopPanel(Action<UIBase> afterClosed = null)
    {
        if (_panelStack.Count == 0)
            return null;

        UIBase popped = _panelStack.Pop();
        ClosePage(popped, afterClosed);

        _panelPresentations.Remove(popped);
        ApplyPanelState(
            popped,
            active: false,
            interactable: false,
            blocksRaycasts: false,
            alpha: 0f);

        afterClosed?.Invoke(popped);

        if (_panelStack.Count > 0)
        {
            DisplayContext display = UnityDisplayContextProvider.GetCurrent();

            ReapplyPanelStack(display);
            ApplyPanelStackState();
        }

        return popped;
    }

    public void PopAllPanels(Action<UIBase> afterClosed = null)
    {
        while (_panelStack.Count > 0)
            PopPanel(afterClosed);
    }

    public void ReapplyVisible(in DisplayContext display)
    {
        ReapplyCurrentRoot(display);

        if (_panelStack.Count == 0)
            return;

        ReapplyPanelStack(display);
        ApplyPanelStackState();
    }

    private void PopUntil(
        UIBase target,
        Action<UIBase> afterClosed = null)
    {
        while (_panelStack.Count > 0 &&
               _panelStack.Peek() != target)
        {
            UIBase popped = _panelStack.Pop();

            ClosePage(popped, afterClosed);

            _panelPresentations.Remove(popped);
            ApplyPanelState(
                popped,
                active: false,
                interactable: false,
                blocksRaycasts: false,
                alpha: 0f);

            afterClosed?.Invoke(popped);
        }
    }

    // Keep live panels in sync with the current display.
    private void ReapplyPanelStack(in DisplayContext display)
    {
        int keep = Math.Max(1, _panelKeepAliveDepth);
        var livePanels = new List<UIBase>(keep);

        foreach (UIBase panel in _panelStack)
        {
            if (livePanels.Count >= keep)
                break;

            livePanels.Add(panel);
        }

        // Apply bottom-up so the top panel remains the last resolved result.
        for (int i = livePanels.Count - 1; i >= 0; i--)
        {
            UIBase panel = livePanels[i];

            if (_panelPresentations.TryGetValue(
                    panel,
                    out UIPresentationSpec presentation))
            {
                ApplyPresentation(panel, presentation, display);
                ReapplyCurrentPage(panel, display);
            }
        }
    }

    private void ApplyPanelStackState()
    {
        if (_panelStack.Count == 0)
            return;

        int keep = Math.Max(1, _panelKeepAliveDepth);
        int index = 0;

        foreach (UIBase panel in _panelStack)
        {
            bool keepAlive = index < keep;

            if (!keepAlive)
            {
                ApplyPanelState(
                    panel,
                    active: false,
                    interactable: false,
                    blocksRaycasts: false,
                    alpha: 0f);

                index++;
                continue;
            }

            if (index == 0)
            {
                panel.transform.SetAsLastSibling();

                ApplyPanelState(
                    panel,
                    active: true,
                    interactable: true,
                    blocksRaycasts: true,
                    alpha: 1f);
            }
            else
            {
                ApplyPanelState(
                    panel,
                    active: true,
                    interactable: false,
                    blocksRaycasts: false,
                    alpha: _coveredPanelAlpha);
            }

            index++;
        }
    }
}