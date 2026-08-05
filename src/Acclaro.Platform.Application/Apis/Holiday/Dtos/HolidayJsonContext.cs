using System.Text.Json.Serialization;

namespace Acclaro.Platform.Application.Apis.Holiday.Dtos;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Response<Dictionary<string, HolidayItem>>))]
internal partial class HolidayJsonContext : JsonSerializerContext;
