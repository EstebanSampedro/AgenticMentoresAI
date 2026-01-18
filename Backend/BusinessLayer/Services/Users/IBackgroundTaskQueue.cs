using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Users;

public interface IBackgroundTaskQueue
{
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}
