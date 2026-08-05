using System;
using System.Collections.Generic;
using System.Text;
using Acclaro.Platform.Application.Apis.Teams.Dtos;
using WebApiClientCore.Attributes;

namespace Acclaro.Platform.Application.Apis.Teams;

/// <summary>
/// Teams 工作流
/// </summary>
#if DEBUG
[LoggingFilter]
#endif
public interface IWorkflowsApi
{
    /// <summary>
    /// 工作流消息发送
    /// </summary>
    /// <param name="json"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    [HttpPost("powerautomate/automations/direct/cu/29/workflows/6b39c232f0024e47a8681f37f0b404bf/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=Pgag80ytnzzWRvaJFXQlxrU1c1Y-yF2EGY1hFZVtdI0")]
    Task<int> SendInvokeAsync([RawJsonContent] string json, CancellationToken token = default);
}
