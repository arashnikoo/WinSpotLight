using System.Windows.Input;

namespace WinSpotLight.Services;

public sealed class AppSettings
{
    public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Alt;
    public Key HotkeyKey { get; set; } = Key.Space;
}
