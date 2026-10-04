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
    /// حفظ بيستحمل <b>السباق</b> — <c>false</c> لو القاعدة رفضت الكتابة.
    ///
    /// <para>🔴 <b>وبيفضّي كل اللي متتبّع لو فشل.</b> الحالة اللي
    /// اتعملت عشانها: نفس الدفعة وصلت مرتين مع بعض (الراكة بتعيد
    /// الإرسال لما الرد يتأخر)، والاتنين عملوا نفس الصف، وواحد وقع على
    /// القيد الفريد. المنادي بيقرا اللي التاني كتبه أو بيعيد من نضيف —
    /// والكيانات اللي فشلت لو فضلت متتبّعة كانت هتتحفظ تاني وتقع تاني.</para>
    ///
    /// <para>⚠️ <b>أي رفض من القاعدة بيرجّع <c>false</c></b> — مش القيد
    /// الفريد بس. والمنادي اللي بيعيد بيحفظ بعدها بـ
    /// <see cref="SaveChangesAsync"/> العادية، فرفض حقيقي (مش سباق)
    /// بيرمي في المرة التانية.</para>
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken ct = default);

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
