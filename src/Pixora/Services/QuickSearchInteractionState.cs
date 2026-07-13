using System.Windows.Input;

namespace Pixora.Services;

public sealed class QuickSearchInteractionState
{
    public bool IsTextEntryActive { get; private set; }

    public void SetTextEntryActive(bool active)
    {
        IsTextEntryActive = active;
    }

    public bool ShouldTextBoxHandleKey(Key key, ModifierKeys modifiers)
    {
        if (!IsTextEntryActive)
        {
            return false;
        }

        return IsTextOrEditingKey(key, modifiers);
    }

    public bool ShouldSuppressUnboundKey(Key key, ModifierKeys modifiers)
    {
        return !IsTextEntryActive && IsTextOrEditingKey(key, modifiers);
    }

    private static bool IsTextOrEditingKey(Key key, ModifierKeys modifiers)
    {
        if (modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            return IsTextInputKey(key);
        }

        if (modifiers == ModifierKeys.Control)
        {
            return key is Key.A
                or Key.C
                or Key.V
                or Key.X
                or Key.Y
                or Key.Z
                or Key.Back
                or Key.Delete
                or Key.Insert
                or Key.Left
                or Key.Right
                or Key.Home
                or Key.End;
        }

        return false;
    }

    private static bool IsTextInputKey(Key key)
    {
        return (key >= Key.A && key <= Key.Z)
            || (key >= Key.D0 && key <= Key.D9)
            || (key >= Key.NumPad0 && key <= Key.NumPad9)
            || key is Key.Space
                or Key.Back
                or Key.Delete
                or Key.Insert
                or Key.Left
                or Key.Right
                or Key.Home
                or Key.End
                or Key.Tab
                or Key.Decimal
                or Key.Add
                or Key.Subtract
                or Key.Multiply
                or Key.Divide
                or Key.Oem1
                or Key.OemPlus
                or Key.OemComma
                or Key.OemMinus
                or Key.OemPeriod
                or Key.Oem2
                or Key.Oem3
                or Key.Oem4
                or Key.Oem5
                or Key.Oem6
                or Key.Oem7
                or Key.Oem8
                or Key.Oem102
                or Key.ImeProcessed
                or Key.DeadCharProcessed;
    }
}
