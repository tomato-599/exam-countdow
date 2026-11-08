using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using WinRT.Interop;

namespace ExamCountdown;

internal static class Ui
{
    public static bool Dark => Render.IsSystemDark();

    public static SolidColorBrush Brush(string hex, byte alpha = 255)
    {
        var color = CountdownWindow.ParseColor(hex);
        color.A = alpha;
        return new SolidColorBrush(color);
    }

    public static TextBlock Text(string text, double size = 14, bool bold = false,
        string? color = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        if (bold) block.FontWeight = FontWeights.SemiBold;
        if (color != null) block.Foreground = Brush(color);
        return block;
    }

    public static TextBlock Hint(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush(Dark ? "#9A9A9A" : "#6B6B6B"),
        };
        return block;
    }

    public static StackPanel Stack(double spacing = 10) => new()
    {
        Spacing = spacing,
        Orientation = Orientation.Vertical,
    };

    public static Border Card(UIElement child)
    {
        return new Border
        {
            Background = new SolidColorBrush(Dark
                ? Windows.UI.Color.FromArgb(255, 44, 44, 44)
                : Windows.UI.Color.FromArgb(255, 248, 248, 248)),
            BorderBrush = new SolidColorBrush(Dark
                ? Windows.UI.Color.FromArgb(255, 58, 58, 58)
                : Windows.UI.Color.FromArgb(255, 229, 229, 229)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Child = child,
        };
    }

    public static StackPanel CardWithTitle(string title, out StackPanel body)
    {
        var root = Stack(8);
        root.Children.Add(Text(title, 15, true));
        body = Stack(8);
        root.Children.Add(body);
        return root;
    }

    public static void AddRow(StackPanel panel, string label, FrameworkElement control,
        string? hint = null)
    {
        var stack = Stack(4);
        stack.Children.Add(Text(label, 13, true));
        stack.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(hint))
            stack.Children.Add(Hint(hint));
        panel.Children.Add(stack);
    }

    public static TextBox Input(string text = "", string? placeholder = null)
    {
        return new TextBox
        {
            Text = text,
            PlaceholderText = placeholder ?? "",
            MinWidth = 160,
        };
    }

    public static NumberBox Number(double value, double min, double max, double step = 1)
    {
        return new NumberBox
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            SmallChange = step,
            LargeChange = step * 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 160,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
    }

    public static ComboBox Combo(params (string Text, string Tag)[] items)
    {
        var combo = new ComboBox { MinWidth = 220 };
        foreach (var (text, tag) in items)
            combo.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    public static string SelectedTag(ComboBox combo, string fallback)
    {
        return combo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;
    }

    public static void SelectTag(ComboBox combo, string tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem { Tag: string value } && value == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    public static ToggleSwitch Toggle(bool value, string on = "开", string off = "关")
    {
        return new ToggleSwitch { IsOn = value, OnContent = on, OffContent = off };
    }

    public static CalendarDatePicker DatePicker(DateTimeOffset? date)
    {
        return new CalendarDatePicker
        {
            Date = date,
            PlaceholderText = "选择日期",
            MinWidth = 150,
        };
    }

    public static TimePicker TimePicker(TimeSpan? time)
    {
        return new TimePicker
        {
            SelectedTime = time,
            ClockIdentifier = "24HourClock",
            MinuteIncrement = 1,
            MinWidth = 110,
        };
    }

    public static int IntValue(NumberBox box, int fallback)
    {
        return double.IsNaN(box.Value)
            ? fallback
            : (int)Math.Round(box.Value);
    }

    public static double DoubleValue(NumberBox box, double fallback)
    {
        return double.IsNaN(box.Value) ? fallback : box.Value;
    }

    public static DateTimeOffset? DateValue(CalendarDatePicker picker)
    {
        return picker.Date;
    }

    public static void CenterWindow(Window window, int dipWidth, int dipHeight)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var scale = SystemIntegration.DpiScale(hwnd);
            var area = DisplayArea.GetFromWindowId(window.AppWindow.Id,
                DisplayAreaFallback.Primary).WorkArea;
            var width = Math.Min((int)Math.Round(dipWidth * scale), area.Width - 40);
            var height = Math.Min((int)Math.Round(dipHeight * scale), area.Height - 40);
            window.AppWindow.Resize(new SizeInt32(width, height));
            window.AppWindow.Move(new PointInt32(
                area.X + Math.Max(0, (area.Width - width) / 2),
                area.Y + Math.Max(0, (area.Height - height) / 2)));
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("窗口居中失败:\n" + ex);
        }
    }

    public static void SetWindowIcon(Window window)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
    }

    public static void MakeTopMost(Window window)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = true;
    }

    public static ScrollViewer Scroll(UIElement content)
    {
        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(24, 18, 24, 24),
        };
    }

    public static string Hex(Windows.UI.Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static ImageBrush? CreateImageBrush(AppConfig cfg)
    {
        var path = AppConfig.ResolveImagePath(cfg.BackgroundImage);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage { UriSource = new Uri(path) };
            return new ImageBrush
            {
                ImageSource = image,
                Stretch = cfg.BackgroundImageStretch switch
                {
                    "uniform" => Stretch.Uniform,
                    "fill" => Stretch.Fill,
                    "none" => Stretch.None,
                    _ => Stretch.UniformToFill,
                },
                AlignmentX = cfg.BackgroundImageAlignX switch
                {
                    "left" => AlignmentX.Left,
                    "right" => AlignmentX.Right,
                    _ => AlignmentX.Center,
                },
                AlignmentY = cfg.BackgroundImageAlignY switch
                {
                    "top" => AlignmentY.Top,
                    "bottom" => AlignmentY.Bottom,
                    _ => AlignmentY.Center,
                },
                Opacity = Math.Clamp(cfg.BackgroundImageOpacity, 0.0, 1.0),
            };
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("加载背景图片失败:\n" + ex);
            return null;
        }
    }
}
