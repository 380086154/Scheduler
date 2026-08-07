namespace Acclaro.Platform.Application.Notifications;

public interface INotifier
{
    /// <summary>
    /// 发送通知。正文以 <paramref name="message"/> 为准；
    /// <paramref name="title"/> 仅作配置/日志提示，不拼进发送内容（message 为空时例外回退）。
    /// </summary>
    Task NotifyAsync(string title, string message, CancellationToken token = default);
}
