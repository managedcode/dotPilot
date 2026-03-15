using Windows.System;

namespace DotPilot.Presentation;

public sealed class ChatComposerModifierState
{
    private readonly Dictionary<VirtualKey, int> pressedModifierKeys = [];

    public bool HasPressedModifier => pressedModifierKeys.Count > 0;

    public void RegisterKeyDown(VirtualKey key)
    {
        var normalizedKey = NormalizeModifierKey(key);
        if (normalizedKey is null)
        {
            return;
        }

        pressedModifierKeys.TryGetValue(normalizedKey.Value, out var count);
        pressedModifierKeys[normalizedKey.Value] = count + 1;
    }

    public void RegisterKeyUp(VirtualKey key)
    {
        var normalizedKey = NormalizeModifierKey(key);
        if (normalizedKey is null ||
            !pressedModifierKeys.TryGetValue(normalizedKey.Value, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            pressedModifierKeys.Remove(normalizedKey.Value);
            return;
        }

        pressedModifierKeys[normalizedKey.Value] = count - 1;
    }

    public void Reset()
    {
        pressedModifierKeys.Clear();
    }

    private static VirtualKey? NormalizeModifierKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => VirtualKey.Shift,
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => VirtualKey.Control,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => VirtualKey.Menu,
            VirtualKey.LeftWindows or VirtualKey.RightWindows => VirtualKey.LeftWindows,
            _ => null,
        };
    }
}
