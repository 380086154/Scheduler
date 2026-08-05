using Acclaro.Platform.Application.Apis.Teams;

namespace Acclaro.Platform.Application.Notifications;

/// <summary>
/// 通过 WebApiClientCore / IWorkflowsApi 发送 Teams Adaptive Card（原始 JSON，兼容 AOT）。
/// </summary>
public sealed class TeamsWebhookNotifier(IWorkflowsApi workflowsApi) : INotifier
{
    public async Task NotifyAsync(string title, string message, CancellationToken token = default)
    {
        var text = string.IsNullOrWhiteSpace(title)
            ? message
            : string.IsNullOrWhiteSpace(message) ? title : $"{title}\n{message}";

        var json = $$"""
            {
              "$schema": "https://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.4",
              "msteams": { "width": "Full" },
              "body": [
                { "type": "TextBlock", "text": "{{JsonEncode(text)}}", "wrap": true }
              ]
            }
        """;

        await workflowsApi.SendInvokeAsync(json, token);
    }

    private static string JsonEncode(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);
}
