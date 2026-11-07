using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace ExamCountdown;

/// <summary>
/// 法定节假日 / 调休数据：内置表离线兜底 + GitHub JSON 覆盖，统计上学日。
/// 远程 JSON 与 holidays_count 仓库格式一致，并与旧版 holidays_cache.json 兼容。
/// </summary>
public static class HolidayCalendar
{
    private static readonly Dictionary<int, (string Start, string End)[]> HolidayRanges = new()
    {
        [2025] = new[]
        {
            ("2025-01-01", "2025-01-01"),
            ("2025-01-28", "2025-02-04"),
            ("2025-04-04", "2025-04-06"),
            ("2025-05-01", "2025-05-05"),
            ("2025-05-31", "2025-06-02"),
            ("2025-10-01", "2025-10-08"),
        },
        [2026] = new[]
        {
            ("2026-01-01", "2026-01-03"),
            ("2026-02-15", "2026-02-23"),
            ("2026-04-04", "2026-04-06"),
            ("2026-05-01", "2026-05-05"),
            ("2026-06-19", "2026-06-21"),
            ("2026-09-25", "2026-09-27"),
            ("2026-10-01", "2026-10-07"),
        },
        [2027] = new[]
        {
            ("2027-01-01", "2027-01-03"),
            ("2027-02-05", "2027-02-11"),
            ("2027-04-03", "2027-04-05"),
            ("2027-05-01", "2027-05-05"),
            ("2027-06-08", "2027-06-10"),
        },
    };

    private static readonly Dictionary<int, string[]> MakeupWorkdays = new()
    {
        [2025] = new[] { "2025-01-26", "2025-02-08", "2025-04-27", "2025-09-28", "2025-10-11" },
        [2026] = new[] { "2026-01-04", "2026-02-14", "2026-02-28", "2026-05-09", "2026-09-20", "2026-10-10" },
        [2027] = new[] { "2027-02-13", "2027-02-20", "2027-04-25", "2027-05-08" },
    };

    private static readonly HashSet<DateOnly> BuiltinHolidays = BuildHolidays();
    private static readonly HashSet<DateOnly> BuiltinMakeup = BuildMakeup();

    private static HashSet<DateOnly> _holidays = new(BuiltinHolidays);
    private static HashSet<DateOnly> _makeup = new(BuiltinMakeup);

    private static readonly string[] RestKeys = { "放假日", "放假", "rest" };
    private static readonly string[] MakeupKeys = { "调休日", "补班日", "调休上班", "makeup" };

    private static HashSet<DateOnly> BuildHolidays()
    {
        var days = new HashSet<DateOnly>();
        foreach (var ranges in HolidayRanges.Values)
        {
            foreach (var (startText, endText) in ranges)
            {
                var current = DateOnly.ParseExact(startText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var end = DateOnly.ParseExact(endText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                while (current <= end)
                {
                    days.Add(current);
                    current = current.AddDays(1);
                }
            }
        }
        return days;
    }

    private static HashSet<DateOnly> BuildMakeup()
    {
        var days = new HashSet<DateOnly>();
        foreach (var values in MakeupWorkdays.Values)
            foreach (var text in values)
                days.Add(DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        return days;
    }

    public static bool IsSchoolDay(DateOnly day)
    {
        if (_makeup.Contains(day)) return true;
        return day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
               && !_holidays.Contains(day);
    }

    public static int SchoolDaysBetween(DateOnly start, DateOnly end)
    {
        var holidays = _holidays;
        var makeup = _makeup;
        var count = 0;
        for (var current = start.AddDays(1); current <= end; current = current.AddDays(1))
        {
            if (makeup.Contains(current) ||
                (current.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                 && !holidays.Contains(current)))
                count++;
        }
        return count;
    }

    public static void ResetToBuiltin()
    {
        _holidays = new HashSet<DateOnly>(BuiltinHolidays);
        _makeup = new HashSet<DateOnly>(BuiltinMakeup);
    }

    public static (List<int> Years, int RestCount, int MakeupCount) ApplyPayload(JsonNode? payload)
    {
        var (rest, makeup, years) = ParsePayload(payload);
        var activeRest = new HashSet<DateOnly>(
            _holidays.Where(d => !years.Contains(d.Year)));
        activeRest.UnionWith(rest);
        var activeMakeup = new HashSet<DateOnly>(
            _makeup.Where(d => !years.Contains(d.Year)));
        activeMakeup.UnionWith(makeup);
        _holidays = activeRest;
        _makeup = activeMakeup;
        return (years, rest.Count, makeup.Count);
    }

    public static (HashSet<DateOnly> RestDays, HashSet<DateOnly> MakeupDays, List<int> Years)
        ParsePayload(JsonNode? payload)
    {
        if (payload is not JsonObject root)
            throw new FormatException("顶层必须是 {年份: {月份: {...}}} 形式的对象");

        var rest = new HashSet<DateOnly>();
        var makeup = new HashSet<DateOnly>();
        var years = new List<int>();

        foreach (var (yearKey, monthsNode) in root)
        {
            if (yearKey.Length != 4 || !int.TryParse(yearKey, out var year) ||
                monthsNode is not JsonObject months)
                continue;
            years.Add(year);
            foreach (var (_, holidaysNode) in months)
            {
                if (holidaysNode is not JsonObject holidays) continue;
                foreach (var (_, infoNode) in holidays)
                {
                    if (infoNode is not JsonObject info) continue;
                    foreach (var day in IterDates(info, RestKeys, year)) rest.Add(day);
                    foreach (var day in IterDates(info, MakeupKeys, year)) makeup.Add(day);
                }
            }
        }

        if (rest.Count == 0 && makeup.Count == 0)
            throw new FormatException("没有解析到任何有效日期（检查年份键与 MMDD 格式）");
        return (rest, makeup, years.Distinct().OrderBy(y => y).ToList());
    }

    private static IEnumerable<DateOnly> IterDates(JsonObject info, string[] keys, int year)
    {
        foreach (var key in keys)
        {
            var node = info[key];
            IEnumerable<JsonNode?> items = node switch
            {
                JsonArray array => array,
                null => Array.Empty<JsonNode?>(),
                _ => new[] { node },
            };
            foreach (var item in items)
            {
                var text = item?.ToString().Trim() ?? "";
                if (text.Length != 4 || !text.All(char.IsDigit)) continue;
                if (!int.TryParse(text[..2], out var month) ||
                    !int.TryParse(text[2..], out var day)) continue;
                if (month is < 1 or > 12) continue;
                if (day < 1 || day > DateTime.DaysInMonth(year, month)) continue;
                yield return new DateOnly(year, month, day);
            }
        }
    }
}

public sealed class HolidayStore
{
    private static readonly HttpClient Http = CreateClient();

    public string UpdatedAt { get; private set; } = "";
    public string Source { get; private set; } = "";
    public string LastError { get; private set; } = "";
    public List<int> DataYears { get; private set; } = new();
    public AppConfig Config { get; set; }

    public HolidayStore(AppConfig cfg)
    {
        Config = cfg;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", $"GaokaoCountdown/{AppInfo.Version}");
        return client;
    }

    public string CacheFile => Path.Combine(ConfigStore.ConfigDir, "holidays_cache.json");

    private string SourceText()
    {
        var repo = string.IsNullOrWhiteSpace(Config.HolidayRepo)
            ? "tomato-599/holidays_count" : Config.HolidayRepo;
        var branch = string.IsNullOrWhiteSpace(Config.HolidayBranch) ? "main" : Config.HolidayBranch;
        var file = string.IsNullOrWhiteSpace(Config.HolidayFile) ? "holidays.json" : Config.HolidayFile;
        return $"{repo}@{branch}/{file}";
    }

    public List<string> Urls()
    {
        var repo = string.IsNullOrWhiteSpace(Config.HolidayRepo)
            ? "tomato-599/holidays_count" : Config.HolidayRepo;
        var branch = string.IsNullOrWhiteSpace(Config.HolidayBranch) ? "main" : Config.HolidayBranch;
        var file = string.IsNullOrWhiteSpace(Config.HolidayFile) ? "holidays.json" : Config.HolidayFile;
        return new List<string>
        {
            $"https://raw.githubusercontent.com/{repo}/{branch}/{file}",
            $"https://fastly.jsdelivr.net/gh/{repo}@{branch}/{file}",
            $"https://cdn.jsdelivr.net/gh/{repo}@{branch}/{file}",
            $"https://testingcf.jsdelivr.net/gh/{repo}@{branch}/{file}",
            $"https://gcore.jsdelivr.net/gh/{repo}@{branch}/{file}",
        };
    }

    public bool LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return false;
            var blob = JsonNode.Parse(File.ReadAllText(CacheFile, Encoding.UTF8));
            if (blob is not JsonObject obj) return false;
            var payload = obj["data"];
            var (years, _, _) = HolidayCalendar.ApplyPayload(payload);
            DataYears = years;
            UpdatedAt = obj["fetched_at"]?.ToString() ?? "";
            Source = obj["source"]?.ToString() ?? "";
            return true;
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("读取节假日缓存失败:\n" + ex);
            return false;
        }
    }

    public bool IsStale()
    {
        if (string.IsNullOrWhiteSpace(UpdatedAt)) return true;
        if (!DateTime.TryParse(UpdatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var stamp))
            return true;
        var hours = Math.Clamp(Config.HolidayCacheHours, 1, 168);
        return (DateTime.Now - stamp).TotalSeconds > hours * 3600;
    }

    public async Task<(bool Ok, string Message)> FetchAsync(bool force = false)
    {
        if (!force && !IsStale() && UpdatedAt.Length > 0)
            return (true, "节假日数据已是最新");

        var lastError = "";
        foreach (var url in Urls())
        {
            try
            {
                var raw = await Http.GetStringAsync(url).ConfigureAwait(false);
                var payload = JsonNode.Parse(raw.TrimStart('\uFEFF'))
                              ?? throw new FormatException("节假日文件为空");
                var (years, restCount, makeupCount) = HolidayCalendar.ApplyPayload(payload);
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                Source = SourceText();
                LastError = "";
                DataYears = years;
                SaveCache(payload);
                return (true,
                    $"节假日数据已更新：{years.Count} 年，{restCount} 个放假日、{makeupCount} 个补班日");
            }
            catch (Exception ex)
            {
                lastError = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        LastError = lastError;
        ConfigStore.LogError($"节假日数据获取失败（{SourceText()}）: {lastError}");
        return (false, $"节假日数据获取失败：{lastError}");
    }

    private void SaveCache(JsonNode payload)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.ConfigDir);
            var blob = new JsonObject
            {
                ["fetched_at"] = UpdatedAt,
                ["source"] = Source,
                ["data"] = payload.DeepClone(),
            };
            File.WriteAllText(CacheFile, blob.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ConfigStore.LogError("写入节假日缓存失败:\n" + ex);
        }
    }

    public string StatusText()
    {
        if (UpdatedAt.Length > 0)
        {
            var stamp = UpdatedAt.Replace("T", " ");
            if (stamp.Length > 16) stamp = stamp[..16];
            return $"已更新 {stamp}（{DataYears.Count} 年）";
        }
        if (LastError.Length > 0) return "更新失败，使用内置数据";
        return "内置数据";
    }
}
