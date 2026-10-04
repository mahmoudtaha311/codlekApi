using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="ISyncBatchRepository"/>
public sealed class SyncBatchRepository(AppDbContext db) : ISyncBatchRepository
{
    public Task<string?> FindResponseJsonAsync(
        Guid rackId, Guid batchId, CancellationToken ct = default) =>
        db.SyncBatches.AsNoTracking()
            .Where(b => b.RackId == rackId && b.BatchId == batchId)
            .Select(b => b.ResponseJson)
            .FirstOrDefaultAsync(ct);

    public void Add(SyncBatch batch) => db.SyncBatches.Add(batch);
}
