using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Spotlight.Interop;
using Spotlight.Models;
using Spotlight.Search;

namespace Spotlight;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x534C;

    private ModifierKeys _hotkeyModifiers = ModifierKeys.Alt;
    private Key _hotkeyKey = Key.Space;

    private readonly SearchEngine _engine = new();
    private readonly DispatcherTimer _debounceTimer;
    private HwndSource? _hwndSource;
    private IntPtr _previousForeground;
    private bool _visible;
    private int _searchVersion;

    public MainWindow()
    {
        InitializeComponent();

        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(60),
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            RunSearch(SearchBox.Text);
        };

        _engine.AddProvider(new AppSearchProvider());
        _engine.AddProvider(new SystemCommandProvider());
        _engine.AddProvider(new CalculatorSearchProvider());
        _engine.AddProvider(new FileSearchProvider());

        SourceInitialized += OnSourceInitialized;
        Deactivated += (_, _) => HideWindow();
        Loaded += (_, _) => _ = _engine.InitializeAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        NativeMethods.SetToolWindow(hwnd);

        if (!RegisterHotkey())
        {
            System.Windows.MessageBox.Show(
                this,
                "Could not register the hotkey. Another program may be using it.",
                "Spotlight",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private bool RegisterHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        uint mods = (uint)_hotkeyModifiers | NativeMethods.MOD_NOREPEAT;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(_hotkeyKey);
        return NativeMethods.RegisterHotKey(hwnd, HotkeyId, mods, vk);
    }

    public ModifierKeys HotkeyModifiers => _hotkeyModifiers;

    public Key HotkeyKey => _hotkeyKey;

    public void SetHotkey(ModifierKeys modifiers, Key key)
    {
        _hotkeyModifiers = modifiers;
        _hotkeyKey = key;
    }

    public bool UpdateHotkey(ModifierKeys modifiers, Key key)
    {
        if (modifiers == _hotkeyModifiers && key == _hotkeyKey) return true;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(hwnd, HotkeyId);
        }

        var previousModifiers = _hotkeyModifiers;
        var previousKey = _hotkeyKey;
        _hotkeyModifiers = modifiers;
        _hotkeyKey = key;

        if (hwnd == IntPtr.Zero) return true;

        if (RegisterHotkey()) return true;

        _hotkeyModifiers = previousModifiers;
        _hotkeyKey = previousKey;
        RegisterHotkey();
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            ToggleWindow();
        }
        return IntPtr.Zero;
    }

    private void ToggleWindow()
    {
        if (_visible) HideWindow();
        else ShowAndFocus();
    }

    public void ShowAndFocus()
    {
        if (_visible) return;
        _visible = true;
        _previousForeground = NativeMethods.GetForegroundWindow();

        SearchBox.Clear();
        Show();
        PositionWindow();
        Activate();
        SearchBox.Focus();
        RunSearch("");
    }

    public void HideWindow()
    {
        if (!_visible) return;
        _visible = false;
        Hide();
        if (_previousForeground != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(_previousForeground);
        }
    }

    private void PositionWindow()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var work = screen.WorkingArea;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        Left = work.Left / scale + (work.Width / scale - Width) / 2;
        Top = work.Top / scale + work.Height / scale * 0.22;
    }

    private async void RunSearch(string query)
    {
        int version = ++_searchVersion;
        try
        {
            var results = await _engine.SearchAsync(query);
            if (version != _searchVersion || !_visible) return;

            ResultsList.ItemsSource = results;
            ResultsList.SelectedIndex = results.Count > 0 ? 0 : -1;
            EmptyHint.Visibility =
                results.Count == 0 && !string.IsNullOrWhiteSpace(query)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not SearchResult result) return;
        HideWindow();
        result.Execute();
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void SearchBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideWindow();
                e.Handled = true;
                break;

            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;

            case Key.Down when ResultsList.Items.Count > 0:
                ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                e.Handled = true;
                break;

            case Key.Up when ResultsList.Items.Count > 0:
                ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchResult)
        {
            ExecuteSelected();
        }
    }

    public void Cleanup()
    {
        _hwndSource?.RemoveHook(WndProc);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(hwnd, HotkeyId);
        }
    }
}
