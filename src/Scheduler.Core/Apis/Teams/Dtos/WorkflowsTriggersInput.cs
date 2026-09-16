using System.Text.Json.Serialization;

namespace Scheduler.Core.Apis.Teams.Dtos;

public class WorkflowsTriggersInput
{
    //[JsonPropertyName("api-version")]
    //public int ApiVersion { get; set; } = 1;

    //public string sp { get; set; } = "%2Ftriggers%2Fmanual%2Frun";

    //public string sv { get; set; } = "1.0";

    //public string sig { get; set; } = "Pgag80ytnzzWRvaJFXQlxrU1c1Y-yF2EGY1hFZVtdI0";

    public WorkflowsTriggersInput(string text)
    {
        Body =
        [
            new BodyItem(text)
        ];
    }

    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://adaptivecards.io/schemas/adaptive-card.json";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "AdaptiveCard";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.4";

    /// <summary>消息主体</summary>
    [JsonPropertyName("body")]
    public List<BodyItem> Body { get; set; }

}

public class BodyItem
{
    public BodyItem(string text)
    {
        Text = text;
    }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "TextBlock";

    [JsonPropertyName("wrap")]
    public bool Wrap { get; set; } = true;

    /// <summary>动态消息</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } 
}