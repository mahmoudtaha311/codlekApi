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
