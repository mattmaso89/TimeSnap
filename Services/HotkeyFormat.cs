namespace TimeSnap.Services;

// Formats a (modifiers, virtualKey) pair into a display string like "Strg+Alt+Shift+Y".
internal static class HotkeyFormat
{
    public static string Format(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add(TimeSnap.Loc.Get("HotkeyModifierCtrl"));
        if ((modifiers & NativeMethods.MOD_ALT) != 0)     parts.Add(TimeSnap.Loc.Get("HotkeyModifierAlt"));
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0)   parts.Add(TimeSnap.Loc.Get("HotkeyModifierShift"));
        if ((modifiers & NativeMethods.MOD_WIN) != 0)     parts.Add(TimeSnap.Loc.Get("HotkeyModifierWin"));
        parts.Add(KeyName(virtualKey));
        return string.Join("+", parts);
    }

    private static string KeyName(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),  // '0'-'9'
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),  // 'A'-'Z'
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",        // F1-F24
        0x20 => TimeSnap.Loc.Get("HotkeyKeySpace"),
        0x09 => TimeSnap.Loc.Get("HotkeyKeyTab"),
        0x0D => TimeSnap.Loc.Get("HotkeyKeyEnter"),
        0x1B => TimeSnap.Loc.Get("HotkeyKeyEscape"),
        0x2D => TimeSnap.Loc.Get("HotkeyKeyInsert"),
        0x2E => TimeSnap.Loc.Get("HotkeyKeyDelete"),
        0x24 => TimeSnap.Loc.Get("HotkeyKeyHome"),
        0x23 => TimeSnap.Loc.Get("HotkeyKeyEnd"),
        0x21 => TimeSnap.Loc.Get("HotkeyKeyPageUp"),
        0x22 => TimeSnap.Loc.Get("HotkeyKeyPageDown"),
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        _ => $"VK_0x{vk:X2}"
    };
}
