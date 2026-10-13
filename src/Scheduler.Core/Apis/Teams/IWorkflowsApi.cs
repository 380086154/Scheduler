using WebApiClientCore.Attributes;

namespace Scheduler.Core.Apis.Teams;

/// <summary>Teams Webhook。HttpHost 应为完整 Webhook 地址。</summary>
#if DEBUG
[LoggingFilter]
#endif
public interface IWorkflowsApi
{
    [HttpPost("")]
    Task<int> SendInvokeAsync([RawJsonContent] string json, CancellationToken token = default);
}
