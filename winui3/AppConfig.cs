using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ExamCountdown;

public static class AppInfo
{
    public const string Id = "GaokaoCountdown";
    public const string Title = "高考倒计时";
    public const string Version = "3.1.0";
    public const string Desc = "置顶悬浮的考试倒计时 · 希沃前台自动隐藏 · 托盘常驻";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static string[] DefaultHideKeywords =>
        new[] { "easinote", "easicamera", "希沃白板", "希沃视频展台" };

    public static string DefaultDeadline()
    {
        var today = DateTime.Today;
        var year = today.Year;
        if (new DateTime(year, 6, 7) < today) year++;
        return $"{year}-06-07 09:00:00";
    }
}

public sealed class CountdownEvent
{
    public string Name { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string Template { get; set; } = "";
    public int DurationDays { get; set; } = 1;
    public string ProgressStart { get; set; } = "";

    public CountdownEvent Clone() => new()
    {
        Name = Name,
        Deadline = Deadline,
        Template = Template,
        DurationDays = DurationDays,
        ProgressStart = ProgressStart,
    };
}

public sealed class AppConfig
{
    private static readonly Regex ColorPattern = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled);

    public string Deadline { get; set; } = AppInfo.DefaultDeadline();
    public int DurationDays { get; set; } = 1;
    public string ProgressStartMode { get; set; } = "school_year";
    public string ProgressStart { get; set; } = "";
    public string Template { get; set; } = "距离{name}还有 {time} 天";
    public string ZeroText { get; set; } = "旗开得胜";
    public string OngoingText { get; set; } = "考试进行中 · 第{day}天";
    public string PastText { get; set; } = "考试已结束";
    public int ZeroFontSize { get; set; } = 30;
    public int BaseFontSize { get; set; } = 20;
    public int UrgentDays { get; set; } = 10;
    public int UrgentFontSize { get; set; } = 34;
    public string UrgentColor { get; set; } = "#E53935";
    public string ColorMode { get; set; } = "system";
    public string TextColor { get; set; } = "#C62828";
    public string BgColor { get; set; } = "#FFFFFF";
    public double Opacity { get; set; } = 0.92;
    public int CornerRadius { get; set; } = 14;
    public bool Autostart { get; set; } = true;
    public string PasswordHash { get; set; } = "";
    public int MarginX { get; set; } = 28;
    public int MarginY { get; set; } = 24;
    public List<string> HideKeywords { get; set; } = AppInfo.DefaultHideKeywords.ToList();
    public bool SchoolDays { get; set; }
    public string HolidayRepo { get; set; } = "tomato-599/holidays_count";
    public string HolidayBranch { get; set; } = "main";
    public string HolidayFile { get; set; } = "holidays.json";
    public bool HolidayAuto { get; set; } = true;
    public int HolidayCacheHours { get; set; } = 6;
    public List<CountdownEvent> Events { get; set; } = new();
    public int RotateSeconds { get; set; } = 5;
    public string EventTemplate { get; set; } = "距离{name}还有 {time} 天";

    public AppConfig Clone() => FromJson(ToJson());

    public List<CountdownEvent> Items()
    {
        if (Events.Count > 0) return Events.Select(e => e.Clone()).ToList();
        return new List<CountdownEvent>
        {
            new()
            {
                Name = "",
                Deadline = Deadline,
                Template = "",
                DurationDays = Math.Clamp(DurationDays, 1, 365),
                ProgressStart = "",
            },
        };
    }

    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return JsonSerializer.Serialize(ToDictionary(), options);
    }

    public Dictionary<string, object?> ToDictionary() => new()
    {
        ["deadline"] = Deadline,
        ["duration_days"] = DurationDays,
        ["progress_start_mode"] = ProgressStartMode,
        ["progress_start"] = ProgressStart,
        ["template"] = Template,
        ["zero_text"] = ZeroText,
        ["ongoing_text"] = OngoingText,
        ["past_text"] = PastText,
        ["zero_font_size"] = ZeroFontSize,
        ["base_font_size"] = BaseFontSize,
        ["urgent_days"] = UrgentDays,
        ["urgent_font_size"] = UrgentFontSize,
        ["urgent_color"] = UrgentColor,
        ["color_mode"] = ColorMode,
        ["text_color"] = TextColor,
        ["bg_color"] = BgColor,
        ["opacity"] = Opacity,
        ["corner_radius"] = CornerRadius,
        ["autostart"] = Autostart,
        ["password_hash"] = PasswordHash,
        ["margin_x"] = MarginX,
        ["margin_y"] = MarginY,
        ["hide_keywords"] = HideKeywords,
        ["school_days"] = SchoolDays,
        ["holiday_repo"] = HolidayRepo,
        ["holiday_branch"] = HolidayBranch,
        ["holiday_file"] = HolidayFile,
        ["holiday_auto"] = HolidayAuto,
        ["holiday_cache_hours"] = HolidayCacheHours,
        ["events"] = Events.Select(e => new Dictionary<string, object?>
        {
            ["name"] = e.Name,
            ["deadline"] = e.Deadline,
            ["template"] = e.Template,
            ["duration_days"] = e.DurationDays,
            ["progress_start"] = e.ProgressStart,
        }).ToList(),
        ["rotate_seconds"] = RotateSeconds,
        ["event_template"] = EventTemplate,
    };

    public static AppConfig Default() => new();

    public static readonly (string Key, string Doc)[] FieldDocs =
    {
        ("deadline", "截止日期时间（即开考时间），ISO 格式 YYYY-MM-DD HH:MM:SS"),
        ("duration_days", "考试持续天数（含截止当天），1 ~ 365；留空按 1 天"),
        ("progress_start_mode", "进度百分比起算方式：school_year = 学年 9 月 1 日，custom = 自定义开始日"),
        ("progress_start", "自定义进度起算日（YYYY-MM-DD），progress_start_mode 为 custom 时生效"),
        ("template", "显示文字模板，{time} 或 {days} = 剩余天数，{date} = 截止日期，{percent} = 进度百分比"),
        ("zero_text", "剩余天数为 0（考试第一天）时显示的文字"),
        ("ongoing_text", "多天考试进行中显示的文字，支持 {name}/{day}/{total}/{percent}/{date}"),
        ("past_text", "考试全部结束后显示的文字"),
        ("base_font_size", "常态文字字号（px）"),
        ("urgent_font_size", "紧急状态下数字字号（px）"),
        ("zero_font_size", "归零 / 结束时文字字号（px）"),
        ("urgent_days", "紧急阈值：剩余天数小于该值时数字放大变红"),
        ("urgent_color", "紧急状态数字颜色（十六进制）"),
        ("color_mode", "颜色方案：system = 跟随系统，custom = 自定义"),
        ("text_color", "自定义模式下的文字颜色"),
        ("bg_color", "自定义模式下的背景颜色"),
        ("opacity", "背景不透明度，0.15 ~ 1.0"),
        ("corner_radius", "悬浮窗圆角半径（px），0 ~ 60；0 为直角"),
        ("autostart", "是否开机自动启动（true / false）"),
        ("password_hash", "设置密码的 PBKDF2 哈希；留空表示不启用密码保护"),
        ("margin_x", "悬浮窗距离屏幕右边距（px）"),
        ("margin_y", "悬浮窗距离屏幕上边距（px）"),
        ("hide_keywords", "前台程序命中这些关键词（进程名或窗口标题）时自动隐藏"),
        ("school_days", "为 true 时只计上学日：扣除周末和法定节假日，调休补班的周末计入"),
        ("holiday_repo", "节假日数据仓库（GitHub owner/repo）"),
        ("holiday_branch", "节假日仓库分支"),
        ("holiday_file", "仓库里的 JSON 文件路径"),
        ("holiday_auto", "是否自动定时从 GitHub 获取节假日数据"),
        ("holiday_cache_hours", "自动更新检查间隔（小时），1 ~ 168"),
        ("events", "多倒计时列表，每项 {name, deadline, template, duration_days, progress_start}"),
        ("rotate_seconds", "多倒计时轮播间隔（秒），2 ~ 600"),
        ("event_template", "多倒计时条目没写自己模板时使用的文字模板（支持 {name}）"),
    };

    public static AppConfig FromJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Default();
        var node = JsonNode.Parse(text);
        return Sanitize(node);
    }

    public static AppConfig Sanitize(JsonNode? node)
    {
        var cfg = new AppConfig();
        if (node is not JsonObject obj) return cfg;

        cfg.Deadline = Str(obj, "deadline", cfg.Deadline);
        if (string.IsNullOrWhiteSpace(cfg.Deadline)) cfg.Deadline = AppInfo.DefaultDeadline();
        cfg.DurationDays = ClampInt(obj, "duration_days", 1, 365, 1);
        cfg.ProgressStartMode = Str(obj, "progress_start_mode", "school_year") == "custom"
            ? "custom" : "school_year";
        cfg.ProgressStart = DateOrEmpty(Str(obj, "progress_start", ""));
        if (cfg.ProgressStartMode == "custom" && cfg.ProgressStart.Length == 0)
            cfg.ProgressStart = DateTime.Today.ToString("yyyy-MM-dd");
        cfg.Template = Str(obj, "template", cfg.Template);
        if (cfg.Template.Length == 0) cfg.Template = "{time}";
        cfg.ZeroText = Str(obj, "zero_text", cfg.ZeroText);
        cfg.OngoingText = Str(obj, "ongoing_text", cfg.OngoingText);
        cfg.PastText = Str(obj, "past_text", cfg.PastText);
        cfg.BaseFontSize = ClampInt(obj, "base_font_size", 8, 120, 20);
        cfg.UrgentFontSize = ClampInt(obj, "urgent_font_size", 8, 200, 34);
        cfg.ZeroFontSize = ClampInt(obj, "zero_font_size", 8, 200, 30);
        cfg.UrgentDays = ClampInt(obj, "urgent_days", 1, 365, 10);
        cfg.Opacity = ClampDouble(obj, "opacity", 0.15, 1.0, 0.92);
        cfg.CornerRadius = ClampInt(obj, "corner_radius", 0, 60, 14);
        cfg.MarginX = ClampInt(obj, "margin_x", 0, 4000, 28);
        cfg.MarginY = ClampInt(obj, "margin_y", 0, 4000, 24);
        cfg.ColorMode = Str(obj, "color_mode", "system") == "custom" ? "custom" : "system";
        cfg.TextColor = ColorOr(obj, "text_color", "#C62828");
        cfg.BgColor = ColorOr(obj, "bg_color", "#FFFFFF");
        cfg.UrgentColor = ColorOr(obj, "urgent_color", "#E53935");
        cfg.Autostart = Bool(obj, "autostart", true);
        cfg.PasswordHash = PasswordHashOrEmpty(Str(obj, "password_hash", ""));
        cfg.SchoolDays = Bool(obj, "school_days", false);

        var keywords = new List<string>();
        if (obj["hide_keywords"] is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var text = item?.ToString()?.Trim() ?? "";
                if (text.Length > 0) keywords.Add(text);
            }
        }
        else
        {
            var raw = Str(obj, "hide_keywords", "");
            keywords.AddRange(raw.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0));
        }
        keywords = keywords
            .Select(k => k.ToLowerInvariant() == "easynote" ? "easinote" : k)
            .Distinct()
            .ToList();
        var broad = new[] { "seewo", "希沃" };
        if (keywords.Any(k => broad.Contains(k.ToLowerInvariant())))
        {
            keywords = keywords.Where(k => !broad.Contains(k.ToLowerInvariant())).ToList();
            foreach (var k in AppInfo.DefaultHideKeywords)
                if (!keywords.Contains(k)) keywords.Add(k);
        }
        cfg.HideKeywords = keywords.Count > 0
            ? keywords
            : AppInfo.DefaultHideKeywords.ToList();

        cfg.HolidayRepo = RepoOr(obj, "holiday_repo", "tomato-599/holidays_count");
        cfg.HolidayBranch = BranchOr(obj, "holiday_branch", "main");
        cfg.HolidayFile = PathOr(obj, "holiday_file", "holidays.json");
        cfg.HolidayAuto = Bool(obj, "holiday_auto", true);
        cfg.HolidayCacheHours = ClampInt(obj, "holiday_cache_hours", 1, 168, 6);

        cfg.Events = ParseEvents(obj["events"] as JsonArray);
        cfg.RotateSeconds = ClampInt(obj, "rotate_seconds", 2, 600, 5);
        cfg.EventTemplate = Str(obj, "event_template", "距离{name}还有 {time} 天");
        if (cfg.EventTemplate.Length == 0)
            cfg.EventTemplate = "距离{name}还有 {time} 天";
        return cfg;
    }

    public static List<CountdownEvent> ParseEvents(JsonArray? array)
    {
        var events = new List<CountdownEvent>();
        if (array == null) return events;
        foreach (var entry in array)
        {
            if (entry is not JsonObject obj) continue;
            var deadline = Str(obj, "deadline", "").Trim();
            if (deadline.Length == 0) continue;
            events.Add(new CountdownEvent
            {
                Name = Str(obj, "name", "").Trim(),
                Deadline = deadline,
                Template = Str(obj, "template", "").Trim(),
                DurationDays = ClampInt(obj, "duration_days", 1, 365, 1),
                ProgressStart = DateOrEmpty(Str(obj, "progress_start", "")),
            });
        }
        return events;
    }

    public static string DateOrEmpty(string value)
    {
        var text = (value ?? "").Trim();
        if (text.Length > 10) text = text[..10];
        return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt) ? dt.ToString("yyyy-MM-dd") : "";
    }

    public static string Str(JsonObject obj, string key, string fallback)
    {
        var node = obj[key];
        if (node == null) return fallback;
        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToString();
        }
    }

    public static bool Bool(JsonObject obj, string key, bool fallback)
    {
        var node = obj[key];
        if (node == null) return fallback;
        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            var text = node.ToString().Trim().ToLowerInvariant();
            if (text == "true" || text == "1" || text == "yes") return true;
            if (text == "false" || text == "0" || text == "no") return false;
            return fallback;
        }
    }

    public static int ClampInt(JsonObject obj, string key, int low, int high, int fallback)
    {
        var node = obj[key];
        if (node == null) return fallback;
        try
        {
            var value = node.GetValue<int>();
            return Math.Clamp(value, low, high);
        }
        catch
        {
            try
            {
                var value = (int)Math.Round(node.GetValue<double>());
                return Math.Clamp(value, low, high);
            }
            catch
            {
                if (double.TryParse(node.ToString().Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed))
                    return Math.Clamp((int)Math.Round(parsed), low, high);
                return fallback;
            }
        }
    }

    public static double ClampDouble(JsonObject obj, string key, double low, double high,
        double fallback)
    {
        var node = obj[key];
        if (node == null) return fallback;
        try
        {
            return Math.Clamp(node.GetValue<double>(), low, high);
        }
        catch
        {
            if (double.TryParse(node.ToString().Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var parsed))
                return Math.Clamp(parsed, low, high);
            return fallback;
        }
    }

    private static string ColorOr(JsonObject obj, string key, string fallback)
    {
        var text = Str(obj, key, "").Trim();
        return ColorPattern.IsMatch(text) ? text : fallback;
    }

    private static string RepoOr(JsonObject obj, string key, string fallback)
    {
        var repo = Str(obj, key, "").Trim().Trim('/');
        if (repo.Count(c => c == '/') != 1 || repo.Any(char.IsWhiteSpace) ||
            repo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return fallback;
        return repo;
    }

    private static string BranchOr(JsonObject obj, string key, string fallback)
    {
        var branch = Str(obj, key, "").Trim();
        if (branch.Length == 0 || branch.Any(c => " \t\r\n~^:?#[\\".Contains(c)))
            return fallback;
        return branch;
    }

    private static string PathOr(JsonObject obj, string key, string fallback)
    {
        var path = Str(obj, key, "").Trim().Trim('/');
        if (path.Length == 0 || path.Contains("..") ||
            path.Any(c => " \t\r\n?#[\\".Contains(c)))
            return fallback;
        return path;
    }

    // ---------------- 密码 ----------------
    public const int PasswordIterations = 120_000;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var digest = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, PasswordIterations,
            HashAlgorithmName.SHA256, 32);
        return $"pbkdf2_sha256${PasswordIterations}${Convert.ToHexString(salt).ToLowerInvariant()}" +
               $"${Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        try
        {
            var parts = (stored ?? "").Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2_sha256") return false;
            var iterations = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var salt = Convert.FromHexString(parts[2]);
            var expected = Convert.FromHexString(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, iterations,
                HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static string PasswordHashOrEmpty(string value)
    {
        var text = (value ?? "").Trim();
        var parts = text.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2_sha256") return "";
        try
        {
            _ = int.Parse(parts[1], CultureInfo.InvariantCulture);
            _ = Convert.FromHexString(parts[2]);
            _ = Convert.FromHexString(parts[3]);
            return text;
        }
        catch
        {
            return "";
        }
    }
}

public static class ConfigStore
{
    public static readonly string ConfigDir = ResolveConfigDir();
    public static string ConfigFile => Path.Combine(ConfigDir, "config.json");
    public static string LogFile => Path.Combine(ConfigDir, "error.log");

    private static string ResolveConfigDir()
    {
        var overrideDir = Environment.GetEnvironmentVariable("GAOKAO_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            Directory.CreateDirectory(overrideDir);
            return overrideDir;
        }

        var primary = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.Id);
        try
        {
            Directory.CreateDirectory(primary);
            var probe = Path.Combine(primary, ".write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return primary;
        }
        catch
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "config");
            try { Directory.CreateDirectory(fallback); } catch { }
            return fallback;
        }
    }

    public static void LogError(string text)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.AppendAllText(LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}\n", Encoding.UTF8);
        }
        catch
        {
            // 日志失败不能影响主流程
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
                return AppConfig.FromJson(File.ReadAllText(ConfigFile, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            LogError("读取配置失败:\n" + ex);
        }
        return AppConfig.Default();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigFile, cfg.ToJson(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            LogError("保存配置失败:\n" + ex);
        }
    }
}
