using WebApiClientCore.Attributes;

namespace Scheduler.Core.Apis.Holiday;

/// <summary>接口坞节假日 API（Timor 备用源）。</summary>
#if DEBUG
[LoggingFilter]
#endif
public interface IApiHubsHolidayApi
{
    /// <summary>按年拉取日历（建议 size=366）。api_key 为空时走匿名额度。</summary>
    [HttpGet("holiday/get")]
    Task<string> GetAsync(
        [PathQuery] int year,
        [PathQuery] int size = 366,
        [PathQuery] int cn = 1,
        [PathQuery] int page = 1,
        [PathQuery] string? api_key = null,
        CancellationToken token = default);
}
