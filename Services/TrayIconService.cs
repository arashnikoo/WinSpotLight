using System.Drawing;
using System.Windows.Forms;

namespace Spotlight.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _openItem;

    public TrayIconService(Action onOpen, Action onSettings, Action onExit)
    {
        _openItem = new ToolStripMenuItem("Open Spotlight");
        _openItem.Click += (_, _) => onOpen();

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Spotlight Launcher",
            Visible = true,
            ContextMenuStrip = BuildMenu(_openItem, onSettings, onExit),
        };
        _icon.DoubleClick += (_, _) => onOpen();
    }

    private static ContextMenuStrip BuildMenu(ToolStripMenuItem openItem, Action onSettings, Action onExit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(openItem);

        var startupItem = new ToolStripMenuItem("Run at startup")
        {
            Checked = StartupManager.IsEnabled(),
        };
        startupItem.CheckedChanged += (_, _) => StartupManager.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => onSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => onExit();
        menu.Items.Add(exitItem);

        return menu;
    }

    public void SetHotkeyText(string display)
    {
        _icon.Text = $"Spotlight Launcher ({display})";
        _openItem.Text = $"Open Spotlight  ({display})";
    }

    public void Show() => _icon.Visible = true;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
