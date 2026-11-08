using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamCountdown;

public sealed record UpdateInfo(string Version, string DownloadUrl);

/// <summary>远端版本检查：读取节假日仓库根目录的 version.json。</summary>
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

    /// <summary>
    /// 支持两种写法：
    /// 1. 纯文本：3.0.2
    /// 2. JSON：{"version": "3.0.3", "download_url": "https://.../Setup.exe"}
    /// </summary>
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
        catch (JsonException)
        {
            // 纯文本格式
        }
        return raw.Split('\n')[0].Trim().Trim('"', '\'', ',', '，').TrimStart('v', 'V');
    }

    /// <summary>从 version.json 里读取安装包下载地址；纯文本或没有该字段时返回空字符串。</summary>
    public static string ExtractDownloadUrl(string text)
    {
        var raw = text.TrimStart('\uFEFF').Trim();
        if (raw.Length == 0) return "";
        try
        {
            var node = JsonNode.Parse(raw);
            if (node is not JsonObject obj) return "";
            foreach (var key in new[]
                     {
                         "download_url", "download", "installer", "url",
                         "browser_download_url",
                     })
            {
                var candidate = obj[key]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
            }
        }
        catch (JsonException)
        {
            // 纯文本没有下载地址
        }
        return "";
    }

    public static async Task<UpdateInfo> FetchLatestAsync(AppConfig cfg)
    {
        var repo = string.IsNullOrWhiteSpace(cfg.HolidayRepo)
            ? "tomato-599/holidays_count" : cfg.HolidayRepo.Trim().Trim('/');
        var branch = string.IsNullOrWhiteSpace(cfg.HolidayBranch) ? "main" : cfg.HolidayBranch;
        // API 直取排在前面：jsDelivr 的分支缓存可能几小时内仍是旧文件
        var urls = new (string Url, bool GitHubApi)[]
        {
            ($"https://api.github.com/repos/{repo}/contents/version.json?ref={branch}", true),
            ($"https://raw.githubusercontent.com/{repo}/{branch}/version.json", false),
            ($"https://fastly.jsdelivr.net/gh/{repo}@{branch}/version.json", false),
            ($"https://cdn.jsdelivr.net/gh/{repo}@{branch}/version.json", false),
            ($"https://testingcf.jsdelivr.net/gh/{repo}@{branch}/version.json", false),
            ($"https://gcore.jsdelivr.net/gh/{repo}@{branch}/version.json", false),
        };

        var lastError = "";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", $"GaokaoCountdown/{AppInfo.Version}");
        foreach (var (url, githubApi) in urls)
        {
            try
            {
                string text;
                if (githubApi)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation(
                        "Accept", "application/vnd.github.raw");
                    using var response = await client.SendAsync(request).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                else
                {
                    text = await client.GetStringAsync(url).ConfigureAwait(false);
                }
                return new UpdateInfo(ExtractVersion(text), ExtractDownloadUrl(text));
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
