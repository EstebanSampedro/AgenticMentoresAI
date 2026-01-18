using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.ConversationLifecycle;

public interface IConversationLifecycleService
{
    Task<int> FinalizeInactiveConversationsAsync(CancellationToken ct);
}
