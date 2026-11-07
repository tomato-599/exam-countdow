using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ExamCountdown;

public sealed partial class App : Application
{
    private AppConfig _cfg = AppConfig.Default();
    private CountdownWindow _window = null!;
    private HolidayStore _holidayStore = null!;
    private TrayIcon? _tray;
    private AboutWindow? _aboutWindow;
    private DispatcherQueue _dispatcher = null!;
    private bool _holidayBusy;
    private bool _holidayNotify;
    private bool _updateBusy;
    private bool _updateManual;
    private string _notifiedVersion = "";

    private const int CmdToggle = 1;
    private const int CmdRefresh = 2;
    private const int CmdNext = 3;
    private const int CmdSettings = 4;
    private const int CmdJson = 5;
    private const int CmdExport = 6;
    private const int CmdImport = 7;
    private const int CmdReload = 8;
    private const int CmdHolidayUpdate = 9;
    private const int CmdCheckUpdate = 10;
    private const int CmdAutostart = 11;
    private const int CmdAbout = 12;
    private const int CmdQuit = 13;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            ConfigStore.LogError("未处理异常:\n" + e.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (SystemIntegration.AlreadyRunning())
        {
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _cfg = ConfigStore.Load();
        if (!File.Exists(ConfigStore.ConfigFile)) ConfigStore.Save(_cfg);

        _holidayStore = new HolidayStore(_cfg);
        _holidayStore.LoadCache();

        _window = new CountdownWindow(_cfg);
        _dispatcher.TryEnqueue(InitAfterWindow);

        if (_cfg.Autostart &&
            SystemIntegration.AutostartCommand() != SystemIntegration.LaunchCommand())
            SystemIntegration.SetAutostart(true);
    }

    private void InitAfterWindow()
    {
        _tray = new TrayIcon(BuildTrayMenu, ToggleWindow, () => _ = OpenSettingsAsync(),
            OnTrayCommand);
        RefreshTrayToolTip();

        if (_cfg.HolidayAuto) MaybeRefreshHolidays();

        var hourly = _dispatcher.CreateTimer();
        hourly.Interval = TimeSpan.FromHours(1);
        hourly.Tick += (_, _) => MaybeRefreshHolidays();
        hourly.Start();

        var firstUpdate = _dispatcher.CreateTimer();
        firstUpdate.Interval = TimeSpan.FromSeconds(2);
        firstUpdate.IsRepeating = false;
        firstUpdate.Tick += (_, _) =>
        {
            firstUpdate.Stop();
            CheckUpdates();
        };
        firstUpdate.Start();

        var updateTimer = _dispatcher.CreateTimer();
        updateTimer.Interval = TimeSpan.FromHours(24);
        updateTimer.Tick += (_, _) => CheckUpdates();
        updateTimer.Start();

        var tooltipTimer = _dispatcher.CreateTimer();
        tooltipTimer.Interval = TimeSpan.FromSeconds(30);
        tooltipTimer.Tick += (_, _) => RefreshTrayToolTip();
        tooltipTimer.Start();
    }

    // ------------------------------------------------------------------ 托盘
    private List<TrayMenuEntry> BuildTrayMenu()
    {
        return new List<TrayMenuEntry>
        {
            TrayMenuEntry.Item(
                _window.IsUserHidden ? "显示悬浮窗" : "隐藏悬浮窗", CmdToggle),
            TrayMenuEntry.Item("立即刷新", CmdRefresh),
            TrayMenuEntry.Item("下一个倒计时", CmdNext, enabled: _window.ItemCount > 1),
            TrayMenuEntry.Line(),
            TrayMenuEntry.Item("设置…", CmdSettings),
            TrayMenuEntry.Item("编辑设置（JSON）…", CmdJson),
            TrayMenuEntry.Item("导出配置…", CmdExport),
            TrayMenuEntry.Item("导入配置…", CmdImport),
            TrayMenuEntry.Item("重新读取配置", CmdReload),
            TrayMenuEntry.Item($"节假日数据：{_holidayStore.StatusText()}",
                0, enabled: false),
            TrayMenuEntry.Item("立即更新节假日数据", CmdHolidayUpdate),
            TrayMenuEntry.Item("检查新版本", CmdCheckUpdate),
            TrayMenuEntry.Item("开机自动启动", CmdAutostart, check: _cfg.Autostart),
            TrayMenuEntry.Line(),
            TrayMenuEntry.Item("关于", CmdAbout),
            TrayMenuEntry.Line(),
            TrayMenuEntry.Item("退出", CmdQuit),
        };
    }

    private async void OnTrayCommand(int command)
    {
        switch (command)
        {
            case CmdToggle:
                ToggleWindow();
                break;
            case CmdRefresh:
                _window.RefreshText();
                RefreshTrayToolTip();
                break;
            case CmdNext:
                _window.NextItem();
                RefreshTrayToolTip();
                break;
            case CmdSettings:
                await OpenSettingsAsync();
                break;
            case CmdJson:
                await OpenJsonEditorAsync();
                break;
            case CmdExport:
                await ExportConfigAsync();
                break;
            case CmdImport:
                await ImportConfigAsync();
                break;
            case CmdReload:
                ApplyConfig(ConfigStore.Load());
                _tray?.ShowBalloon(AppInfo.Title, "已重新读取 config.json。");
                break;
            case CmdHolidayUpdate:
                MaybeRefreshHolidays(force: true, notify: true);
                break;
            case CmdCheckUpdate:
                CheckUpdates(manual: true);
                break;
            case CmdAutostart:
                await ToggleAutostartAsync();
                break;
            case CmdAbout:
                _aboutWindow = new AboutWindow();
                _aboutWindow.Closed += (_, _) => _aboutWindow = null;
                _aboutWindow.ShowWindow();
                break;
            case CmdQuit:
                Quit();
                break;
        }
    }

    private void ToggleWindow()
    {
        _window.SetUserHidden(!_window.IsUserHidden);
        RefreshTrayToolTip();
    }

    private void RefreshTrayToolTip()
    {
        try
        {
            var items = _cfg.Items();
            var parts = items.Select(item => Render.PlainText(_cfg,
                Render.CountDays(_cfg, item.Deadline), item));
            _tray?.SetToolTip($"{AppInfo.Title}：" + string.Join(" | ", parts));
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("刷新托盘提示失败:\n" + ex);
        }
    }

    // ------------------------------------------------------------------ 设置
    private async Task<bool> RequirePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(_cfg.PasswordHash)) return true;
        var dialog = new PasswordPromptWindow(_cfg.PasswordHash);
        return await dialog.ShowAsync();
    }

    private async Task OpenSettingsAsync()
    {
        if (!await RequirePasswordAsync()) return;
        var dialog = new SettingsWindow(_cfg, _holidayStore, () =>
        {
            _window.RefreshText();
            RefreshTrayToolTip();
        });
        var result = await dialog.ShowAsync();
        if (result != null) ApplyConfig(result);
    }

    private async Task OpenJsonEditorAsync()
    {
        if (!await RequirePasswordAsync()) return;
        var dialog = new JsonEditorWindow(_cfg);
        var result = await dialog.ShowAsync();
        if (result == null) return;
        ApplyConfig(result);
        _tray?.ShowBalloon(AppInfo.Title, "设置已保存到 config.json 并立即生效。");
    }

    private void ApplyConfig(AppConfig cfg)
    {
        var old = _cfg;
        if (cfg.Autostart != old.Autostart &&
            !SystemIntegration.SetAutostart(cfg.Autostart))
        {
            _tray?.ShowBalloon(AppInfo.Title, "设置开机自启动失败，请查看 error.log。",
                warning: true);
        }

        var sourceChanged = cfg.HolidayRepo != old.HolidayRepo ||
                            cfg.HolidayBranch != old.HolidayBranch ||
                            cfg.HolidayFile != old.HolidayFile;

        _cfg = cfg;
        ConfigStore.Save(cfg);
        _holidayStore.Config = cfg;
        _window.ApplyConfig(cfg);
        _window.SetUserHidden(false);
        RefreshTrayToolTip();

        if (sourceChanged && cfg.HolidayAuto)
            MaybeRefreshHolidays(force: true, notify: true);
    }

    private async Task ToggleAutostartAsync()
    {
        if (!await RequirePasswordAsync()) return;
        var enabled = !_cfg.Autostart;
        if (!SystemIntegration.SetAutostart(enabled))
        {
            _tray?.ShowBalloon(AppInfo.Title, "设置开机自启动失败，请查看 error.log。",
                warning: true);
            return;
        }
        _cfg.Autostart = enabled;
        ConfigStore.Save(_cfg);
    }

    private async Task ExportConfigAsync()
    {
        var path = await FilePickers.PickExportAsync(_window, "GaokaoCountdown-config");
        if (path == null) return;
        try
        {
            var export = _cfg.Clone();
            export.PasswordHash = "";
            File.WriteAllText(path, export.ToJson(), new UTF8Encoding(false));
            _tray?.ShowBalloon(AppInfo.Title, $"配置已导出到：\n{path}");
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("导出配置失败:\n" + ex);
            _tray?.ShowBalloon(AppInfo.Title, "导出配置失败，详情见 error.log。",
                warning: true);
        }
    }

    private async Task ImportConfigAsync()
    {
        if (!await RequirePasswordAsync()) return;
        var path = await FilePickers.PickImportAsync(_window);
        if (path == null) return;
        AppConfig imported;
        try
        {
            imported = AppConfig.FromJson(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("导入配置失败:\n" + ex);
            _tray?.ShowBalloon(AppInfo.Title, "导入失败：不是合法的 JSON 文件。",
                warning: true);
            return;
        }
        if (string.IsNullOrWhiteSpace(imported.PasswordHash))
            imported.PasswordHash = _cfg.PasswordHash;
        ApplyConfig(imported);
        _tray?.ShowBalloon(AppInfo.Title, "配置已导入并立即生效。");
    }

    // ------------------------------------------------------------------ 节假日
    private void MaybeRefreshHolidays(bool force = false, bool notify = false)
    {
        if (_holidayBusy) return;
        if (!force && !_holidayStore.IsStale()) return;
        var auto = force || _cfg.HolidayAuto;
        if (!auto) return;

        _holidayBusy = true;
        _holidayNotify = notify;
        _holidayStore.Config = _cfg.Clone();
        var store = _holidayStore;

        Task.Run(async () =>
        {
            var result = await store.FetchAsync(force: true);
            _dispatcher.TryEnqueue(() => OnHolidayFetched(result.Ok, result.Message));
        });
    }

    private void OnHolidayFetched(bool ok, string message)
    {
        _holidayBusy = false;
        _window.RefreshText();
        RefreshTrayToolTip();
        if (!_holidayNotify) return;
        _holidayNotify = false;
        _tray?.ShowBalloon(AppInfo.Title, message, warning: !ok);
    }

    // ------------------------------------------------------------------ 版本检查
    private void CheckUpdates(bool manual = false)
    {
        if (_updateBusy) return;
        _updateBusy = true;
        _updateManual = manual;
        var cfg = _cfg.Clone();

        Task.Run(async () =>
        {
            try
            {
                var latest = await UpdateService.FetchLatestAsync(cfg);
                _dispatcher.TryEnqueue(() => OnUpdateChecked(true, latest, ""));
            }
            catch (Exception ex)
            {
                _dispatcher.TryEnqueue(() => OnUpdateChecked(false, "", ex.Message));
            }
        });
    }

    private void OnUpdateChecked(bool ok, string latest, string error)
    {
        var manual = _updateManual;
        _updateManual = false;
        _updateBusy = false;

        if (!ok)
        {
            if (manual)
                _tray?.ShowBalloon(AppInfo.Title, $"检查新版本失败：{error}",
                    warning: true);
            return;
        }

        if (UpdateService.IsNewer(latest))
        {
            if (manual || latest != _notifiedVersion)
            {
                _notifiedVersion = latest;
                _tray?.ShowBalloon(AppInfo.Title,
                    $"发现新版本 {latest}（当前 {AppInfo.Version}），请前往 GitHub 仓库下载新版安装包。");
            }
        }
        else if (manual)
        {
            _tray?.ShowBalloon(AppInfo.Title, $"当前已是最新版本（{AppInfo.Version}）。");
        }
    }

    // ------------------------------------------------------------------ 退出
    private void Quit()
    {
        ConfigStore.Save(_cfg);
        _tray?.Dispose();
        _tray = null;
        _window.Close();
        Exit();
    }
}
