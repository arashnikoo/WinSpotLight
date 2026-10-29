using System.Collections.Generic;
using System.Windows.Input;

namespace Spotlight.Services;

public static class HotkeyFormatter
{
    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(5);
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(FormatKey(key));
        return string.Join(" + ", parts);
    }

    private static string FormatKey(Key key)
    {
        string name = key.ToString();
        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1]))
        {
            return name[1..];
        }
        return name switch
        {
            "Space" => "Space",
            "Return" => "Enter",
            "Escape" => "Esc",
            "Back" => "Backspace",
            "Next" => "Page Down",
            "Prior" => "Page Up",
            "OemPlus" => "+",
            "OemMinus" => "-",
            "OemComma" => ",",
            "OemPeriod" => ".",
            "OemQuestion" => "/",
            "OemTilde" => "`",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "OemPipe" => "\\",
            "OemQuotes" => "'",
            "OemSemicolon" => ";",
            _ => name,
        };
    }
}
