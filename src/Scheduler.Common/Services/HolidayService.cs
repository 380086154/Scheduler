using Scheduler.Common;

namespace Scheduler.Common.Services;

/// <summary>
/// 节假日服务类
/// https://timor.tech/api/holiday/
/// </summary>
public sealed class HolidayService1
{
    private const string ApiUrl = "https://timor.tech/api/holiday/year/{0}";

    private readonly string _holidayDirectory;

    public HolidayService1()
    {
        _holidayDirectory = Path.Combine(AppContext.BaseDirectory, "data", "holiday");
        Directory.CreateDirectory(_holidayDirectory);
    }

    private static string GetApiUrl(int year) => string.Format(ApiUrl, year);

    private string GetHolidayFile(int year) => Path.Combine(_holidayDirectory, $"holiday{year}.json");

    public async Task EnsureHolidayFileAsync(int year)
    {
        var file = GetHolidayFile(year);
        if (File.Exists(file)) return;

        try
        {
            var json = await HttpHelper.Client.GetStringAsync(GetApiUrl(year));
            await File.WriteAllTextAsync(file, json);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task InitializeAsync(bool nextYear = false)
    {
        var year = DateTime.Today.Year;

        await EnsureHolidayFileAsync(year);

        if (nextYear && DateTime.Today.Month == 12)
        {
            await EnsureHolidayFileAsync(year + 1);
        }
    }
}
