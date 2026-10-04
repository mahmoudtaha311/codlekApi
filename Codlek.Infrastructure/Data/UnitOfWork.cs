using Codlek.Application.Interfaces;

namespace Codlek.Infrastructure.Data;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);

    /// <summary>
    /// 🔴 <b>المعاملة بتعدّي على استراتيجية الإعادة، مش
    /// <c>BeginTransaction</c> على الناشف.</b>
    ///
    /// <para>القاعدة متظبّطة بـ<c>EnableRetryOnFailure</c> — لأن
    /// القاعدة المُدارة بتنام وأول استعلام بعدها بيرجع خطأ عابر. ومع
    /// الإعاد ده، EF <b>بترمي</b> على أي معاملة يدوية: لأن إعادة نص
    /// معاملة مالهاش معنى.</para>
    ///
    /// <para>⚠️ والاستراتيجية بتعيد <b>الكتلة كلها</b> من الأول.
    /// فاللي جوّه <c>work</c> لازم يكون قابل للتكرار: متعملش فيه
    /// حاجة برّه القاعدة (تبعت إيميل، تكتب ملف) — دي بتتعمل مرتين.</para>
    /// </summary>
    public Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            work,
            async (_, inner, token) =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(token);

                T result = await inner(token);

                await transaction.CommitAsync(token);
                return result;
            },
            verifySucceeded: null,
            cancellationToken: ct);
}
