using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ExamCountdown;

public sealed class PasswordPromptWindow : Window
{
    private const int MaxAttempts = 5;
    private readonly string _hash;
    private readonly PasswordBox _password;
    private readonly TextBlock _error;
    private readonly TaskCompletionSource<bool> _completion = new();
    private int _attempts;

    public PasswordPromptWindow(string passwordHash)
    {
        _hash = passwordHash;
        Title = $"{AppInfo.Title} - 需要密码";
        Ui.SetWindowIcon(this);
        Ui.MakeTopMost(this);

        var stack = Ui.Stack(12);
        stack.Children.Add(Ui.Text("此操作需要密码", 20, true));
        stack.Children.Add(Ui.Hint(
            "设置已启用密码保护，请输入密码后继续。\n" +
            "忘记密码可在 config.json 中删除 password_hash 后重新读取配置。"));

        _password = new PasswordBox { PlaceholderText = "请输入设置密码" };
        _password.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter) TryAccept();
        };
        stack.Children.Add(_password);

        _error = Ui.Text("", 12, false, "#C62828");
        stack.Children.Add(_error);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var cancel = new Button { Content = "取消" };
        cancel.Click += (_, _) => Finish(false);
        var ok = new Button
        {
            Content = "确定",
            Style = TryAccentStyle(),
        };
        ok.Click += (_, _) => TryAccept();
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        stack.Children.Add(buttons);

        Content = new Border { Padding = new Thickness(24), Child = stack };
        Ui.CenterWindow(this, 440, 300);
        Closed += (_, _) => _completion.TrySetResult(false);
    }

    public Task<bool> ShowAsync()
    {
        Activate();
        _password.Focus(FocusState.Programmatic);
        return _completion.Task;
    }

    private void TryAccept()
    {
        if (AppConfig.VerifyPassword(_password.Password, _hash))
        {
            Finish(true);
            return;
        }
        _attempts++;
        var remaining = MaxAttempts - _attempts;
        if (remaining <= 0)
        {
            Finish(false);
            return;
        }
        _password.Password = "";
        _error.Text = $"密码错误，还可尝试 {remaining} 次。";
        _password.Focus(FocusState.Programmatic);
    }

    private void Finish(bool result)
    {
        _completion.TrySetResult(result);
        Close();
    }

    internal static Style? TryAccentStyle()
    {
        return Application.Current.Resources.TryGetValue("AccentButtonStyle", out var found)
               && found is Style style ? style : null;
    }
}

public sealed class PasswordSetupWindow : Window
{
    private readonly string _hash;
    private readonly bool _hasPassword;
    private readonly PasswordBox? _current;
    private readonly PasswordBox _new;
    private readonly PasswordBox _confirm;
    private readonly TextBlock _error;
    private readonly TaskCompletionSource<string?> _completion = new();

    public PasswordSetupWindow(string passwordHash)
    {
        _hash = passwordHash;
        _hasPassword = !string.IsNullOrWhiteSpace(passwordHash);
        Title = $"{AppInfo.Title} - 设置密码";
        Ui.SetWindowIcon(this);
        Ui.MakeTopMost(this);

        var stack = Ui.Stack(12);
        stack.Children.Add(Ui.Text("设置密码", 20, true));
        var hintText = "开启后，打开设置、编辑 JSON、导入配置、修改开机自启都需要密码。" +
                       "密码只保存加盐哈希，不保存明文。\n新密码留空表示关闭密码保护。";
        if (_hasPassword) hintText = "先输入当前密码，再设置新密码。\n" + hintText;
        stack.Children.Add(Ui.Hint(hintText));

        if (_hasPassword)
        {
            _current = new PasswordBox { PlaceholderText = "当前密码" };
            stack.Children.Add(_current);
        }
        _new = new PasswordBox
        {
            PlaceholderText = "新密码，至少 4 位；留空关闭保护",
            MaxLength = 64,
        };
        stack.Children.Add(_new);
        _confirm = new PasswordBox
        {
            PlaceholderText = "再次输入新密码",
            MaxLength = 64,
        };
        _confirm.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter) TryAccept();
        };
        stack.Children.Add(_confirm);

        _error = Ui.Text("", 12, false, "#C62828");
        stack.Children.Add(_error);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var cancel = new Button { Content = "取消" };
        cancel.Click += (_, _) => Finish(null);
        var ok = new Button { Content = "确定", Style = PasswordPromptWindow.TryAccentStyle() };
        ok.Click += (_, _) => TryAccept();
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        stack.Children.Add(buttons);

        Content = new Border { Padding = new Thickness(24), Child = stack };
        Ui.CenterWindow(this, 470, 400);
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    public Task<string?> ShowAsync()
    {
        Activate();
        return _completion.Task;
    }

    private void TryAccept()
    {
        if (_current != null && !AppConfig.VerifyPassword(_current.Password, _hash))
        {
            _error.Text = "当前密码不正确。";
            _current.Password = "";
            _current.Focus(FocusState.Programmatic);
            return;
        }
        var password = _new.Password;
        if (password != _confirm.Password)
        {
            _error.Text = "两次输入的新密码不一致。";
            _confirm.Password = "";
            _confirm.Focus(FocusState.Programmatic);
            return;
        }
        if (password.Length > 0 && password.Length < 4)
        {
            _error.Text = "新密码至少 4 位。";
            _new.Focus(FocusState.Programmatic);
            return;
        }
        Finish(password.Length == 0 ? "" : AppConfig.HashPassword(password));
    }

    private void Finish(string? result)
    {
        _completion.TrySetResult(result);
        Close();
    }
}

public sealed class JsonEditorWindow : Window
{
    private readonly TextBox _editor;
    private readonly TextBlock _error;
    private readonly TaskCompletionSource<AppConfig?> _completion = new();

    public JsonEditorWindow(AppConfig cfg)
    {
        Title = $"{AppInfo.Title} - 编辑设置（JSON）";
        Ui.SetWindowIcon(this);
        Ui.MakeTopMost(this);

        _error = Ui.Text("", 12, false, "#C62828");
        _editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        var format = new Button { Content = "格式化" };
        format.Click += (_, _) => FormatJson();
        var defaults = new Button { Content = "填入默认值" };
        defaults.Click += (_, _) => _editor.Text = AppConfig.Default().ToJson();
        var cancel = new Button { Content = "取消" };
        cancel.Click += (_, _) => Finish(null);
        var save = new Button
        {
            Content = "保存并应用",
            Style = PasswordPromptWindow.TryAccentStyle(),
        };
        save.Click += (_, _) => Save();
        buttons.Children.Add(format);
        buttons.Children.Add(defaults);
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var bottom = new Grid { ColumnSpacing = 8 };
        bottom.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
        });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(_error);
        Grid.SetColumn(buttons, 1);
        bottom.Children.Add(buttons);

        var root = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 8,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(Ui.Hint(
            "直接编辑这段 JSON，保存后立即生效；每次启动也会重新读取它。" +
            "未知字段会被忽略，越界数值会自动修正，颜色必须写成 #RRGGBB。"));
        Grid.SetRow(_editor, 1);
        root.Children.Add(_editor);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);
        Content = root;
        _editor.Text = cfg.ToJson();

        Ui.CenterWindow(this, 880, 700);
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    public Task<AppConfig?> ShowAsync()
    {
        Activate();
        return _completion.Task;
    }

    private void FormatJson()
    {
        try
        {
            var cfg = AppConfig.FromJson(_editor.Text);
            _editor.Text = cfg.ToJson();
            _error.Text = "";
        }
        catch (Exception ex)
        {
            _error.Text = "JSON 格式不正确：" + ex.Message;
        }
    }

    private void Save()
    {
        try
        {
            var cfg = AppConfig.FromJson(_editor.Text);
            Finish(cfg);
        }
        catch (Exception ex)
        {
            _error.Text = "保存失败：不是合法的 JSON。" + ex.Message;
        }
    }

    private void Finish(AppConfig? cfg)
    {
        _completion.TrySetResult(cfg);
        Close();
    }
}

public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = $"{AppInfo.Title} - 关于";
        Ui.SetWindowIcon(this);
        Ui.MakeTopMost(this);

        var stack = Ui.Stack(12);
        stack.Children.Add(Ui.Text(AppInfo.Title, 24, true));
        stack.Children.Add(Ui.Text($"{AppInfo.Version} · WinUI 3 / Windows App SDK", 14));
        stack.Children.Add(Ui.Text(AppInfo.Desc, 14));
        stack.Children.Add(Ui.Hint("配置与旧版（1.x）完全兼容，可以直接沿用 %APPDATA%\\GaokaoCountdown\\config.json。"));

        var info = Ui.Stack(8);
        info.Children.Add(Ui.Text("配置文件", 14, true));
        var path = Ui.Text(ConfigStore.ConfigFile, 12);
        path.IsTextSelectionEnabled = true;
        info.Children.Add(path);
        info.Children.Add(Ui.Text("运行日志", 14, true));
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

        var close = new Button
        {
            Content = "确定",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        close.Click += (_, _) => Close();
        stack.Children.Add(close);

        Content = new Grid
        {
            Padding = new Thickness(24),
            Children = { Ui.Scroll(stack) },
        };
        Ui.CenterWindow(this, 760, 680);
    }

    public void ShowWindow() => Activate();

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
}

public static class FilePickers
{
    public static async Task<string?> PickImportAsync(Window owner)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".json");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("选择导入文件失败:\n" + ex);
            return null;
        }
    }

    public static async Task<string?> PickExportAsync(Window owner, string suggestedName)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedName,
            };
            picker.FileTypeChoices.Add("JSON 文件", new List<string> { ".json" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("选择导出位置失败:\n" + ex);
            return null;
        }
    }
}

public static class UpdateService
{
    public static (int Major, int Minor, int Patch) ParseVersion(string text)
    {
        var parts = new List<int>();
        foreach (var token in (text ?? "").Trim().TrimStart('v', 'V').Split('.'))
        {
            var digits = new string(token.Where(char.IsDigit).ToArray());
            parts.Add(digits.Length > 0 ? int.Parse(digits, CultureInfo.InvariantCulture) : 0);
            if (parts.Count == 3) break;
        }
        while (parts.Count < 3) parts.Add(0);
        return (parts[0], parts[1], parts[2]);
    }

    public static bool IsNewer(string latest, string current = AppInfo.Version)
        => ParseVersion(latest).CompareTo(ParseVersion(current)) > 0;

    public static string ExtractVersion(string text)
    {
        var raw = text.TrimStart('\uFEFF').Trim();
        if (raw.Length == 0) throw new FormatException("version.json 为空");
        try
        {
            var node = JsonNode.Parse(raw);
            if (node is JsonObject obj)
            {
                foreach (var key in new[] { "version", "latest", "tag_name", "tag" })
                {
                    var candidate = obj[key]?.ToString();
                    if (!string.IsNullOrWhiteSpace(candidate))
                        return candidate.Trim().TrimStart('v', 'V');
                }
                throw new FormatException("version.json 缺少 version 字段");
            }
            if (node is JsonValue valueNode && valueNode.TryGetValue<string>(out var value))
                return value.Trim().TrimStart('v', 'V');
        }
        catch (System.Text.Json.JsonException)
        {
            // 纯文本格式
        }
        return raw.Split('\n')[0].Trim().Trim('"', '\'', ',', '，').TrimStart('v', 'V');
    }

    public static async Task<string> FetchLatestAsync(AppConfig cfg)
    {
        var repo = string.IsNullOrWhiteSpace(cfg.HolidayRepo)
            ? "tomato-599/holidays_count" : cfg.HolidayRepo.Trim().Trim('/');
        var branch = string.IsNullOrWhiteSpace(cfg.HolidayBranch) ? "main" : cfg.HolidayBranch;
        var urls = new[]
        {
            $"https://raw.githubusercontent.com/{repo}/{branch}/version.json",
            $"https://fastly.jsdelivr.net/gh/{repo}@{branch}/version.json",
            $"https://cdn.jsdelivr.net/gh/{repo}@{branch}/version.json",
            $"https://testingcf.jsdelivr.net/gh/{repo}@{branch}/version.json",
            $"https://gcore.jsdelivr.net/gh/{repo}@{branch}/version.json",
        };

        var lastError = "";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", $"GaokaoCountdown/{AppInfo.Version}");
        foreach (var url in urls)
        {
            try
            {
                var text = await client.GetStringAsync(url).ConfigureAwait(false);
                return ExtractVersion(text);
            }
            catch (Exception ex)
            {
                lastError = $"{ex.GetType().Name}: {ex.Message}";
            }
        }
        throw new InvalidOperationException(lastError.Length > 0
            ? lastError : "没有可用的下载地址");
    }
}
