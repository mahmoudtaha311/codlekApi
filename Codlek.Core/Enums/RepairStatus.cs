// 🔴 رقم مجمّد — الراكة بتقراه كرقم على السلك. اقرا Enums/README.md قبل أي تعديل.

namespace Codlek.Core.Enums;

/// <summary>
/// حالة أمر الصيانة.
///
/// <para>🔴 <b>الراكة بتقراها كأرقام، ومفيش ترتيب بينها.</b>
/// <c>Cancelled = 5</c> رقمها أكبر من <c>InProgress = 2</c>، فأي
/// مقارنة بالأكبر/الأصغر بتحسب الإلغاء «شغل اتعمل». الحواجز كلها
/// عضوية صريحة.</para>
/// </summary>
public enum RepairStatus
{
    New = 0,
    WaitingForRepair = 1,
    InProgress = 2,
    Completed = 3,
    UnableToRepair = 4,
    Cancelled = 5
}
