namespace Codlek.Application.Interfaces;

/// <summary>
/// الحفظ والمعاملات.
///
/// <para>⚠️ <b>مفيش مستودع واحد جوّاها.</b> الشكل المعتاد إن
/// <c>IUnitOfWork</c> يبقى فيها خاصية لكل مستودع — وفي المشروع
/// المرجعي بقت ٢٣ خاصية في واجهة واحدة. كل Handler بياخد المستودع
/// اللي محتاجه من الحاوية مباشرة، والواجهة دي للحفظ وبس.</para>
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// معاملة ذرّية.
    ///
    /// <para>🔴 <b>متنادهاش <c>BeginTransaction</c> بنفسك.</b> القاعدة
    /// متظبّطة بـ<c>EnableRetryOnFailure</c> (القاعدة المُدارة بتنام)،
    /// وEF بترمي على أي معاملة يدوية مع الإعاد ده. الدالة دي بتعدّي
    /// على <c>CreateExecutionStrategy</c> الصح.</para>
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
