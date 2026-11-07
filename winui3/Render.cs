using System.Globalization;
using Microsoft.Win32;

namespace ExamCountdown;

public sealed record TextRun(string Text, double FontSize, bool Bold, string Color);

/// <summary>文字渲染：剩余天数、多天考试进度、模板占位符、进度百分比。</summary>
public static class Render
{
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppInfo.PersonalizeKey);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int number && number == 0;
        }
        catch
        {
            return false;
        }
    }

    public static (string TextColor, string BgColor) ResolveColors(AppConfig cfg, bool dark)
    {
        if (cfg.ColorMode == "custom") return (cfg.TextColor, cfg.BgColor);
        return dark ? ("#F2F2F2", "#1E1E1E") : ("#202124", "#FFFFFF");
    }

    public static DateTime? ParseDeadline(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return null;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return dt;
        if (text.Length >= 10 && DateTime.TryParse(text[..10], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dateOnly))
            return dateOnly;
        return null;
    }

    public static DateOnly? ParseDate(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length > 10) text = text[..10];
        if (text.Length == 0) return null;
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day) ? day : null;
    }

    public static int CountDays(AppConfig cfg, string? deadline)
    {
        var dt = ParseDeadline(deadline);
        if (dt == null) return 0;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var target = DateOnly.FromDateTime(dt.Value);
        if (cfg.SchoolDays)
        {
            if (target < today) return target.DayNumber - today.DayNumber;
            return HolidayCalendar.SchoolDaysBetween(today, target);
        }
        return target.DayNumber - today.DayNumber;
    }

    public static int DurationDays(AppConfig cfg, CountdownEvent? item = null)
    {
        var raw = item?.DurationDays ?? 0;
        if (raw <= 0) raw = cfg.DurationDays;
        return Math.Clamp(raw, 1, 365);
    }

    public static (int Day, int Total)? ExamProgress(AppConfig cfg, CountdownEvent? item)
    {
        var raw = !string.IsNullOrWhiteSpace(item?.Deadline) ? item!.Deadline : cfg.Deadline;
        var dt = ParseDeadline(raw);
        if (dt == null) return null;
        var total = DurationDays(cfg, item);
        if (total <= 1) return null;
        var offset = DateOnly.FromDateTime(DateTime.Today).DayNumber
                     - DateOnly.FromDateTime(dt.Value).DayNumber;
        if (offset > 0 && offset < total) return (offset + 1, total);
        return null;
    }

    public static DateOnly SchoolYearStart(DateOnly deadline)
    {
        var year = deadline.Month >= 9 ? deadline.Year : deadline.Year - 1;
        return new DateOnly(year, 9, 1);
    }

    public static DateOnly? ResolveProgressStart(AppConfig cfg, CountdownEvent? item)
    {
        var custom = ParseDate(item?.ProgressStart);
        if (custom != null) return custom;
        if (cfg.ProgressStartMode == "custom") return ParseDate(cfg.ProgressStart);
        return null;
    }

    public static int ProgressPercent(AppConfig cfg, CountdownEvent? item)
    {
        var raw = !string.IsNullOrWhiteSpace(item?.Deadline) ? item!.Deadline : cfg.Deadline;
        var dt = ParseDeadline(raw);
        if (dt == null) return 0;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var deadline = DateOnly.FromDateTime(dt.Value);
        var total = DurationDays(cfg, item);
        var offset = today.DayNumber - deadline.DayNumber;
        if (offset >= total) return 100;
        if (offset > 0) return Math.Min(100, (int)Math.Round((offset + 1) * 100.0 / total));
        var start = ResolveProgressStart(cfg, item) ?? SchoolYearStart(deadline);
        var span = deadline.DayNumber - start.DayNumber;
        if (span <= 0) return today >= deadline ? 100 : 0;
        return Math.Clamp((int)Math.Round((today.DayNumber - start.DayNumber) * 100.0 / span),
            0, 100);
    }

    public static (string Template, string Zero, string Past, string Name)
        ResolveTexts(AppConfig cfg, CountdownEvent? item)
    {
        item ??= new CountdownEvent();
        var name = item.Name ?? "";
        string template;
        if (!string.IsNullOrWhiteSpace(item.Template)) template = item.Template;
        else if (name.Length > 0) template = cfg.EventTemplate;
        else template = cfg.Template;
        var zero = (cfg.ZeroText ?? "").Replace("{name}", name);
        var past = (cfg.PastText ?? "").Replace("{name}", name);
        return (template, zero, past, name);
    }

    private static string FormatOngoing(AppConfig cfg, CountdownEvent? item, int day, int total)
    {
        item ??= new CountdownEvent();
        var name = item.Name ?? "";
        var raw = !string.IsNullOrWhiteSpace(item.Deadline) ? item.Deadline : cfg.Deadline;
        var dateText = (raw ?? "").Length > 10 ? raw![..10] : raw ?? "";
        var template = (cfg.OngoingText ?? "").Trim();
        if (template.Length == 0) template = "考试进行中 · 第{day}天";
        var percent = total > 0 ? Math.Min(100, (int)Math.Round(day * 100.0 / total)) : 0;
        return template
            .Replace("{name}", name)
            .Replace("{date}", dateText)
            .Replace("{day}", day.ToString(CultureInfo.InvariantCulture))
            .Replace("{total}", total.ToString(CultureInfo.InvariantCulture))
            .Replace("{percent}", percent.ToString(CultureInfo.InvariantCulture));
    }

    public static List<TextRun> BuildRuns(AppConfig cfg, int days, CountdownEvent? item, bool dark)
    {
        var (textColor, _) = ResolveColors(cfg, dark);
        var urgentColor = cfg.UrgentColor;
        var baseSize = cfg.BaseFontSize;
        var urgentSize = cfg.UrgentFontSize;
        var zeroSize = cfg.ZeroFontSize;
        var (template, zeroText, pastText, name) = ResolveTexts(cfg, item);

        if (days <= 0)
        {
            var progress = ExamProgress(cfg, item);
            string text;
            if (progress != null)
            {
                text = FormatOngoing(cfg, item, progress.Value.Day, progress.Value.Total);
            }
            else
            {
                text = (days == 0 ? zeroText : pastText);
                if (text.Length == 0) text = zeroText;
                text = text.Replace("{percent}", "100");
            }
            return new List<TextRun>
            {
                new(text, zeroSize, true, urgentColor),
            };
        }

        var raw = !string.IsNullOrWhiteSpace(item?.Deadline) ? item!.Deadline : cfg.Deadline;
        var dateText = (raw ?? "").Length > 10 ? raw![..10] : raw ?? "";
        var percent = ProgressPercent(cfg, item).ToString(CultureInfo.InvariantCulture);

        string prefix, suffix;
        var placeholder = template.Contains("{time}") ? "{time}"
            : template.Contains("{days}") ? "{days}" : null;
        if (placeholder != null)
        {
            var index = template.IndexOf(placeholder, StringComparison.Ordinal);
            prefix = template[..index];
            suffix = template[(index + placeholder.Length)..];
        }
        else
        {
            prefix = template + " ";
            suffix = "";
        }

        prefix = prefix.Replace("{date}", dateText).Replace("{name}", name)
            .Replace("{percent}", percent);
        suffix = suffix.Replace("{date}", dateText).Replace("{name}", name)
            .Replace("{percent}", percent);

        var urgent = days < cfg.UrgentDays;
        return new List<TextRun>
        {
            new(prefix, baseSize, false, textColor),
            new(days.ToString(CultureInfo.InvariantCulture),
                urgent ? urgentSize : baseSize, true, urgent ? urgentColor : textColor),
            new(suffix, baseSize, false, textColor),
        };
    }

    public static string PlainText(AppConfig cfg, int days, CountdownEvent? item = null)
    {
        var (template, zeroText, pastText, name) = ResolveTexts(cfg, item);
        if (days <= 0)
        {
            var progress = ExamProgress(cfg, item);
            if (progress != null)
                return FormatOngoing(cfg, item, progress.Value.Day, progress.Value.Total);
            var text = days == 0 ? zeroText : pastText;
            return text.Replace("{percent}", "100");
        }
        var raw = !string.IsNullOrWhiteSpace(item?.Deadline) ? item!.Deadline : cfg.Deadline;
        var dateText = (raw ?? "").Length > 10 ? raw![..10] : raw ?? "";
        var result = template.Length > 0 ? template : "{time}";
        result = result.Replace("{time}", days.ToString(CultureInfo.InvariantCulture))
            .Replace("{days}", days.ToString(CultureInfo.InvariantCulture))
            .Replace("{date}", dateText)
            .Replace("{name}", name);
        return result.Replace("{percent}",
            ProgressPercent(cfg, item).ToString(CultureInfo.InvariantCulture));
    }
}
