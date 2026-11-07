using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace ExamCountdown;

/// <summary>无边框置顶悬浮窗：悬停隐藏、拖拽记忆、前台程序避让、多倒计时轮播。</summary>
public sealed class CountdownWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly IntPtr _hwnd;
    private readonly Border _card;
    private readonly RichTextBlock _rich;
    private readonly DispatcherQueueTimer _hoverTimer;
    private readonly DispatcherQueueTimer _dragTimer;
    private readonly DispatcherQueueTimer _foregroundTimer;
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly DispatcherQueueTimer _rotateTimer;

    private List<CountdownEvent> _items = new();
    private int _index;
    private bool _userHidden;
    private bool _hoverHidden;
    private bool _foregroundBlocked;
    private bool _dragging;
    private bool _dragJustEnded;
    private bool _dark;
    private double _minWidth;
    private double _minHeight;
    private int _dragOffsetX;
    private int _dragOffsetY;

    public CountdownWindow(AppConfig cfg)
    {
        _cfg = cfg;
        Title = AppInfo.Title;

        var presenter = OverlappedPresenter.Create();
        AppWindow.SetPresenter(presenter);
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;

        _rich = new RichTextBlock
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            TextWrapping = TextWrapping.NoWrap,
            TextAlignment = TextAlignment.Center,
            IsTextSelectionEnabled = false,
        };

        _card = new Border
        {
            CornerRadius = new CornerRadius(cfg.CornerRadius),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(18, 9, 18, 9),
            Child = _rich,
        };
        Content = _card;

        _hwnd = WindowNative.GetWindowHandle(this);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);

        _hoverTimer = DispatcherQueue.CreateTimer();
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(100);
        _hoverTimer.Tick += (_, _) => PollHover();
        _hoverTimer.Start();

        _dragTimer = DispatcherQueue.CreateTimer();
        _dragTimer.Interval = TimeSpan.FromMilliseconds(15);
        _dragTimer.Tick += (_, _) => DragTick();

        _foregroundTimer = DispatcherQueue.CreateTimer();
        _foregroundTimer.Interval = TimeSpan.FromMilliseconds(500);
        _foregroundTimer.Tick += (_, _) => CheckForeground();
        _foregroundTimer.Start();

        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(30);
        _refreshTimer.Tick += (_, _) => RefreshText();
        _refreshTimer.Start();

        _rotateTimer = DispatcherQueue.CreateTimer();
        _rotateTimer.Tick += (_, _) => NextItem();

        _card.PointerEntered += OnPointerEntered;
        _card.PointerPressed += OnPointerPressed;

        ApplyConfig(cfg);
        // 必须先正常激活一次，让 WinUI 的合成层连上窗口；之后再改成不抢焦点。
        Activate();
        SystemIntegration.MakeCountdownWindow(_hwnd);
        presenter.IsAlwaysOnTop = true;
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE
            | Native.SWP_SHOWWINDOW);
    }

    public CountdownEvent CurrentItem()
    {
        if (_items.Count == 0) _items = _cfg.Items();
        if (_index >= _items.Count) _index = 0;
        return _items[_index];
    }

    public int ItemCount => _items.Count;

    public bool IsUserHidden => _userHidden;

    public void SetUserHidden(bool hidden)
    {
        _userHidden = hidden;
        UpdateVisibility();
    }

    public void NextItem()
    {
        if (_items.Count > 1) _index = (_index + 1) % _items.Count;
        RefreshText();
    }

    public void ApplyConfig(AppConfig cfg)
    {
        _items = cfg.Items();
        if (_index >= _items.Count) _index = 0;
        _dark = Render.IsSystemDark();
        FitToWidest();
        RefreshText();
        SyncRotateTimer();
        PlaceTopRight();
    }

    private void SyncRotateTimer()
    {
        if (_items.Count > 1)
        {
            _rotateTimer.Interval = TimeSpan.FromMilliseconds(
                Math.Max(2000, _cfg.RotateSeconds * 1000));
            _rotateTimer.Start();
        }
        else
        {
            _rotateTimer.Stop();
        }
    }

    private void FitToWidest()
    {
        if (_items.Count == 0)
        {
            _minWidth = 0;
            _minHeight = 0;
            return;
        }

        _card.MinWidth = 0;
        _card.MinHeight = 0;
        var widest = 0.0;
        var tallest = 0.0;
        foreach (var item in _items)
        {
            SetRuns(Render.BuildRuns(_cfg,
                Render.CountDays(_cfg, item.Deadline), item, _dark));
            _card.Measure(new Windows.Foundation.Size(double.PositiveInfinity,
                double.PositiveInfinity));
            widest = Math.Max(widest, _card.DesiredSize.Width);
            tallest = Math.Max(tallest, _card.DesiredSize.Height);
        }
        _minWidth = widest;
        _minHeight = tallest;
    }

    public void RefreshText()
    {
        var item = CurrentItem();
        var days = Render.CountDays(_cfg, item.Deadline);
        SetRuns(Render.BuildRuns(_cfg, days, item, _dark));
        ApplyCardStyle();
        ResizeToContent();
        PlaceTopRight();
    }

    private void SetRuns(List<TextRun> runs)
    {
        _rich.Blocks.Clear();
        var paragraph = new Paragraph { TextAlignment = TextAlignment.Center };
        foreach (var run in runs)
        {
            paragraph.Inlines.Add(new Run
            {
                Text = run.Text,
                FontSize = run.FontSize,
                FontWeight = run.Bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(ParseColor(run.Color)),
            });
        }
        _rich.Blocks.Add(paragraph);
    }

    private void ApplyCardStyle()
    {
        _card.CornerRadius = new CornerRadius(Math.Clamp(_cfg.CornerRadius, 0, 60));
        var (_, bgHex) = Render.ResolveColors(_cfg, _dark);
        var background = ParseColor(bgHex);
        background.A = (byte)Math.Round(
            Math.Clamp(_cfg.Opacity, 0.15, 1.0) * 255);
        _card.Background = new SolidColorBrush(background);
    }

    private void ResizeToContent()
    {
        _card.MinWidth = _minWidth;
        _card.MinHeight = _minHeight;
        _card.Measure(new Windows.Foundation.Size(double.PositiveInfinity,
            double.PositiveInfinity));
        var desired = _card.DesiredSize;
        var scale = SystemIntegration.DpiScale(_hwnd);
        var width = Math.Max(1, (int)Math.Ceiling(desired.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(desired.Height * scale));
        AppWindow.Resize(new SizeInt32(width, height));
        SystemIntegration.RoundWindow(_hwnd, width, height,
            (int)Math.Round(Math.Clamp(_cfg.CornerRadius, 0, 60) * scale));
    }

    private void PlaceTopRight()
    {
        if (_dragging) return;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id,
            DisplayAreaFallback.Primary).WorkArea;
        var size = AppWindow.Size;
        var scale = SystemIntegration.DpiScale(_hwnd);
        var x = area.X + area.Width - size.Width - (int)Math.Round(_cfg.MarginX * scale);
        var y = area.Y + (int)Math.Round(_cfg.MarginY * scale);
        AppWindow.Move(new PointInt32(Math.Max(area.X, x), Math.Max(area.Y, y)));
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (Native.IsLeftButtonDown()) StartDrag();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(_card).Properties.IsLeftButtonPressed) StartDrag();
    }

    private void StartDrag()
    {
        if (_dragging) return;
        _dragging = true;
        var cursor = Native.CursorPosition();
        var position = AppWindow.Position;
        _dragOffsetX = cursor.X - position.X;
        _dragOffsetY = cursor.Y - position.Y;
        _dragTimer.Start();
    }

    private void DragTick()
    {
        if (!Native.IsLeftButtonDown())
        {
            _dragTimer.Stop();
            _dragging = false;
            _dragJustEnded = true;
            PersistPosition();
            return;
        }
        var cursor = Native.CursorPosition();
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST,
            cursor.X - _dragOffsetX, cursor.Y - _dragOffsetY, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private void PersistPosition()
    {
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        var center = new PointInt32(position.X + size.Width / 2, position.Y + size.Height / 2);
        var area = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Primary).WorkArea;
        var scale = Math.Max(0.1, SystemIntegration.DpiScale(_hwnd));
        var marginX = Math.Clamp((int)Math.Round(
            (area.X + area.Width - (position.X + size.Width)) / scale), 0, 4000);
        var marginY = Math.Clamp((int)Math.Round(
            (position.Y - area.Y) / scale), 0, 4000);
        if (marginX == _cfg.MarginX && marginY == _cfg.MarginY) return;
        _cfg.MarginX = marginX;
        _cfg.MarginY = marginY;
        ConfigStore.Save(_cfg);
    }

    private void PollHover()
    {
        if (_dragging || _userHidden || _foregroundBlocked) return;
        var cursor = Native.CursorPosition();
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        var inside = cursor.X >= position.X && cursor.X < position.X + size.Width
                     && cursor.Y >= position.Y && cursor.Y < position.Y + size.Height;

        if (!_hoverHidden)
        {
            if (inside && Native.IsLeftButtonDown())
            {
                StartDrag();
                return;
            }
            if (inside && _dragJustEnded) return;
            _dragJustEnded = false;
            if (inside)
            {
                _hoverHidden = true;
                UpdateVisibility();
            }
            return;
        }

        if (!inside)
        {
            _hoverHidden = false;
            UpdateVisibility();
        }
    }

    private void CheckForeground()
    {
        var keywords = _cfg.HideKeywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.ToLowerInvariant())
            .ToList();
        var blocked = false;
        if (keywords.Count > 0)
        {
            var haystack = $"{SystemIntegration.ForegroundProcessName()} " +
                           $"{SystemIntegration.ForegroundWindowTitle().ToLowerInvariant()}";
            blocked = keywords.Any(haystack.Contains);
        }

        if (blocked == _foregroundBlocked) return;
        _foregroundBlocked = blocked;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_userHidden || _hoverHidden || _foregroundBlocked)
        {
            Native.ShowWindow(_hwnd, Native.SW_HIDE);
        }
        else
        {
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE
                | Native.SWP_SHOWWINDOW);
        }
    }

    public static Windows.UI.Color ParseColor(string hex)
    {
        var text = (hex ?? "").Trim().TrimStart('#');
        try
        {
            if (text.Length == 3)
                text = string.Concat(text.Select(c => new string(c, 2)));
            if (text.Length == 8)
            {
                var a = byte.Parse(text[..2], NumberStyles.HexNumber);
                var r = byte.Parse(text[2..4], NumberStyles.HexNumber);
                var g = byte.Parse(text[4..6], NumberStyles.HexNumber);
                var b = byte.Parse(text[6..8], NumberStyles.HexNumber);
                return Windows.UI.Color.FromArgb(a, r, g, b);
            }
            if (text.Length == 6)
            {
                return Windows.UI.Color.FromArgb(255,
                    byte.Parse(text[..2], NumberStyles.HexNumber),
                    byte.Parse(text[2..4], NumberStyles.HexNumber),
                    byte.Parse(text[4..6], NumberStyles.HexNumber));
            }
        }
        catch
        {
            // 非法颜色回退到默认文字色
        }
        return Windows.UI.Color.FromArgb(255, 0x20, 0x21, 0x24);
    }
}
