using System.Windows;
using System.Windows.Input;
using WinSpotLight.Services;

namespace WinSpotLight;

public partial class SettingsWindow : Window
{
    private readonly Func<AppSettings, bool> _apply;
    private ModifierKeys _modifiers;
    private Key _key;

    public SettingsWindow(AppSettings settings, Func<AppSettings, bool> apply)
    {
        InitializeComponent();
        _modifiers = settings.HotkeyModifiers;
        _key = settings.HotkeyKey;
        _apply = apply;
        HotkeyBox.Text = HotkeyFormatter.Format(_modifiers, _key);
        Loaded += (_, _) => HotkeyBox.Focus();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        ModifierKeys mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None)
        {
            MessageText.Text = "Include at least one modifier (Ctrl, Alt, Shift, or Win).";
            e.Handled = true;
            return;
        }

        _modifiers = mods;
        _key = key;
        MessageText.Text = string.Empty;
        HotkeyBox.Text = HotkeyFormatter.Format(_modifiers, _key);
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            HotkeyModifiers = _modifiers,
            HotkeyKey = _key,
        };

        if (_apply(settings))
        {
            DialogResult = true;
        }
        else
        {
            MessageText.Text = "That combination is already in use by another program. Try a different one.";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
