using Microsoft.Extensions.Hosting;

namespace Scheduler.App;

/// <summary>
/// WPF 自己管理进程生命周期。默认 ConsoleLifetime 在 VS F5 / 无控制台时
/// 可能把 Host 停掉，表现为一点托盘或任务栏就退出。
/// </summary>
internal sealed class WpfHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
