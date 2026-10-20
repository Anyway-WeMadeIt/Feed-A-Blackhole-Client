using UnityEngine;

public sealed class UIDisplayRefreshDriver : MonoBehaviour
{
    [SerializeField]
    private bool reapplyOnDisplayChange = true;

    private UIManager _ui;
    private DisplayContext _lastDisplay;
    private bool _initialized;

    public void Initialize(UIManager ui)
    {
        _ui = ui;

        _lastDisplay = UnityDisplayContextProvider.GetCurrent();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || !reapplyOnDisplayChange)
            return;
        
        DisplayContext current = UnityDisplayContextProvider.GetCurrent();

        if (current == _lastDisplay)
            return;

        _lastDisplay = current;

        if (_ui.CurrentRoot == null)
            return;

        _ui.ReapplyVisible(current);
    }
}