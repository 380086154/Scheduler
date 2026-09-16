using Scheduler.Core.Apis.Teams;

namespace Scheduler.Core.Notifications;

/// <summary>
/// 通过 WebApiClientCore / IWorkflowsApi 发送 Teams Adaptive Card（原始 JSON）。
/// </summary>
public sealed class TeamsWebhookNotifier(IWorkflowsApi workflowsApi) : INotifier
{
    public async Task NotifyAsync(string title, string message, CancellationToken token = default)
    {
        var text = NotificationText.Body(title, message);

        var json = $$"""
            {
              "$schema": "https://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.4",
              "msteams": { "width": "Full" },
              "body": [
                { "type": "TextBlock", "text": "{{NotificationText.JsonEncode(text)}}", "wrap": true }
              ]
            }
            """;

        await workflowsApi.SendInvokeAsync(json, token);
    }
}
