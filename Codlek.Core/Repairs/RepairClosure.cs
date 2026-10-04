using Codlek.Core.Enums;

namespace Codlek.Core.Repairs;

/// <summary>
/// الأمر مقفول؟ — <b>عضوية صريحة</b>.
///
/// <para>🔴 <c>Cancelled = 5</c> أكبر من <c>InProgress = 2</c>، فأي
/// <c>&gt;=</c> هنا بيحسب غلط. الأرقام مالهاش ترتيب.</para>
///
/// <para>⚠️ <b>والرسالة مش هنا.</b> المشروع القديم بيقول «مينفعش
/// يتسند» في الإسناد و«مينفعش يتعدّل» في التعديل — نفس الشرط
/// برسالتين مختلفتين. الدالة دي بتجاوب على الشرط بس، وكل مكان نداء
/// بيختار رسالته.</para>
/// </summary>
public static class RepairClosure
{
    public static bool IsClosed(RepairStatus status) =>
        status is RepairStatus.Completed
               or RepairStatus.UnableToRepair
               or RepairStatus.Cancelled;

    /// <summary>
    /// ⚠️ عكس <see cref="IsClosed"/> بالحرف — موجودة عشان القايمة
    /// النازلة للراكة تقرا نفس التعريف بدل ما تكتب التلات حالات
    /// بإيدها.
    /// </summary>
    public static bool IsOpen(RepairStatus status) => !IsClosed(status);
}
