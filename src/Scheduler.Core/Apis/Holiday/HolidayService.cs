using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Scheduler.Core.Apis.Holiday.Dtos;
using Scheduler.Common;
using Microsoft.Extensions.Configuration;

namespace Scheduler.Core.Apis.Holiday;

/// <summary>
/// 节假日服务：加载/缓存全年数据，并提供工作日相关判断。
/// 本地文件不存在时按 Timor → curl → 接口坞 顺序下载，最终统一落盘为 Timor 结构。
/// </summary>
public class HolidayService
{
    /// <summary>接口坞：工作日（含调休上班）</summary>
    private const int ApiHubsWorkday = 1;

    /// <summary>接口坞：非工作日</summary>
    private const int ApiHubsNonWorkday = 2;

    /// <summary>接口坞：非节假日</summary>
    private const int ApiHubsHolidayNone = 10;

    /// <summary>接口坞：法定节假日</summary>
    private const int ApiHubsLegalYes = 1;

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly IHolidayApi _holidayApi;
    private readonly IApiHubsHolidayApi _apiHubsHolidayApi;
    private readonly string? _apiHubsApiKey;
    private readonly string _holidayDirectory;

    /// <summary>year -> (MM-dd -> item)</summary>
    private readonly ConcurrentDictionary<int, Dictionary<string, HolidayItem>> _cache = new();

    public HolidayService(
        IHolidayApi holidayApi,
        IApiHubsHolidayApi apiHubsHolidayApi,
        IConfiguration configuration)
    {
        _holidayApi = holidayApi;
        _apiHubsHolidayApi = apiHubsHolidayApi;
        _apiHubsApiKey = configuration["Holiday:ApiHubsApiKey"];
        _holidayDirectory = AppDataPaths.GetDirectory("holiday");
    }

    /// <summary>
    /// 确保当年节假日就绪；12 月时可顺带加载下一年。
    /// </summary>
    public async Task InitializeAsync(bool nextYear = false)
    {
        var year = DateTime.Today.Year;

        await EnsureHolidayLoadedAsync(year);

        if (nextYear && DateTime.Today.Month == 12)
        {
            await EnsureHolidayLoadedAsync(year + 1);
        }
    }

    /// <summary>
    /// 确保指定年份数据在内存缓存中：本地有文件则直接读，否则先下载再落盘。
    /// </summary>
    public async Task EnsureHolidayLoadedAsync(int year)
    {
        if (_cache.ContainsKey(year)) return;

        var file = GetHolidayFile(year);
        if (!File.Exists(file))
        {
            try
            {
                var downloaded = await DownloadYearJsonAsync(year);
                await File.WriteAllTextAsync(file, downloaded);
            }
            catch (HolidayNotPublishedException)
            {
                // 这一年还没有公布，不落盘，工作日判断按普通周末处理。
                return;
            }
        }

        var json = await File.ReadAllTextAsync(file);

        // 提莫节假日数据（AOT：使用源生成器，避免反射反序列化）
        var response = JsonSerializer.Deserialize(json, HolidayJsonContext.Default.ResponseDictionaryStringHolidayItem);
        if (response?.Code != 0 || response.Holiday is null)
            throw new InvalidOperationException($"节假日数据无效: {file}");
        _cache[year] = response.Holiday;
    }

    /// <summary>
    /// 下载全年 JSON：先 WebApiClient 调 Timor；失败或 Cloudflare 挑战后再 curl；
    /// 仍失败则走接口坞，并转换为 Timor 格式。
    /// </summary>
    private async Task<string> DownloadYearJsonAsync(int year, CancellationToken cancellationToken = default)
    {
        var sawEmpty = false;
        try
        {
            var json = await _holidayApi.GetYearAsync(year, cancellationToken);
            switch (ClassifyHolidayJson(json))
            {
                case HolidayJsonKind.Ready:
                    return json;
                case HolidayJsonKind.Empty:
                    sawEmpty = true;
                    break;
            }
        }
        catch
        {
            // 继续尝试 curl
        }

        try
        {
            var url = $"https://timor.tech/api/holiday/year/{year}";
            var viaCurl = await DownloadViaCurlAsync(url, cancellationToken);
            switch (ClassifyHolidayJson(viaCurl))
            {
                case HolidayJsonKind.Ready:
                    return viaCurl;
                case HolidayJsonKind.Empty:
                    sawEmpty = true;
                    break;
            }
        }
        catch
        {
            // 继续尝试接口坞
        }

        try
        {
            return await DownloadViaApiHubsAsync(year, cancellationToken);
        }
        catch (HolidayNotPublishedException)
        {
            sawEmpty = true;
        }
        catch (Exception) when (sawEmpty)
        {
            // Timor 已经说明这一年是空的，后面的源失败不再改成下载失败。
        }

        if (sawEmpty)
            throw new HolidayNotPublishedException(year);

        throw new InvalidOperationException($"节假日数据无效 (year={year}).");
    }

    /// <summary>从接口坞拉取全年日历并转换为 Timor year JSON。</summary>
    private async Task<string> DownloadViaApiHubsAsync(int year, CancellationToken cancellationToken)
    {
        // 空 key 走匿名额度（约 5 次/天）；有 key 则按用户计费/限流
        var apiKey = string.IsNullOrWhiteSpace(_apiHubsApiKey) ? null : _apiHubsApiKey.Trim();
        var raw = await _apiHubsHolidayApi.GetAsync(
            year,
            size: 366,
            cn: 1,
            page: 1,
            api_key: apiKey,
            token: cancellationToken);

        var api = JsonSerializer.Deserialize(raw, HolidayJsonContext.Default.ApiHubsResponse);
        if (api?.Code != 0)
            throw new InvalidOperationException($"接口坞节假日数据无效 (year={year}).");
        if (api.Data?.List is not { Count: > 0 } list)
            throw new HolidayNotPublishedException(year);

        var timorJson = ConvertApiHubsToTimorJson(list);
        if (ClassifyHolidayJson(timorJson) == HolidayJsonKind.Empty)
            throw new HolidayNotPublishedException(year);
        if (ClassifyHolidayJson(timorJson) != HolidayJsonKind.Ready)
            throw new InvalidOperationException("接口坞数据转换后不是有效的节假日 JSON。");

        return timorJson;
    }

    /// <summary>
    /// 将接口坞按天列表转为 Timor 结构：只保留相对自然周的例外日
    /// （周末补班、工作日放假），普通工作日/周末不写入 map。
    /// </summary>
    internal static string ConvertApiHubsToTimorJson(IEnumerable<ApiHubsHolidayDay> days)
    {
        var map = new Dictionary<string, HolidayItem>(StringComparer.Ordinal);

        foreach (var day in days)
        {
            if (!TryParseYmd(day.Date, out var date)) continue;

            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var isWorkday = day.Workday == ApiHubsWorkday;
            var isRest = day.Workday == ApiHubsNonWorkday;

            // 周末上班 → 补班（holiday=false）；工作日休息 → 放假（holiday=true）
            if (isWorkday && isWeekend)
            {
                map[date.ToString("MM-dd")] = new HolidayItem
                {
                    Holiday = false,
                    Name = ResolveMakeupName(day),
                    Wage = 1,
                    Date = date.ToString("yyyy-MM-dd")
                };
            }
            else if (isRest && !isWeekend)
            {
                map[date.ToString("MM-dd")] = new HolidayItem
                {
                    Holiday = true,
                    Name = ResolveHolidayName(day),
                    Wage = day.HolidayLegal == ApiHubsLegalYes ? 3 : 2,
                    Date = date.ToString("yyyy-MM-dd")
                };
            }
        }

        var response = new Response<Dictionary<string, HolidayItem>>
        {
            Code = 0,
            Holiday = map
        };

        return JsonSerializer.Serialize(response, HolidayJsonContext.Default.ResponseDictionaryStringHolidayItem);
    }

    /// <summary>放假日显示名：优先接口中文节假日名。</summary>
    private static string ResolveHolidayName(ApiHubsHolidayDay day)
    {
        if (!string.IsNullOrWhiteSpace(day.HolidayCn)
            && day.Holiday != ApiHubsHolidayNone
            && !day.HolidayCn.Contains("非节假日", StringComparison.Ordinal))
            return day.HolidayCn!;

        return "放假";
    }

    /// <summary>补班日显示名：优先调休中文说明。</summary>
    private static string ResolveMakeupName(ApiHubsHolidayDay day)
    {
        if (!string.IsNullOrWhiteSpace(day.HolidayOvertimeCn)
            && !day.HolidayOvertimeCn.Contains("非", StringComparison.Ordinal))
            return day.HolidayOvertimeCn!;

        return "调休";
    }

    private static bool TryParseYmd(int ymd, out DateTime date)
    {
        date = default;
        var s = ymd.ToString(CultureInfo.InvariantCulture);
        return DateTime.TryParseExact(
            s,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>粗判是否为 Timor 形节假日 JSON（排除 Cloudflare 挑战页）。空 map 也算这种形状。</summary>
    private static bool IsValidHolidayJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var trimmed = json.TrimStart();
        return trimmed.StartsWith('{')
               && trimmed.Contains("\"holiday\"", StringComparison.Ordinal)
               && !trimmed.Contains("Just a moment", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// code=0 且 holiday 为空对象表示接口成功，但这一年还没有放假安排。不能当成功数据保存。
    /// </summary>
    private static HolidayJsonKind ClassifyHolidayJson(string? json)
    {
        if (!IsValidHolidayJson(json))
            return HolidayJsonKind.Invalid;

        var response = JsonSerializer.Deserialize(json!, HolidayJsonContext.Default.ResponseDictionaryStringHolidayItem);
        if (response?.Code != 0 || response.Holiday is null)
            return HolidayJsonKind.Invalid;

        return response.Holiday.Count == 0 ? HolidayJsonKind.Empty : HolidayJsonKind.Ready;
    }

    /// <summary>
    /// 用带浏览器 UA 的 curl.exe 下载。裸 curl 无 -A 常被 Cloudflare 挑战；
    /// 写入临时文件再按 UTF-8 读取，避免控制台管道编码导致中文乱码。
    /// </summary>
    private static async Task<string> DownloadViaCurlAsync(string url, CancellationToken cancellationToken)
    {
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

    /// <summary>指定日期是否工作日；true 表示需要上班。</summary>
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

    /// <summary>获取指定日期之后的下一个工作日（不含当天）。跨年时会自动加载下一年数据。</summary>
    public async Task<DateTime> GetNextWorkDayAsync(DateTime date)
    {
        var next = date.Date.AddDays(1);
        while (!await IsWorkDayAsync(next))
        {
            next = next.AddDays(1);
        }
        return next;
    }

    /// <summary>
    /// 获取下一个工作日，并判断相对基准日期是否跨月。
    /// </summary>
    /// <returns>
    /// nextWorkDay: 下一个工作日；
    /// crossMonth: 是否跨月（月份或年份不同）。
    /// </returns>
    public async Task<(DateTime nextWorkDay, bool crossMonth)> GetNextWorkDayWithCrossMonthAsync(DateTime date)
    {
        var nextWorkDay = await GetNextWorkDayAsync(date);
        var crossMonth = nextWorkDay.Year != date.Year || nextWorkDay.Month != date.Month;
        return (nextWorkDay, crossMonth);
    }

    /// <summary>是否为本周最后一个工作日（今天是工作日，且下一工作日跨周；周一为一周起点）。</summary>
    public async Task<bool> IsLastWorkDayOfWeekAsync(DateTime date)
    {
        date = date.Date;
        if (!await IsWorkDayAsync(date)) return false;

        var next = await GetNextWorkDayAsync(date);
        return StartOfWeekMonday(next) != StartOfWeekMonday(date);
    }

    /// <summary>以周一为起点的本周起始日。</summary>
    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }

    /// <summary>是否为本月最后一个工作日（今天是工作日，且下一工作日跨月）。</summary>
    public async Task<bool> IsLastWorkDayOfMonthAsync(DateTime date)
    {
        date = date.Date;
        if (!await IsWorkDayAsync(date)) return false;
        var (_, crossMonth) = await GetNextWorkDayWithCrossMonthAsync(date);
        return crossMonth;
    }

    /// <summary>无节假日数据时的兜底：周一到周五视为工作日。</summary>
    private static bool IsNormalWeekday(DateTime date)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public IReadOnlyList<int> ListStoredYears()
    {
        if (!Directory.Exists(_holidayDirectory))
            return [];

        var years = new List<int>();
        foreach (var file in Directory.EnumerateFiles(_holidayDirectory, "holiday*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("holiday", StringComparison.Ordinal)
                && int.TryParse(name["holiday".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var year))
                years.Add(year);
        }

        years.Sort();
        return years;
    }

    /// <summary>返回该年例外日的副本。没有本地文件时为空。</summary>
    public IReadOnlyList<HolidayItem> CopyYear(int year)
    {
        var map = GetOrLoadMap(year);
        if (map is null)
            return [];

        return map.Values.Select(CloneItem).OrderBy(item => item.Date, StringComparer.Ordinal).ToList();
    }

    /// <summary>写入该年文件，并替换内存中的日历，工作日判断立即使用新数据。</summary>
    public void SaveYear(int year, IReadOnlyList<HolidayItem> items)
    {
        var map = new Dictionary<string, HolidayItem>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!DateTime.TryParseExact(item.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date.Year != year)
                throw new InvalidOperationException($"Invalid holiday date: {item.Date}");

            map[date.ToString("MM-dd", CultureInfo.InvariantCulture)] = CloneItem(item);
        }

        var response = new Response<Dictionary<string, HolidayItem>>
        {
            Code = 0,
            Holiday = map
        };
        var json = JsonSerializer.Serialize(response, HolidayJsonContext.Default.ResponseDictionaryStringHolidayItem);
        Directory.CreateDirectory(_holidayDirectory);
        File.WriteAllText(GetHolidayFile(year), json);
        _cache[year] = map;
    }

    /// <summary>手动下载指定年份并覆盖本地文件。自动逻辑只在文件不存在时下载。</summary>
    public async Task FetchYearAsync(int year, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await DownloadYearJsonAsync(year, cancellationToken);
            Directory.CreateDirectory(_holidayDirectory);
            await File.WriteAllTextAsync(GetHolidayFile(year), json, cancellationToken);
            _cache.TryRemove(year, out _);
            GetOrLoadMap(year);
        }
        catch (HolidayNotPublishedException)
        {
            // 之前误把空 map 存下来时，清掉这个空文件，年份列表不再把它当成已获取。
            RemoveEmptyHolidayFile(year);
            throw;
        }
    }

    private void RemoveEmptyHolidayFile(int year)
    {
        var file = GetHolidayFile(year);
        if (!File.Exists(file))
            return;

        var json = File.ReadAllText(file);
        if (ClassifyHolidayJson(json) != HolidayJsonKind.Empty)
            return;

        File.Delete(file);
        _cache.TryRemove(year, out _);
    }

    private Dictionary<string, HolidayItem>? GetOrLoadMap(int year)
    {
        if (_cache.TryGetValue(year, out var cached))
            return cached;

        var file = GetHolidayFile(year);
        if (!File.Exists(file))
            return null;

        var json = File.ReadAllText(file);
        var response = JsonSerializer.Deserialize(json, HolidayJsonContext.Default.ResponseDictionaryStringHolidayItem);
        if (response?.Code != 0 || response.Holiday is null)
            throw new InvalidOperationException($"Invalid holiday file: {file}");

        _cache[year] = response.Holiday;
        return response.Holiday;
    }

    private static HolidayItem CloneItem(HolidayItem item) => new()
    {
        Holiday = item.Holiday,
        Name = item.Name,
        Wage = item.Wage,
        Date = item.Date,
        Rest = item.Rest,
        After = item.After,
        Target = item.Target
    };

    private string GetHolidayFile(int year) => Path.Combine(_holidayDirectory, $"holiday{year}.json");

    private enum HolidayJsonKind
    {
        Invalid,
        Empty,
        Ready
    }
}

/// <summary>接口调用成功，但该年放假安排还没有公布。</summary>
public sealed class HolidayNotPublishedException : InvalidOperationException
{
    public HolidayNotPublishedException(int year)
        : base($"Holiday schedule for {year} has not been published.")
    {
        Year = year;
    }

    public int Year { get; }
}
