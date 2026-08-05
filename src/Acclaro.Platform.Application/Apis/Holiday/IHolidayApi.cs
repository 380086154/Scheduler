using Acclaro.Platform.Application.Apis.Holiday.Dtos;
using WebApiClientCore.Attributes;

namespace Acclaro.Platform.Application.Apis.Holiday;

/// <summary>
/// 提莫的神秘小站 - 免费节假日 API
/// </summary>
//[HttpHost("https://timor.tech/")]
#if DEBUG
[LoggingFilter]
#endif
//// 写在接口上，会出现在 LoggingFilter 的 REQUEST 里；仅靠 HttpClient.DefaultRequestHeaders 时日志往往看不到
//[Header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")]
//[Header("Accept", "application/json, text/plain, */*")]
//[Header("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8")]
public interface IHolidayApi
{
    /// <summary>获取指定年份节假日安排</summary>
    [HttpGet("api/holiday/year/{year}")]
    Task<string> GetYearAsync(int year, CancellationToken token = default);

    /// <summary>获取指定月份节假日安排</summary>
    [HttpGet("api/holiday/year/{year_month}")]
    Task<Response<string>> GetMonthAsync(string year_month, CancellationToken token = default);
}
