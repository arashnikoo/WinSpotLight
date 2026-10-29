using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinSpotLight.Interop;
using WinSpotLight.Models;
using WinSpotLight.Search;

namespace WinSpotLight;

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
                "WinSpotLight",
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
        PositionWindow();
        CaptureBackground();
        Show();
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

        // Cover the whole monitor with the blur overlay.
        Left = work.Left / scale;
        Top = work.Top / scale;
        Width = work.Width / scale;
        Height = work.Height / scale;

        // Place the bar near the top, centered.
        RootBorder.Margin = new Thickness(0, work.Height / scale * 0.22, 0, 0);
    }

    private void CaptureBackground()
    {
        try
        {
            double scaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
            double scaleY = VisualTreeHelper.GetDpi(this).DpiScaleY;
            int x = (int)Math.Round(Left * scaleX);
            int y = (int)Math.Round(Top * scaleY);
            int w = (int)Math.Round(Width * scaleX);
            int h = (int)Math.Round(Height * scaleY);
            if (w <= 0 || h <= 0) return;

            using var capture = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(capture))
            {
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
            }

            // Blur the whole screen: downscale then upscale.
            int sw = Math.Max(1, w / 16);
            int sh = Math.Max(1, h / 16);
            using var small = new System.Drawing.Bitmap(sw, sh);
            using (var g = System.Drawing.Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(capture, 0, 0, sw, sh);
            }

            using var blurred = new System.Drawing.Bitmap(w, h);
            using (var g = System.Drawing.Graphics.FromImage(blurred))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(small, 0, 0, w, h);
            }

            BackgroundImage.Source = BitmapToImageSource(blurred);
        }
        catch
        {
            // Non-fatal: keep the previous background.
        }
    }

    private static BitmapSource BitmapToImageSource(System.Drawing.Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    private void Background_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HideWindow();
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
