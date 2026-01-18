using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.BatchService;

public class AiBatchService : IAiBatchService
{
    private readonly DBContext _context;
    private readonly AiBatchingOptions _aiBatchingOptions;

    public AiBatchService(DBContext context, IOptions<AiBatchingOptions> aiBatchingOptions)
    {
        _context = context;
        _aiBatchingOptions = aiBatchingOptions.Value;
    }

    public async Task UpsertAndExtendWindowAsync(
        string chatId,
        string? cleanText,
        List<string> imageUrls,
        string lastMessageId,
        string user)
    {
        var now = DateTime.UtcNow;
        var extendTo = now.AddSeconds(_aiBatchingOptions.WindowSeconds);

        await using var tx = await _context.Database.BeginTransactionAsync();

        var batch = await _context.AiPendingBatches
            .SingleOrDefaultAsync(b => b.ChatId == chatId &&
                                       (b.Status == "Pending" || b.Status == "Processing"));

        if (batch is null)
        {
            batch = new AiPendingBatch
            {
                ChatId = chatId,
                WindowEndsAt = extendTo,
                Status = "Pending",
                AccumulatedText = cleanText ?? string.Empty,
                AccumulatedImages = JsonSerializer.Serialize(imageUrls ?? new()),
                LastMessageId = lastMessageId,
                CreatedAt = now,
                CreatedBy = string.IsNullOrWhiteSpace(user) ? "system" : user,
                UpdatedAt = now,
                UpdatedBy = string.IsNullOrWhiteSpace(user) ? "system" : user
            };

            _context.AiPendingBatches.Add(batch);
        }
        else
        {
            batch.WindowEndsAt = extendTo;

            if (!string.IsNullOrWhiteSpace(cleanText))
            {
                batch.AccumulatedText = string.Join("\n\n",
                    new[] { batch.AccumulatedText, cleanText }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            var imgs = JsonSerializer.Deserialize<List<string>>(batch.AccumulatedImages ?? "[]") ?? new();
            if (imageUrls != null && imageUrls.Count > 0)
            {
                imgs.AddRange(imageUrls);
                imgs = imgs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            batch.AccumulatedImages = JsonSerializer.Serialize(imgs);

            batch.LastMessageId = lastMessageId;
            batch.UpdatedAt = now;
            batch.UpdatedBy = string.IsNullOrWhiteSpace(user) ? "system" : user;
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<AiPendingBatch>> TakeDueBatchesAsync(int max)
    {
        var now = DateTime.UtcNow;
        return await _context.AiPendingBatches
            .Where(b => b.Status == "Pending" && b.WindowEndsAt <= now)
            .OrderBy(b => b.WindowEndsAt)
            .Take(max)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> TryMarkProcessingAsync(int id)
    {
        // Exclusión por Status (optimista). Sin CT.
        var affected = await _context.AiPendingBatches
            .Where(b => b.Id == id && b.Status == "Pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, "Processing")
                .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)
                .SetProperty(b => b.UpdatedBy, "system"));

        return affected == 1;
    }

    public Task MarkDoneAsync(int id) =>
        _context.AiPendingBatches
           .Where(b => b.Id == id)
           .ExecuteUpdateAsync(s => s
               .SetProperty(b => b.Status, "Done")
               .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)
               .SetProperty(b => b.UpdatedBy, "system"));

    public Task MarkErrorAsync(int id, string error) =>
        _context.AiPendingBatches
           .Where(b => b.Id == id)
           .ExecuteUpdateAsync(s => s
               .SetProperty(b => b.Status, "Error")
               .SetProperty(b => b.ErrorMessage, error)
               .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)
               .SetProperty(b => b.UpdatedBy, "system"));

    public Task CancelOpenBatchAsync(string chatId, string user) =>
        _context.AiPendingBatches
           .Where(b => b.ChatId == chatId && (b.Status == "Pending" || b.Status == "Processing"))
           .ExecuteUpdateAsync(s => s
               .SetProperty(b => b.Status, "Canceled")
               .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)
               .SetProperty(b => b.UpdatedBy, string.IsNullOrWhiteSpace(user) ? "system" : user));

    public async Task<AiPendingBatch> GetBatchByIdAsync(int id)
    {
        var batch = await _context.AiPendingBatches
                                  .FirstOrDefaultAsync(x => x.Id == id);

        if (batch == null)
            throw new Exception($"Batch {id} no encontrado.");

        return batch;
    }
}
