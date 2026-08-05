namespace Acclaro.Platform.Application.Notifications;

public interface INotifier
{
    /// <summary>
    /// 通知发送
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task NotifyAsync(string title, string message, CancellationToken token = default);
}
