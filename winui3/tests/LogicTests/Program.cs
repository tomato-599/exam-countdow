using ExamCountdown;
using CountdownEvent = ExamCountdown.CountdownEvent;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("config dir: " + ConfigStore.ConfigDir);

var cfg = ConfigStore.Load();
Console.WriteLine($"deadline={cfg.Deadline} template={cfg.Template}");
Console.WriteLine($"events={cfg.Events.Count} rotate={cfg.RotateSeconds}");
foreach (var item in cfg.Items())
{
    var days = Render.CountDays(cfg, item.Deadline);
    Console.WriteLine($"  name=[{item.Name}] deadline={item.Deadline} dur={item.DurationDays} " +
                      $"start={item.ProgressStart} days={days} percent={Render.ProgressPercent(cfg, item)} " +
                      $"text={Render.PlainText(cfg, days, item)}");
}

var main = cfg.Items()[0];
Console.WriteLine("main percent=" + Render.ProgressPercent(cfg, main));

var custom = AppConfig.Default();
custom.ProgressStartMode = "custom";
custom.ProgressStart = DateTime.Today.ToString("yyyy-MM-dd");
var pastDeadline = DateTime.Today.AddDays(100).ToString("yyyy-MM-dd HH:mm:ss");
Console.WriteLine("custom today start percent=" +
                  Render.ProgressPercent(custom, new CountdownEvent { Deadline = pastDeadline }));

var multi = new CountdownEvent
{
    Name = "二模",
    Deadline = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"),
    DurationDays = 4,
};
Console.WriteLine("ongoing text=" + Render.PlainText(cfg, 0, multi) +
                  " percent=" + Render.ProgressPercent(cfg, multi));

var count = HolidayCalendar.SchoolDaysBetween(
    new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
Console.WriteLine("school days 2026-09-01..09-30 = " + count);

var hash = AppConfig.HashPassword("1234");
Console.WriteLine($"hash={hash[..24]}... verifyTrue={AppConfig.VerifyPassword("1234", hash)} " +
                  $"verifyFalse={AppConfig.VerifyPassword("1235", hash)}");

var json = """{"progress_start_mode":"custom","opacity":5,"events":[{"name":"A","deadline":"2027-01-01","duration_days":"3"}]}""";
var updateJson = """{"version":"3.0.3","download_url":"https://github.com/tomato-599/exam-countdow/releases/download/v3.0.3/exam-countdow-Setup-3.0.3.exe"}""";
Console.WriteLine("update version=" + UpdateService.ExtractVersion(updateJson) +
                  " url=" + UpdateService.ExtractDownloadUrl(updateJson) +
                  " newer=" + UpdateService.IsNewer("3.0.3", "3.0.2"));
Console.WriteLine("plain version=" + UpdateService.ExtractVersion("3.0.2") +
                  " url=[" + UpdateService.ExtractDownloadUrl("3.0.2") + "]");

var normalized = AppConfig.FromJson(json);
Console.WriteLine($"normalized mode={normalized.ProgressStartMode} start={normalized.ProgressStart} " +
                  $"opacity={normalized.Opacity} events={normalized.Events.Count} dur={normalized.Events[0].DurationDays}");

if (args.Contains("--fetch"))
{
    var live = await UpdateService.FetchLatestAsync(cfg);
    Console.WriteLine($"live version={live.Version} url={live.DownloadUrl}");
}

var dumped = cfg.ToJson();
Console.WriteLine("json len=" + dumped.Length);
Console.WriteLine(dumped.Substring(0, Math.Min(160, dumped.Length)));
