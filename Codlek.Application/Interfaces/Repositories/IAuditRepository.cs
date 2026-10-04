using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// سجل المراجعة — <b>قراية وبس</b>.
///
/// <para>🔴 <b>مفيش دالة تعديل ولا مسح هنا ولا هتبقى فيه.</b> سجل
/// المراجعة اللي بيتعدّل مش سجل مراجعة. والكتابة بتحصل من
/// <c>IAuditTrail</c> مع كل إجراء، مش من هنا.</para>
/// </summary>
public interface IAuditRepository
{
    /// <summary>صفحة من السجل + العدد الكلي قبل التصفيح.</summary>
    Task<(IReadOnlyList<AuditEvent> Rows, int TotalItems)> SearchAsync(
        Guid tenantId, AuditFilter filter, CancellationToken ct = default);

    /// <summary>
    /// نفس الفلتر ونفس الترتيب، <b>بلا تصفيح</b> — للتصدير.
    /// </summary>
    /// <param name="cap">
    /// 🔴 <b>سقف الصفوف — والمستودع بيجيب <c>cap + 1</c>.</b>
    ///
    /// <para>الصف الزيادة هو اللي بيخلّي المنادي يعرف إن فيه قص
    /// ويقوله <b>جوّه الملف</b>. ملف مقصوص في صمت بيتقري على إنه كل
    /// البيانات — والمدير بيبني عليه قرار جرد.</para>
    /// </param>
    Task<IReadOnlyList<AuditEvent>> ExportAsync(
        Guid tenantId, AuditFilter filter, int cap, CancellationToken ct = default);

    /// <summary>
    /// القيم الموجودة فعلاً في السجل — <b>للفلاتر</b>.
    ///
    /// <para>⚠️ قايمة ثابتة بكل الأكواد كانت هتدّي فلاتر بتطلّع صفر
    /// دايماً، والمستخدم بيفتكر إن السجل ناقص.</para>
    /// </summary>
    Task<(IReadOnlyList<string> Actions,
          IReadOnlyList<string> EntityTypes,
          IReadOnlyList<string> Actors)>
        DistinctValuesAsync(Guid tenantId, int maxActors, CancellationToken ct = default);
}
