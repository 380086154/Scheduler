using WebApiClientCore.Attributes;

namespace Acclaro.Platform.Application.Apis.WeCom;

/// <summary>企业微信群机器人 Webhook。</summary>
#if DEBUG
[LoggingFilter]
#endif
public interface IWeComApi
{
    /// <summary>发送消息。key 来自 webhook URL 查询参数。</summary>
    [HttpPost("cgi-bin/webhook/send?key={key}")]
    Task<string> SendAsync(string key, [RawJsonContent] string json, CancellationToken token = default);
}
