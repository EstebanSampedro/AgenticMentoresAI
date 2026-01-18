using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Users;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

    public ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
        => _queue.Writer.WriteAsync(workItem);

    public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct)
        => _queue.Reader.ReadAsync(ct);
}
