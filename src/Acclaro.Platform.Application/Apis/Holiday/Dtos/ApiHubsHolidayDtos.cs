using System.Text.Json.Serialization;

namespace Acclaro.Platform.Application.Apis.Holiday.Dtos;

public sealed class ApiHubsResponse
{
    public int Code { get; set; }
    public string? Msg { get; set; }
    public ApiHubsPageData? Data { get; set; }
}

public sealed class ApiHubsPageData
{
    public List<ApiHubsHolidayDay>? List { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public int Total { get; set; }
}

public sealed class ApiHubsHolidayDay
{
    /// <summary>yyyyMMdd，如 20260101</summary>
    public int Date { get; set; }

    /// <summary>1=工作日（含调休上班），2=非工作日</summary>
    public int Workday { get; set; }

    /// <summary>1=周末，2=非周末</summary>
    public int Weekend { get; set; }

    /// <summary>节假日枚举；10=非节假日</summary>
    public int Holiday { get; set; }

    /// <summary>1=法定节假日，2=非法定</summary>
    [JsonPropertyName("holiday_legal")]
    public int HolidayLegal { get; set; }

    [JsonPropertyName("holiday_cn")]
    public string? HolidayCn { get; set; }

    [JsonPropertyName("workday_cn")]
    public string? WorkdayCn { get; set; }

    [JsonPropertyName("holiday_overtime_cn")]
    public string? HolidayOvertimeCn { get; set; }
}
