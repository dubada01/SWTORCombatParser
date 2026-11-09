#if LINUX || MACOS
using System;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler;

// libuiohook allows one global hook per process. Share the keyboard hook instead
// of creating competing native listeners or selecting events on the X11 root.
public class MouseHookHandler
{
    private bool _subscribed;
    public event Action<Point> MouseClicked = delegate { };
    public void SubscribeToClicks()
    {
        if (_subscribed) return;
        if (!HotkeyHandler.GlobalInputAvailable)
            throw new InvalidOperationException("Global click capture is unavailable. See Settings → Overlay details for permissions/session guidance.");
        HotkeyHandler.GlobalMousePressed += OnMousePressed;
        _subscribed = true;
    }
    public void UnsubscribeFromClicks()
    {
        if (!_subscribed) return;
        HotkeyHandler.GlobalMousePressed -= OnMousePressed;
        _subscribed = false;
    }
    private void OnMousePressed(Point position) => MouseClicked.InvokeSafely(position);
}
#endif
