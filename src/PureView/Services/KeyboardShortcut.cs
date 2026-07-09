using System.Windows.Input;

namespace PureView.Services;

public sealed record KeyboardShortcut(Key Key, ModifierKeys Modifiers)
{
    public bool Matches(Key key, ModifierKeys modifiers)
    {
        return Key == NormalizeKey(key) && Modifiers == NormalizeModifiers(modifiers);
    }

    public string ToDisplayText()
    {
        var parts = new List<string>();
        if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            parts.Add("Shift");
        }

        if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            parts.Add("Alt");
        }

        if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
        {
            parts.Add("Win");
        }

        parts.Add(KeyToText(Key));
        return string.Join("+", parts);
    }

    public static KeyboardShortcut FromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };

        return new KeyboardShortcut(NormalizeKey(key), NormalizeModifiers(Keyboard.Modifiers));
    }

    public static bool IsValidInput(KeyboardShortcut shortcut)
    {
        return shortcut.Key is not Key.None
            and not Key.LeftCtrl
            and not Key.RightCtrl
            and not Key.LeftShift
            and not Key.RightShift
            and not Key.LeftAlt
            and not Key.RightAlt
            and not Key.LWin
            and not Key.RWin
            and not Key.System
            and not Key.ImeProcessed;
    }

    public static Key NormalizeKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => Key.LeftCtrl,
            Key.LeftShift or Key.RightShift => Key.LeftShift,
            Key.LeftAlt or Key.RightAlt => Key.LeftAlt,
            Key.LWin or Key.RWin => Key.LWin,
            _ => key,
        };
    }

    public static ModifierKeys NormalizeModifiers(ModifierKeys modifiers)
    {
        return modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows);
    }

    private static string KeyToText(Key key)
    {
        return key switch
        {
            Key.OemQuestion => "/",
            Key.Divide => "Num /",
            Key.Multiply => "Num *",
            Key.Add => "Num +",
            Key.Subtract => "Num -",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            _ => key.ToString(),
        };
    }
}
