using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Acclaro.Platform.Application.Apis.Holiday.Dtos;
using Acclaro.Platform.Common.Common;

namespace Acclaro.Platform.Application.Apis.Holiday;

public class HolidayService
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly IHolidayApi _holidayApi;
    private readonly string _holidayDirectory;
    private readonly ConcurrentDictionary<int, Dictionary<string, HolidayItem>> _cache = new(); // year -> (MM-dd -> item)

    public HolidayService(IHolidayApi holidayApi)
    {
        _holidayApi = holidayApi;
        _holidayDirectory = AppDataPaths.GetDirectory("holiday");
    }

    public async Task InitializeAsync(bool nextYear = false)
    {
        var year = DateTime.Today.Year;

        await EnsureHolidayLoadedAsync(year);

        if (nextYear && DateTime.Today.Month == 12)
        {
            await EnsureHolidayLoadedAsync(year + 1);
        }
    }

    public async Task EnsureHolidayLoadedAsync(int year)
    {
        if (_cache.ContainsKey(year)) return;

        var file = GetHolidayFile(year);
        if (!File.Exists(file))
        {
            var downloaded = await DownloadYearJsonAsync(year);
            await File.WriteAllTextAsync(file, downloaded);
        }

        var json = await File.ReadAllTextAsync(file);

        // 提莫节假日数据（AOT：使用源生成器，避免反射反序列化）
        var response = JsonSerializer.Deserialize(json, HolidayJsonContext.Default.ResponseDictionaryStringHolidayItem);
        if (response?.Code != 0 || response.Holiday is null)
            throw new InvalidOperationException($"节假日数据无效: {file}");
        _cache[year] = response.Holiday;
    }

    /// <summary>
    /// 先走 WebApiClient；若被 Cloudflare 挑战，再回退到带浏览器 UA 的 curl.exe。
    /// 裸 curl 无 -A 会得到 Just a moment...；加浏览器 UA 后通常为 200。
    /// </summary>
    private async Task<string> DownloadYearJsonAsync(int year, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _holidayApi.GetYearAsync(year, cancellationToken);
            if (IsValidHolidayJson(json)) return json;
        }
        catch
        {
            // fall through to curl
        }

        var url = $"https://timor.tech/api/holiday/year/{year}";
        return await DownloadViaCurlAsync(url, cancellationToken);
    }

    private static bool IsValidHolidayJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var trimmed = json.TrimStart();
        return trimmed.StartsWith('{')
               && trimmed.Contains("\"holiday\"", StringComparison.Ordinal)
               && !trimmed.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> DownloadViaCurlAsync(string url, CancellationToken cancellationToken)
    {
        // 写入临时文件再按 UTF-8 读取，避免控制台/管道编码导致中文乱码
        var tempFile = Path.Combine(Path.GetTempPath(), $"holiday-{Guid.NewGuid():N}.json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "curl.exe",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("-sS");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("-A");
            psi.ArgumentList.Add(BrowserUserAgent);
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add("Accept: application/json");
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add("Accept-Language: zh-CN,zh;q=0.9");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(tempFile);
            psi.ArgumentList.Add(url);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 curl.exe");

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || !File.Exists(tempFile))
            {
                throw new InvalidOperationException(
                    $"curl 下载节假日失败 (exit={process.ExitCode}). stderr={stderr}");
            }

            var json = await File.ReadAllTextAsync(tempFile, Encoding.UTF8, cancellationToken);
            if (!IsValidHolidayJson(json))
                throw new InvalidOperationException("curl 下载内容不是有效的节假日 JSON。");

            return json;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* ignore */ }
        }
    }

    /// <summary>指定日期是否工作日，true 是</summary>
    public async Task<bool> IsWorkDayAsync(DateTime date)
    {
        var year = date.Year;
        if (!_cache.ContainsKey(year)) await EnsureHolidayLoadedAsync(year);
        if (!_cache.TryGetValue(year, out var map)) return IsNormalWeekday(date);
        var key = date.ToString("MM-dd");
        if (map.TryGetValue(key, out var item))
        {
            // true=放假，false=补班
            return !item.Holiday;
        }
        return IsNormalWeekday(date);
    }

    /// <summary>获取指定日期之后的下一个工作日（不含当天）</summary>
    public async Task<DateTime> GetNextWorkDayAsync(DateTime date)
    {
        var next = date.Date.AddDays(1);
        // 跨年时 EnsureHolidayLoadedAsync 会在 IsWorkDayAsync 里自动加载
        while (!await IsWorkDayAsync(next))
        {
            next = next.AddDays(1);
        }
        return next;
    }

    /// <summary>
    /// 获取下一个工作日，并判断相对基准日期是否跨月
    /// </summary>
    /// <returns>
    /// NextWorkDay: 下一个工作日
    /// CrossMonth: 是否跨月（月份或年份不同）
    /// </returns>
    public async Task<(DateTime nextWorkDay, bool crossMonth)> GetNextWorkDayWithCrossMonthAsync(DateTime date)
    {
        var nextWorkDay = await GetNextWorkDayAsync(date);
        var crossMonth = nextWorkDay.Year != date.Year || nextWorkDay.Month != date.Month;
        return (nextWorkDay, crossMonth);
    }

    /// <summary>是否为本周最后一个工作日（今天是工作日，且下一工作日跨周；周一为一周起点）</summary>
    public async Task<bool> IsLastWorkDayOfWeekAsync(DateTime date)
    {
        date = date.Date;
        if (!await IsWorkDayAsync(date)) return false;

        var next = await GetNextWorkDayAsync(date);
        return StartOfWeekMonday(next) != StartOfWeekMonday(date);
    }

    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }

    /// <summary>是否为本月最后一个工作日（今天是工作日，且下一工作日跨月）</summary>
    public async Task<bool> IsLastWorkDayOfMonthAsync(DateTime date)
    {
        date = date.Date;
        if (!await IsWorkDayAsync(date)) return false;
        var (_, crossMonth) = await GetNextWorkDayWithCrossMonthAsync(date);
        return crossMonth;
    }

    /// <summary>指定日期是否工作日，true 是</summary>
    private static bool IsNormalWeekday(DateTime date)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private string GetHolidayFile(int year) => Path.Combine(_holidayDirectory, $"holiday{year}.json");
}
