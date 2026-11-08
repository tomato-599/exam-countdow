using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ExamCountdown;

/// <summary>设置窗口：倒计时 / 多倒计时 / 外观 / 行为 / 节假日 / 关于。</summary>
public sealed class SettingsWindow : Window
{
    private readonly AppConfig _orig;
    private readonly HolidayStore _holidayStore;
    private readonly Action _onHolidayUpdated;
    private readonly TaskCompletionSource<AppConfig?> _completion = new();
    private readonly List<EventRowUi> _rows = new();

    private CalendarDatePicker _deadlineDate = null!;
    private TimePicker _deadlineTime = null!;
    private NumberBox _duration = null!;
    private ComboBox _startMode = null!;
    private CalendarDatePicker _startDate = null!;
    private TextBox _templateBox = null!;
    private TextBox _zeroBox = null!;
    private TextBox _ongoingBox = null!;
    private TextBox _pastBox = null!;

    private NumberBox _rotate = null!;
    private TextBox _eventTemplateBox = null!;
    private StackPanel _eventsHost = null!;
    private TextBlock _eventsEmpty = null!;

    private ComboBox _colorMode = null!;
    private TextBox _textColorBox = null!;
    private TextBox _bgColorBox = null!;
    private TextBox _urgentColorBox = null!;
    private NumberBox _baseSize = null!;
    private NumberBox _urgentSize = null!;
    private NumberBox _zeroSize = null!;
    private NumberBox _urgentDays = null!;
    private Slider _opacity = null!;
    private TextBlock _opacityText = null!;
    private NumberBox _cornerRadius = null!;
    private TextBox _fontBox = null!;
    private ComboBox _fontPreset = null!;
    private TextBox _imagePathBox = null!;
    private Slider _imageOpacity = null!;
    private TextBlock _imageOpacityText = null!;
    private ComboBox _imageStretch = null!;
    private ComboBox _imageAlignX = null!;
    private ComboBox _imageAlignY = null!;
    private TextBlock _preview = null!;
    private Border _previewImageLayer = null!;
    private Border _previewCard = null!;

    private ToggleSwitch _autostart = null!;
    private ToggleSwitch _schoolDays = null!;
    private TextBox _keywords = null!;
    private NumberBox _marginX = null!;
    private NumberBox _marginY = null!;
    private TextBlock _passwordStatus = null!;
    private string _passwordHash = "";

    private TextBox _repo = null!;
    private TextBox _branch = null!;
    private TextBox _file = null!;
    private ToggleSwitch _holidayAuto = null!;
    private NumberBox _cacheHours = null!;
    private TextBlock _holidayStatus = null!;
    private TextBlock _holidayMessage = null!;
    private Button _holidayUpdate = null!;

    public SettingsWindow(AppConfig cfg, HolidayStore holidayStore,
        Action onHolidayUpdated)
    {
        _orig = cfg.Clone();
        _holidayStore = holidayStore;
        _onHolidayUpdated = onHolidayUpdated;
        Title = $"{AppInfo.Title} - 设置";
        Ui.SetWindowIcon(this);
        Ui.MakeTopMost(this);

        var nav = new NavigationView
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            IsSettingsVisible = false,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsPaneToggleButtonVisible = false,
            OpenPaneLength = 176,
            IsPaneOpen = true,
        };

        var pages = new Dictionary<string, UIElement>
        {
            ["countdown"] = BuildCountdownPage(),
            ["events"] = BuildEventsPage(),
            ["appearance"] = BuildAppearancePage(),
            ["behavior"] = BuildBehaviorPage(),
            ["holiday"] = BuildHolidayPage(),
            ["about"] = BuildAboutPage(),
        };

        foreach (var (tag, title) in new[]
                 {
                     ("countdown", "倒计时"), ("events", "多倒计时"),
                     ("appearance", "外观"), ("behavior", "行为"),
                     ("holiday", "节假日"), ("about", "关于"),
                 })
        {
            nav.MenuItems.Add(new NavigationViewItem
            {
                Content = title,
                Tag = tag,
            });
        }

        nav.SelectionChanged += (_, _) =>
        {
            if (nav.SelectedItem is NavigationViewItem { Tag: string tag } &&
                pages.TryGetValue(tag, out var page))
                nav.Content = page;
        };

        var bar = new Grid
        {
            Padding = new Thickness(16, 10, 16, 10),
            ColumnSpacing = 8,
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Ui.Dark
                ? Windows.UI.Color.FromArgb(255, 58, 58, 58)
                : Windows.UI.Color.FromArgb(255, 224, 224, 224)),
        };
        bar.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
        });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var reset = new Button { Content = "恢复默认" };
        reset.Click += (_, _) => RestoreDefaults();
        bar.Children.Add(reset);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        var cancel = new Button { Content = "取消" };
        cancel.Click += (_, _) => Cancel();
        var accept = new Button
        {
            Content = "确定",
            Style = Application.Current.Resources["AccentButtonStyle"] as Style,
        };
        accept.Click += (_, _) => Accept();
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        Grid.SetColumn(buttons, 1);
        bar.Children.Add(buttons);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(nav);
        Grid.SetRow(bar, 1);
        root.Children.Add(bar);
        Content = root;

        LoadValues(cfg);
        nav.SelectedItem = nav.MenuItems[0];
        Ui.CenterWindow(this, 980, 780);
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    public Task<AppConfig?> ShowAsync()
    {
        Activate();
        return _completion.Task;
    }

    private void Accept()
    {
        try
        {
            _completion.TrySetResult(BuildConfig());
            Close();
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("保存设置失败:\n" + ex);
        }
    }

    private void Cancel()
    {
        _completion.TrySetResult(null);
        Close();
    }

    // ------------------------------------------------------------------ 页面
    private UIElement BuildCountdownPage()
    {
        var stack = Ui.Stack(14);
        stack.Children.Add(Ui.Text("倒计时", 24, true));

        var body = Ui.Stack(12);
        _deadlineDate = Ui.DatePicker(null);
        _deadlineTime = Ui.TimePicker(TimeSpan.Zero);
        var deadlineRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        deadlineRow.Children.Add(_deadlineDate);
        deadlineRow.Children.Add(_deadlineTime);
        Ui.AddRow(body, "截止日期时间", deadlineRow, "开考时间，精确到分钟。");

        _duration = Ui.Number(1, 0, 365);
        Ui.AddRow(body, "考试持续天数（天）", _duration,
            "含截止当天；填 0 按 1 天处理，多天考试期间显示「考试进行中」。");
        stack.Children.Add(Ui.Card(body));

        var startBody = Ui.Stack(12);
        _startMode = Ui.Combo(
            ("学年 9 月 1 日（默认）", "school_year"),
            ("自定义日期", "custom"));
        _startDate = Ui.DatePicker(null);
        var startRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        startRow.Children.Add(_startMode);
        startRow.Children.Add(_startDate);
        Ui.AddRow(startBody, "进度起算日", startRow,
            "{percent} 按「起算日 → 截止日期」计算已过比例；选学年时按截止日期所在学年自动推算 9 月 1 日，" +
            "选自定义日期则固定从该日算起，不会每天顺延。");
        stack.Children.Add(Ui.Card(startBody));

        var textBody = Ui.Stack(12);
        _templateBox = Ui.Input("", "例如：距离高考还有 {time} 天");
        Ui.AddRow(textBody, "显示文字", _templateBox,
            "占位符：{time} / {days} = 剩余天数，{date} = 截止日期，{name} = 事件名称，{percent} = 进度百分比。");

        _zeroBox = Ui.Input("", "剩余天数 = 0 时显示");
        Ui.AddRow(textBody, "考试第一天显示", _zeroBox);

        _ongoingBox = Ui.Input("", "考试进行中 · 第{day}天");
        Ui.AddRow(textBody, "多天考试进行中显示", _ongoingBox,
            "额外支持 {day} = 第几天、{total} = 总天数、{percent} = 已考比例。");

        _pastBox = Ui.Input("", "考试全部结束后显示");
        Ui.AddRow(textBody, "考试结束后显示", _pastBox);
        stack.Children.Add(Ui.Card(textBody));

        return Ui.Scroll(stack);
    }

    private UIElement BuildEventsPage()
    {
        var stack = Ui.Stack(14);
        stack.Children.Add(Ui.Text("多倒计时", 24, true));

        var body = Ui.Stack(12);
        _rotate = Ui.Number(5, 2, 600);
        Ui.AddRow(body, "轮播间隔（秒）", _rotate, "只有两条以上倒计时才会轮播。");
        _eventTemplateBox = Ui.Input("", "距离{name}还有 {time} 天");
        Ui.AddRow(body, "默认模板", _eventTemplateBox,
            "条目自己没写模板时使用；支持 {name}、{time}、{date}、{percent}。");
        stack.Children.Add(Ui.Card(body));

        var listBody = Ui.Stack(10);
        listBody.Children.Add(Ui.Text("倒计时列表", 15, true));
        _eventsEmpty = Ui.Hint("还没有倒计时条目，点击下方「添加倒计时」新建一条。");
        _eventsHost = new StackPanel { Spacing = 8 };
        listBody.Children.Add(_eventsEmpty);
        listBody.Children.Add(_eventsHost);

        var add = new Button
        {
            Content = "+ 添加倒计时",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        add.Click += (_, _) => AddRow(null);
        listBody.Children.Add(add);
        listBody.Children.Add(Ui.Hint(
            "列表顺序即轮播顺序；名称和模板都为空的行会被忽略。持续天数留空按 1 天。"));
        stack.Children.Add(Ui.Card(listBody));

        return Ui.Scroll(stack);
    }

    private UIElement BuildAppearancePage()
    {
        var stack = Ui.Stack(14);
        stack.Children.Add(Ui.Text("外观", 24, true));

        _preview = new TextBlock
        {
            FontSize = 18,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(14, 10, 14, 10),
            Text = "距离高考还有 123 天",
        };
        _previewImageLayer = new Border
        {
            CornerRadius = new CornerRadius(10),
            IsHitTestVisible = false,
        };
        var previewGrid = new Grid();
        previewGrid.Children.Add(_previewImageLayer);
        previewGrid.Children.Add(_preview);
        _previewCard = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Child = previewGrid,
        };
        stack.Children.Add(_previewCard);
        stack.Children.Add(Ui.Hint("预览仅示意模板、字体、背景与配色，实际尺寸以悬浮窗为准。"));

        var fontBody = Ui.Stack(12);
        _fontBox = Ui.Input("", "例如 Microsoft YaHei UI、SimHei、KaiTi");
        Ui.AddRow(fontBody, "字体名称", _fontBox,
            "填写系统已安装的字体；找不到时会自动回退到默认字体。");
        _fontPreset = Ui.Combo(
            ("微软雅黑 UI（默认）", "Microsoft YaHei UI"),
            ("微软雅黑", "Microsoft YaHei"),
            ("等线", "DengXian"),
            ("黑体", "SimHei"),
            ("宋体", "SimSun"),
            ("楷体", "KaiTi"),
            ("思源黑体", "Source Han Sans SC"),
            ("Segoe UI", "Segoe UI"),
            ("Consolas", "Consolas"));
        Ui.AddRow(fontBody, "常用字体", _fontPreset, "选择后会填入上面的字体名称。");

        _imagePathBox = Ui.Input("", @"例如 D:\图片\背景.jpg");
        _imagePathBox.Width = 420;
        var browse = new Button { Content = "浏览…" };
        browse.Click += async (_, _) =>
        {
            var path = await FilePickers.PickImageAsync(this);
            if (path != null)
            {
                _imagePathBox.Text = path;
                UpdatePreview();
            }
        };
        var clear = new Button { Content = "清除" };
        clear.Click += (_, _) =>
        {
            _imagePathBox.Text = "";
            UpdatePreview();
        };
        var imageRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        imageRow.Children.Add(_imagePathBox);
        imageRow.Children.Add(browse);
        imageRow.Children.Add(clear);
        Ui.AddRow(fontBody, "背景图片", imageRow,
            "支持 png / jpg / jpeg / bmp / gif；可以写绝对路径，或相对配置目录的路径。");

        _imageOpacity = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 1,
            Width = 320,
        };
        _imageOpacityText = Ui.Text("100%", 13);
        var imageOpacityRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        imageOpacityRow.Children.Add(_imageOpacity);
        imageOpacityRow.Children.Add(_imageOpacityText);
        Ui.AddRow(fontBody, "图片不透明度", imageOpacityRow);
        stack.Children.Add(Ui.Card(fontBody));

        var imageBody = Ui.Stack(12);
        _imageStretch = Ui.Combo(
            ("覆盖填充（保持比例）", "uniform_to_fill"),
            ("适应（完整显示）", "uniform"),
            ("拉伸铺满", "fill"),
            ("原始大小", "none"));
        Ui.AddRow(imageBody, "图片缩放", _imageStretch);
        _imageAlignX = Ui.Combo(("左", "left"), ("中", "center"), ("右", "right"));
        Ui.AddRow(imageBody, "水平位置", _imageAlignX);
        _imageAlignY = Ui.Combo(("上", "top"), ("中", "center"), ("下", "bottom"));
        Ui.AddRow(imageBody, "垂直位置", _imageAlignY);
        stack.Children.Add(Ui.Card(imageBody));

        var body = Ui.Stack(12);
        _colorMode = Ui.Combo(
            ("跟随系统", "system"),
            ("自定义颜色", "custom"));
        Ui.AddRow(body, "颜色方案", _colorMode);

        _textColorBox = Ui.Input("", "#C62828");
        _textColorBox.Width = 160;
        Ui.AddRow(body, "文字颜色", _textColorBox, "十六进制，例如 #C62828。");

        _bgColorBox = Ui.Input("", "#FFFFFF");
        _bgColorBox.Width = 160;
        Ui.AddRow(body, "背景颜色", _bgColorBox);

        _urgentColorBox = Ui.Input("", "#E53935");
        _urgentColorBox.Width = 160;
        Ui.AddRow(body, "紧急数字颜色", _urgentColorBox);

        _opacity = new Slider
        {
            Minimum = 15,
            Maximum = 100,
            StepFrequency = 1,
            Width = 320,
        };
        _opacityText = Ui.Text("92%", 13);
        var opacityRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        opacityRow.Children.Add(_opacity);
        opacityRow.Children.Add(_opacityText);
        Ui.AddRow(body, "背景不透明度", opacityRow);
        stack.Children.Add(Ui.Card(body));

        var fonts = Ui.Stack(12);
        _baseSize = Ui.Number(20, 8, 120);
        Ui.AddRow(fonts, "常态文字字号（px）", _baseSize);
        _urgentSize = Ui.Number(34, 8, 200);
        Ui.AddRow(fonts, "紧急数字字号（px）", _urgentSize);
        _zeroSize = Ui.Number(30, 8, 200);
        Ui.AddRow(fonts, "归零 / 结束字号（px）", _zeroSize);
        _urgentDays = Ui.Number(10, 1, 365);
        Ui.AddRow(fonts, "紧急阈值（天）", _urgentDays,
            "剩余天数小于该值时数字放大并变红。");
        _cornerRadius = Ui.Number(14, 0, 60);
        Ui.AddRow(fonts, "圆角半径（px）", _cornerRadius,
            "悬浮窗卡片的圆角大小，0 为直角，最大 60。");
        stack.Children.Add(Ui.Card(fonts));

        return Ui.Scroll(stack);
    }

    private UIElement BuildBehaviorPage()
    {
        var stack = Ui.Stack(14);
        stack.Children.Add(Ui.Text("行为", 24, true));

        var body = Ui.Stack(12);
        _autostart = Ui.Toggle(_orig.Autostart);
        Ui.AddRow(body, "开机自动启动", _autostart);

        _schoolDays = Ui.Toggle(_orig.SchoolDays);
        Ui.AddRow(body, "只计上学日", _schoolDays,
            "扣除周末与法定节假日，调休补班的周末计入；数据来自内置表与节假日仓库。");

        _keywords = Ui.Input("", "easinote,easicamera,希沃白板");
        Ui.AddRow(body, "自动隐藏的关键词", _keywords,
            "前台进程名或窗口标题包含这些关键词时隐藏悬浮窗，英文逗号分隔。");

        _marginX = Ui.Number(28, 0, 4000);
        Ui.AddRow(body, "右边距（px）", _marginX, "也可以直接把悬浮窗拖到想要的位置。");
        _marginY = Ui.Number(24, 0, 4000);
        Ui.AddRow(body, "上边距（px）", _marginY);
        stack.Children.Add(Ui.Card(body));

        var passwordBody = Ui.Stack(8);
        passwordBody.Children.Add(Ui.Text("设置密码", 15, true));
        _passwordStatus = Ui.Hint("");
        passwordBody.Children.Add(_passwordStatus);
        var passwordButton = new Button { Content = "设置 / 修改密码…" };
        passwordButton.Click += async (_, _) => await EditPasswordAsync();
        passwordBody.Children.Add(passwordButton);
        passwordBody.Children.Add(Ui.Hint(
            "启用后，打开设置、编辑 JSON、导入配置、修改开机自启都需要密码。" +
            "密码只保存 PBKDF2 加盐哈希，不保存明文。忘记密码时可在 config.json 中删除 " +
            "password_hash 后重新读取配置。"));
        stack.Children.Add(Ui.Card(passwordBody));

        return Ui.Scroll(stack);
    }

    private UIElement BuildHolidayPage()
    {
        var stack = Ui.Stack(14);
        stack.Children.Add(Ui.Text("节假日数据", 24, true));

        var body = Ui.Stack(12);
        _repo = Ui.Input("", "tomato-599/holidays_count");
        Ui.AddRow(body, "GitHub 仓库", _repo, "格式 owner/repo。");
        _branch = Ui.Input("", "main");
        Ui.AddRow(body, "分支", _branch);
        _file = Ui.Input("", "holidays.json");
        Ui.AddRow(body, "JSON 文件路径", _file);
        _holidayAuto = Ui.Toggle(_orig.HolidayAuto);
        Ui.AddRow(body, "自动更新", _holidayAuto);
        _cacheHours = Ui.Number(6, 1, 168);
        Ui.AddRow(body, "检查间隔（小时）", _cacheHours);
        stack.Children.Add(Ui.Card(body));

        var statusBody = Ui.Stack(10);
        _holidayStatus = Ui.Text("", 14);
        statusBody.Children.Add(_holidayStatus);
        _holidayMessage = Ui.Hint("");
        statusBody.Children.Add(_holidayMessage);
        _holidayUpdate = new Button { Content = "立即更新" };
        _holidayUpdate.Click += async (_, _) => await UpdateHolidaysAsync();
        statusBody.Children.Add(_holidayUpdate);
        statusBody.Children.Add(Ui.Hint(
            "获取失败不影响使用：会继续使用本地缓存或内置数据。修改仓库 / 分支 / 文件后，" +
            "确定并应用设置即可重新获取。"));
        stack.Children.Add(Ui.Card(statusBody));

        return Ui.Scroll(stack);
    }

    private UIElement BuildAboutPage()
    {
        var stack = Ui.Stack(14);
        stack.Children.Add(Ui.Text("关于", 24, true));

        var info = Ui.Stack(8);
        info.Children.Add(Ui.Text($"{AppInfo.Title}  {AppInfo.Version}", 20, true));
        info.Children.Add(Ui.Text(AppInfo.Desc, 14));
        info.Children.Add(Ui.Hint("WinUI 3 / Windows App SDK 重构版，配置与旧版兼容。"));
        info.Children.Add(Ui.Text("配置文件", 13, true));
        var path = Ui.Text(ConfigStore.ConfigFile, 12);
        path.IsTextSelectionEnabled = true;
        info.Children.Add(path);
        info.Children.Add(Ui.Text("运行日志", 13, true));
        var log = Ui.Text(ConfigStore.LogFile, 12);
        log.IsTextSelectionEnabled = true;
        info.Children.Add(log);
        var open = new Button { Content = "打开配置目录" };
        open.Click += (_, _) => OpenConfigFolder();
        info.Children.Add(open);
        stack.Children.Add(Ui.Card(info));

        var docs = Ui.Stack(6);
        docs.Children.Add(Ui.Text("配置字段说明", 15, true));
        foreach (var (key, doc) in AppConfig.FieldDocs)
            docs.Children.Add(Ui.Text($"{key} · {doc}", 12));
        stack.Children.Add(Ui.Card(docs));

        return Ui.Scroll(stack);
    }

    private static void OpenConfigFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{ConfigStore.ConfigDir}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("打开配置目录失败:\n" + ex);
        }
    }

    // ------------------------------------------------------------------ 数据
    private void LoadValues(AppConfig cfg)
    {
        var deadline = Render.ParseDeadline(cfg.Deadline) ?? DateTime.Today.AddHours(9);
        _deadlineDate.Date = new DateTimeOffset(deadline.Date);
        _deadlineTime.SelectedTime = deadline.TimeOfDay;
        _duration.Value = Math.Max(1, cfg.DurationDays);

        Ui.SelectTag(_startMode, cfg.ProgressStartMode);
        var start = Render.ParseDate(cfg.ProgressStart) ?? DateOnly.FromDateTime(DateTime.Today);
        _startDate.Date = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue));
        _startDate.IsEnabled = cfg.ProgressStartMode == "custom";

        _templateBox.Text = cfg.Template;
        _zeroBox.Text = cfg.ZeroText;
        _ongoingBox.Text = cfg.OngoingText;
        _pastBox.Text = cfg.PastText;

        _rotate.Value = cfg.RotateSeconds;
        _eventTemplateBox.Text = cfg.EventTemplate;
        foreach (var row in _rows.ToList()) RemoveRow(row);
        foreach (var item in cfg.Events) AddRow(item);
        UpdateEventsEmpty();

        Ui.SelectTag(_colorMode, cfg.ColorMode);
        _textColorBox.Text = cfg.TextColor;
        _bgColorBox.Text = cfg.BgColor;
        _urgentColorBox.Text = cfg.UrgentColor;
        _baseSize.Value = cfg.BaseFontSize;
        _urgentSize.Value = cfg.UrgentFontSize;
        _zeroSize.Value = cfg.ZeroFontSize;
        _urgentDays.Value = cfg.UrgentDays;
        _opacity.Value = Math.Round(cfg.Opacity * 100);
        _opacityText.Text = $"{(int)Math.Round(cfg.Opacity * 100)}%";
        _cornerRadius.Value = cfg.CornerRadius;
        _fontBox.Text = cfg.FontFamily;
        _imagePathBox.Text = cfg.BackgroundImage;
        _imageOpacity.Value = Math.Round(cfg.BackgroundImageOpacity * 100);
        _imageOpacityText.Text = $"{(int)Math.Round(cfg.BackgroundImageOpacity * 100)}%";
        Ui.SelectTag(_imageStretch, cfg.BackgroundImageStretch);
        Ui.SelectTag(_imageAlignX, cfg.BackgroundImageAlignX);
        Ui.SelectTag(_imageAlignY, cfg.BackgroundImageAlignY);

        _autostart.IsOn = cfg.Autostart;
        _schoolDays.IsOn = cfg.SchoolDays;
        _keywords.Text = string.Join(",", cfg.HideKeywords);
        _marginX.Value = cfg.MarginX;
        _marginY.Value = cfg.MarginY;
        _passwordHash = cfg.PasswordHash;
        UpdatePasswordStatus();

        _repo.Text = cfg.HolidayRepo;
        _branch.Text = cfg.HolidayBranch;
        _file.Text = cfg.HolidayFile;
        _holidayAuto.IsOn = cfg.HolidayAuto;
        _cacheHours.Value = cfg.HolidayCacheHours;
        _holidayStatus.Text = $"当前数据：{_holidayStore.StatusText()}";
        _holidayMessage.Text = "";

        WireEvents();
        UpdateColorControls();
        UpdatePreview();
    }

    private void WireEvents()
    {
        _startMode.SelectionChanged += (_, _) =>
        {
            _startDate.IsEnabled = Ui.SelectedTag(_startMode, "school_year") == "custom";
            UpdatePreview();
        };
        _colorMode.SelectionChanged += (_, _) =>
        {
            UpdateColorControls();
            UpdatePreview();
        };

        _deadlineDate.DateChanged += (_, _) => UpdatePreview();
        _deadlineTime.SelectedTimeChanged += (_, _) => UpdatePreview();
        _duration.ValueChanged += (_, _) => UpdatePreview();
        _startDate.DateChanged += (_, _) => UpdatePreview();
        _templateBox.TextChanged += (_, _) => UpdatePreview();
        _zeroBox.TextChanged += (_, _) => UpdatePreview();
        _ongoingBox.TextChanged += (_, _) => UpdatePreview();
        _pastBox.TextChanged += (_, _) => UpdatePreview();
        _textColorBox.TextChanged += (_, _) => UpdatePreview();
        _bgColorBox.TextChanged += (_, _) => UpdatePreview();
        _urgentColorBox.TextChanged += (_, _) => UpdatePreview();
        _baseSize.ValueChanged += (_, _) => UpdatePreview();
        _urgentSize.ValueChanged += (_, _) => UpdatePreview();
        _zeroSize.ValueChanged += (_, _) => UpdatePreview();
        _urgentDays.ValueChanged += (_, _) => UpdatePreview();
        _cornerRadius.ValueChanged += (_, _) => UpdatePreview();
        _fontBox.TextChanged += (_, _) => UpdatePreview();
        _fontPreset.SelectionChanged += (_, _) =>
        {
            if (_fontPreset.SelectedItem is ComboBoxItem { Tag: string font })
                _fontBox.Text = font;
        };
        _imagePathBox.TextChanged += (_, _) => UpdatePreview();
        _imageOpacity.ValueChanged += (_, _) =>
        {
            _imageOpacityText.Text = $"{(int)Math.Round(_imageOpacity.Value)}%";
            UpdatePreview();
        };
        _imageStretch.SelectionChanged += (_, _) => UpdatePreview();
        _imageAlignX.SelectionChanged += (_, _) => UpdatePreview();
        _imageAlignY.SelectionChanged += (_, _) => UpdatePreview();
        _opacity.ValueChanged += (_, _) =>
        {
            _opacityText.Text = $"{(int)Math.Round(_opacity.Value)}%";
            UpdatePreview();
        };
    }

    private void UpdateColorControls()
    {
        var custom = Ui.SelectedTag(_colorMode, "system") == "custom";
        _textColorBox.IsEnabled = custom;
        _bgColorBox.IsEnabled = custom;
    }

    private void UpdatePasswordStatus()
    {
        _passwordStatus.Text = string.IsNullOrWhiteSpace(_passwordHash)
            ? "当前未启用密码保护。"
            : "已启用密码保护；修改或关闭时需要先输入当前密码。";
    }

    private async Task EditPasswordAsync()
    {
        var dialog = new PasswordSetupWindow(_passwordHash);
        var result = await dialog.ShowAsync();
        if (result == null) return;
        _passwordHash = result;
        UpdatePasswordStatus();
    }

    private async Task UpdateHolidaysAsync()
    {
        _holidayUpdate.IsEnabled = false;
        _holidayUpdate.Content = "更新中…";
        _holidayMessage.Text = "";
        try
        {
            _holidayStore.Config = BuildConfig();
            var (ok, message) = await _holidayStore.FetchAsync(force: true);
            _holidayStatus.Text = $"当前数据：{_holidayStore.StatusText()}";
            _holidayMessage.Text = message;
            if (ok) _onHolidayUpdated();
            _holidayMessage.Foreground = Ui.Brush(ok ? "#2E7D32" : "#C62828");
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("手动更新节假日失败:\n" + ex);
            _holidayMessage.Text = "更新失败：" + ex.Message;
        }
        finally
        {
            _holidayUpdate.Content = "立即更新";
            _holidayUpdate.IsEnabled = true;
        }
    }

    private void AddRow(CountdownEvent? item)
    {
        var row = new EventRowUi(item, RemoveRow, UpdatePreview);
        _rows.Add(row);
        _eventsHost.Children.Add(row.Root);
        UpdateEventsEmpty();
        UpdatePreview();
    }

    private void RemoveRow(EventRowUi row)
    {
        if (!_rows.Remove(row)) return;
        _eventsHost.Children.Remove(row.Root);
        UpdateEventsEmpty();
        UpdatePreview();
    }

    private void UpdateEventsEmpty()
    {
        _eventsEmpty.Visibility = _rows.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private AppConfig BuildConfig()
    {
        var cfg = _orig.Clone();
        var date = _deadlineDate.Date?.Date ?? DateTime.Today;
        var time = _deadlineTime.SelectedTime ?? TimeSpan.Zero;
        cfg.Deadline = date.Add(time).ToString("yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);
        cfg.DurationDays = Math.Max(1, Ui.IntValue(_duration, 1));
        cfg.ProgressStartMode = Ui.SelectedTag(_startMode, "school_year");
        var start = _startDate.Date?.Date ?? DateTime.Today;
        cfg.ProgressStart = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        cfg.Template = _templateBox.Text;
        cfg.ZeroText = _zeroBox.Text;
        cfg.OngoingText = _ongoingBox.Text;
        cfg.PastText = _pastBox.Text;

        cfg.RotateSeconds = Math.Max(2, Ui.IntValue(_rotate, 5));
        cfg.EventTemplate = _eventTemplateBox.Text;
        cfg.Events = _rows.Select(r => r.ToConfig())
            .Where(e => e != null)
            .Select(e => e!)
            .ToList();

        cfg.ColorMode = Ui.SelectedTag(_colorMode, "system");
        cfg.TextColor = _textColorBox.Text;
        cfg.BgColor = _bgColorBox.Text;
        cfg.UrgentColor = _urgentColorBox.Text;
        cfg.BaseFontSize = Math.Max(8, Ui.IntValue(_baseSize, 20));
        cfg.UrgentFontSize = Math.Max(8, Ui.IntValue(_urgentSize, 34));
        cfg.ZeroFontSize = Math.Max(8, Ui.IntValue(_zeroSize, 30));
        cfg.UrgentDays = Math.Max(1, Ui.IntValue(_urgentDays, 10));
        cfg.Opacity = Math.Clamp(_opacity.Value / 100.0, 0.15, 1.0);
        cfg.CornerRadius = Math.Clamp(Ui.IntValue(_cornerRadius, 14), 0, 60);
        cfg.FontFamily = _fontBox.Text;
        cfg.BackgroundImage = _imagePathBox.Text;
        cfg.BackgroundImageOpacity = Math.Clamp(_imageOpacity.Value / 100.0, 0.0, 1.0);
        cfg.BackgroundImageStretch = Ui.SelectedTag(_imageStretch, "uniform_to_fill");
        cfg.BackgroundImageAlignX = Ui.SelectedTag(_imageAlignX, "center");
        cfg.BackgroundImageAlignY = Ui.SelectedTag(_imageAlignY, "center");

        cfg.Autostart = _autostart.IsOn;
        cfg.SchoolDays = _schoolDays.IsOn;
        var keywords = _keywords.Text.Split(',')
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .ToList();
        cfg.HideKeywords = keywords.Count > 0 ? keywords : AppInfo.DefaultHideKeywords.ToList();
        cfg.MarginX = Math.Max(0, Ui.IntValue(_marginX, 28));
        cfg.MarginY = Math.Max(0, Ui.IntValue(_marginY, 24));
        cfg.PasswordHash = _passwordHash;

        cfg.HolidayRepo = _repo.Text;
        cfg.HolidayBranch = _branch.Text;
        cfg.HolidayFile = _file.Text;
        cfg.HolidayAuto = _holidayAuto.IsOn;
        cfg.HolidayCacheHours = Math.Clamp(Ui.IntValue(_cacheHours, 6), 1, 168);

        return AppConfig.FromJson(cfg.ToJson());
    }

    private void UpdatePreview()
    {
        if (_preview == null || _textColorBox == null) return;
        try
        {
            var cfg = BuildConfig();
            var days = Render.CountDays(cfg, cfg.Deadline);
            var text = Render.PlainText(cfg, days);
            _preview.Text = text.Length > 0 ? text : "（显示文字为空）";
            _preview.FontFamily = new FontFamily(
                string.IsNullOrWhiteSpace(cfg.FontFamily)
                    ? "Microsoft YaHei UI" : cfg.FontFamily);
            var (textColor, bgColor) = Render.ResolveColors(cfg, Ui.Dark);
            _preview.Foreground = Ui.Brush(textColor);
            _previewCard.Background = Ui.Brush(bgColor,
                (byte)Math.Round(cfg.Opacity * 255));
            _previewCard.BorderBrush = Ui.Brush(Ui.Dark ? "#565656" : "#D2D2D2");
            _previewCard.CornerRadius = new CornerRadius(cfg.CornerRadius);
            _previewImageLayer.CornerRadius = new CornerRadius(cfg.CornerRadius);
            _previewImageLayer.Background = Ui.CreateImageBrush(cfg);
        }
        catch
        {
            // 预览失败不影响编辑
        }
    }

    private void RestoreDefaults()
    {
        var cfg = AppConfig.Default();
        _deadlineDate.Date = new DateTimeOffset(DateTime.Today.AddHours(9));
        _deadlineTime.SelectedTime = TimeSpan.FromHours(9);
        _duration.Value = cfg.DurationDays;
        Ui.SelectTag(_startMode, cfg.ProgressStartMode);
        _startDate.Date = new DateTimeOffset(DateTime.Today);
        _startDate.IsEnabled = false;
        _templateBox.Text = cfg.Template;
        _zeroBox.Text = cfg.ZeroText;
        _ongoingBox.Text = cfg.OngoingText;
        _pastBox.Text = cfg.PastText;

        _rotate.Value = cfg.RotateSeconds;
        _eventTemplateBox.Text = cfg.EventTemplate;
        foreach (var row in _rows.ToList()) RemoveRow(row);

        Ui.SelectTag(_colorMode, cfg.ColorMode);
        _textColorBox.Text = cfg.TextColor;
        _bgColorBox.Text = cfg.BgColor;
        _urgentColorBox.Text = cfg.UrgentColor;
        _baseSize.Value = cfg.BaseFontSize;
        _urgentSize.Value = cfg.UrgentFontSize;
        _zeroSize.Value = cfg.ZeroFontSize;
        _urgentDays.Value = cfg.UrgentDays;
        _opacity.Value = Math.Round(cfg.Opacity * 100);
        _opacityText.Text = $"{(int)Math.Round(cfg.Opacity * 100)}%";
        _cornerRadius.Value = cfg.CornerRadius;
        _fontBox.Text = cfg.FontFamily;
        _imagePathBox.Text = cfg.BackgroundImage;
        _imageOpacity.Value = Math.Round(cfg.BackgroundImageOpacity * 100);
        _imageOpacityText.Text = $"{(int)Math.Round(cfg.BackgroundImageOpacity * 100)}%";
        Ui.SelectTag(_imageStretch, cfg.BackgroundImageStretch);
        Ui.SelectTag(_imageAlignX, cfg.BackgroundImageAlignX);
        Ui.SelectTag(_imageAlignY, cfg.BackgroundImageAlignY);

        _autostart.IsOn = cfg.Autostart;
        _schoolDays.IsOn = cfg.SchoolDays;
        _keywords.Text = string.Join(",", cfg.HideKeywords);
        _marginX.Value = cfg.MarginX;
        _marginY.Value = cfg.MarginY;
        _passwordHash = "";
        UpdatePasswordStatus();

        _repo.Text = cfg.HolidayRepo;
        _branch.Text = cfg.HolidayBranch;
        _file.Text = cfg.HolidayFile;
        _holidayAuto.IsOn = cfg.HolidayAuto;
        _cacheHours.Value = cfg.HolidayCacheHours;

        UpdateColorControls();
        UpdatePreview();
    }

    // ------------------------------------------------------------------ 条目行
    private sealed class EventRowUi
    {
        public Border Root { get; }
        private readonly TextBox _name;
        private readonly TextBox _template;
        private readonly CalendarDatePicker _date;
        private readonly TimePicker _time;
        private readonly NumberBox _duration;
        private readonly CheckBox _startCheck;
        private readonly CalendarDatePicker _startDate;

        public EventRowUi(CountdownEvent? item, Action<EventRowUi> remove,
            Action changed)
        {
            var deadline = Render.ParseDeadline(item?.Deadline)
                           ?? DateTime.Today.AddDays(1).AddHours(9);

            _name = Ui.Input(item?.Name ?? "", "名称，如 一模");
            _date = Ui.DatePicker(new DateTimeOffset(deadline.Date));
            _date.Width = 170;
            _time = Ui.TimePicker(deadline.TimeOfDay);
            _time.Width = 110;
            _duration = Ui.Number(item?.DurationDays ?? 1, 0, 365);
            _duration.Width = 88;

            var delete = new Button
            {
                Content = "删除",
                Padding = new Thickness(8, 4, 8, 4),
            };
            delete.Click += (_, _) => remove(this);

            var line1 = new Grid { ColumnSpacing = 8 };
            line1.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
            });
            line1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line1.Children.Add(_name);
            Grid.SetColumn(_date, 1);
            line1.Children.Add(_date);
            Grid.SetColumn(_time, 2);
            line1.Children.Add(_time);
            Grid.SetColumn(_duration, 3);
            line1.Children.Add(_duration);
            Grid.SetColumn(delete, 4);
            line1.Children.Add(delete);

            _template = Ui.Input(item?.Template ?? "", "可选模板，留空用默认模板");
            _startCheck = new CheckBox
            {
                Content = "自定义起算日",
                IsChecked = !string.IsNullOrWhiteSpace(item?.ProgressStart),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var start = Render.ParseDate(item?.ProgressStart)
                        ?? DateOnly.FromDateTime(DateTime.Today);
            _startDate = Ui.DatePicker(
                new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue)));
            _startDate.IsEnabled = _startCheck.IsChecked == true;
            _startDate.MinWidth = 140;

            var line2 = new Grid { ColumnSpacing = 8 };
            line2.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
            });
            line2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line2.Children.Add(_template);
            Grid.SetColumn(_startCheck, 1);
            line2.Children.Add(_startCheck);
            Grid.SetColumn(_startDate, 2);
            line2.Children.Add(_startDate);

            var body = Ui.Stack(8);
            body.Children.Add(line1);
            body.Children.Add(line2);
            Root = new Border
            {
                Background = new SolidColorBrush(Ui.Dark
                    ? Windows.UI.Color.FromArgb(255, 54, 54, 54)
                    : Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Ui.Dark
                    ? Windows.UI.Color.FromArgb(255, 70, 70, 70)
                    : Windows.UI.Color.FromArgb(255, 230, 230, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Child = body,
            };

            _name.TextChanged += (_, _) => changed();
            _template.TextChanged += (_, _) => changed();
            _date.DateChanged += (_, _) => changed();
            _time.SelectedTimeChanged += (_, _) => changed();
            _duration.ValueChanged += (_, _) => changed();
            _startCheck.Checked += (_, _) =>
            {
                _startDate.IsEnabled = true;
                changed();
            };
            _startCheck.Unchecked += (_, _) =>
            {
                _startDate.IsEnabled = false;
                changed();
            };
            _startDate.DateChanged += (_, _) => changed();
        }

        public CountdownEvent? ToConfig()
        {
            var name = _name.Text.Trim();
            var template = _template.Text.Trim();
            if (name.Length == 0 && template.Length == 0) return null;
            var date = _date.Date?.Date ?? DateTime.Today;
            var time = _time.SelectedTime ?? TimeSpan.Zero;
            var start = _startDate.Date?.Date ?? DateTime.Today;
            return new CountdownEvent
            {
                Name = name,
                Deadline = date.Add(time).ToString("yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture),
                Template = template,
                DurationDays = Math.Max(1, Ui.IntValue(_duration, 1)),
                ProgressStart = _startCheck.IsChecked == true
                    ? start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "",
            };
        }
    }
}
