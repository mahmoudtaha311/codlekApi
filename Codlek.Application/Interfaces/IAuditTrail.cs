using Codlek.Application.Abstractions;

namespace Codlek.Application.Interfaces;

/// <summary>
/// سجل الإجراءات الإدارية.
///
/// <para>⚠️ <b>بيضيف الصف ومابيحفظش.</b> الحفظ مسؤولية
/// <see cref="IUnitOfWork"/> — عشان السجل والتغيير نفسه ينزلوا في
/// حفظة واحدة. لو كل واحد حفظ لوحده، وقوع القاعدة بينهم بيدّي تغيير
/// من غير سجل، أو سجل لتغيير مااتمش.</para>
/// </summary>
public interface IAuditTrail
{
    void Record(
        string action,
        string entityType,
        Guid? entityId,
        string entityCode,
        string summary);

    /// <summary>
    /// سطر سجل فاعله <b>محطة فحص</b>.
    ///
    /// <para>🔴 <b>ودالة منفصلة عن قصد.</b> الدالة اللي فوق بتقرا
    /// الشركة والفاعل من <c>ICurrentUser</c> — وطلب الراكة مالوش
    /// توكن، و<c>ICurrentUser.TenantId</c> <b>بترمي</b> لو اتندهت
    /// عليه. فمعالج راكة بينده الدالة الغلط بيقع بـ<c>500</c> على
    /// مسار المزامنة، والراكة بتعيد المحاولة للأبد.</para>
    ///
    /// <para>⚠️ <b>والشركة بتيجي من المحطة المتحققة</b>
    /// (<see cref="RackAuditActor.TenantId"/>) — مكتوبة في التوقيع
    /// مش متروكة للنية.</para>
    /// </summary>
    /// <param name="dataJson">
    /// ⚠️ تفاصيل إضافية للمراجعة (الجسم المرفوع مثلاً). فاضي = مفيش
    /// — <b>مش</b> <c>null</c>، العمود مابيقبلهاش.
    /// </param>
    void RecordForRack(
        RackAuditActor actor,
        string action,
        string entityType,
        Guid? entityId,
        string entityCode,
        string summary,
        string dataJson = "");
}
