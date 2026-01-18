using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.BatchService;

public interface IAiBatchService
{
    Task UpsertAndExtendWindowAsync(
        string chatId,
        string? cleanText,
        List<string> imageUrls,
        string lastMessageId,
        string user);

    Task<List<AiPendingBatch>> TakeDueBatchesAsync(int max);
    Task<bool> TryMarkProcessingAsync(int id);
    Task MarkDoneAsync(int id);
    Task MarkErrorAsync(int id, string error);
    Task CancelOpenBatchAsync(string chatId, string user);

    Task<AiPendingBatch> GetBatchByIdAsync(int id);
}
