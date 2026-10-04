using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// سجل الدفعات اللي اتستقبلت — <b>عدم التكرار</b>.
/// </summary>
public interface ISyncBatchRepository
{
    /// <summary>
    /// الرد المخزّن لدفعة اتستقبلت قبل كده — أو <c>null</c> لو مفيش.
    ///
    /// <para>⚠️ <b>بالمحطة والدفعة — مش بالشركة.</b> معرّف المحطة فريد
    /// على النظام كله، والقيد الفريد على الجدول نفسه
    /// <c>(RackId, BatchId)</c>.</para>
    /// </summary>
    Task<string?> FindResponseJsonAsync(
        Guid rackId, Guid batchId, CancellationToken ct = default);

    void Add(SyncBatch batch);
}
