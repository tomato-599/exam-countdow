using System.Diagnostics;
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

    public static async Task<string?> PickImageAsync(Window owner)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" })
                picker.FileTypeFilter.Add(ext);
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("选择背景图片失败:\n" + ex);
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

public sealed class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private readonly Action _quit;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _install;
    private readonly Button _release;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private bool _downloading;

    public UpdateWindow(UpdateInfo info, Action quit)
    {
        _info = info;
        _quit = quit;
        Title = $"{AppInfo.Title} - 发现新版本";
        Ui.SetWindowIcon(this);
        Ui.MakeTopMost(this);

        var stack = Ui.Stack(12);
        stack.Children.Add(Ui.Text($"发现新版本 {info.Version}", 22, true));
        stack.Children.Add(Ui.Text($"当前版本 {AppInfo.Version}", 13));
        stack.Children.Add(Ui.Hint(
            "更新包从 GitHub Releases 下载到临时目录，下载完成后会自动启动安装程序。"));

        if (!string.IsNullOrWhiteSpace(info.DownloadUrl))
        {
            var link = Ui.Text(info.DownloadUrl, 11, false, "#1565C0");
            link.IsTextSelectionEnabled = true;
            stack.Children.Add(link);
        }
        else
        {
            stack.Children.Add(Ui.Hint("version.json 未提供 download_url，请打开发布页手动下载。"));
        }

        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 4,
            Visibility = Visibility.Collapsed,
        };
        _status = Ui.Hint("");
        stack.Children.Add(_progress);
        stack.Children.Add(_status);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var later = new Button { Content = "稍后" };
        later.Click += (_, _) => Close();
        _release = new Button { Content = "打开发布页" };
        _release.Click += (_, _) => OpenReleasePage();
        _install = new Button
        {
            Content = "下载并安装",
            Style = PasswordPromptWindow.TryAccentStyle(),
        };
        _install.Click += async (_, _) => await DownloadAndInstallAsync();
        if (string.IsNullOrWhiteSpace(info.DownloadUrl)) _install.IsEnabled = false;
        buttons.Children.Add(_release);
        buttons.Children.Add(later);
        buttons.Children.Add(_install);
        stack.Children.Add(buttons);

        Content = new Border { Padding = new Thickness(24), Child = stack };
        Ui.CenterWindow(this, 580, 380);
        Closed += (_, _) => _http.Dispose();
    }

    public void ShowWindow() => Activate();

    private void OpenReleasePage()
    {
        var url = _info.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
            url = "https://github.com/tomato-599/exam-countdow/releases";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("打开发布页失败:\n" + ex);
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        if (_downloading || string.IsNullOrWhiteSpace(_info.DownloadUrl)) return;
        _downloading = true;
        _install.IsEnabled = false;
        _release.IsEnabled = false;
        _progress.Visibility = Visibility.Visible;
        _progress.IsIndeterminate = true;
        _progress.Value = 0;
        _status.Text = "正在连接 GitHub…";

        try
        {
            using var response = await _http.GetAsync(_info.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            var path = Path.Combine(Path.GetTempPath(),
                $"ExamCountdown-Setup-{_info.Version}.exe");
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(path))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    received += read;
                    if (total > 0)
                    {
                        _progress.IsIndeterminate = false;
                        _progress.Value = received * 100.0 / total;
                        _status.Text =
                            $"已下载 {received / 1048576.0:F1} / {total / 1048576.0:F1} MB";
                    }
                    else
                    {
                        _status.Text = $"已下载 {received / 1048576.0:F1} MB";
                    }
                }
            }

            _progress.IsIndeterminate = false;
            _progress.Value = 100;
            _status.Text = "下载完成，正在启动安装程序…";
            await Task.Delay(600);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            Close();
            _quit();
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("下载更新失败:\n" + ex);
            _status.Text = "下载失败：" + ex.Message;
            _progress.Visibility = Visibility.Collapsed;
            _install.IsEnabled = true;
            _release.IsEnabled = true;
            _downloading = false;
        }
    }
}
