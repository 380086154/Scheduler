using System.Text.Json.Serialization;

namespace Acclaro.Platform.Application.Apis.Holiday.Dtos;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Response<Dictionary<string, HolidayItem>>))]
[JsonSerializable(typeof(ApiHubsResponse))]
internal partial class HolidayJsonContext : JsonSerializerContext;
