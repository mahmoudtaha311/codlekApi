namespace Codlek.Application.Interfaces;

/// <summary>
/// الأرقام المتسلسلة لكل شركة.
///
/// <para>🔴 <b>مش <c>MAX(code)+1</c> ومش قراءة-ثم-كتابة.</b> الاتنين
/// بيتسابقوا: راكتين بتسجّلوا في نفس اللحظة بياخدوا نفس الرقم،
/// وساعتها الفهرس الفريد بيرفض واحدة منهم بخطأ مالوش أي معنى عند
/// المستخدم.</para>
/// </summary>
public interface ITenantCounters
{
    /// <summary>
    /// الرقم الجايّ، والعدّاد بيزيد — <b>في جملة واحدة</b>.
    /// </summary>
    /// <param name="counterName">
    /// ⚠️ <b>الاسم ده مفتاح مخزّن.</b> تغييره بيرجّع الترقيم لواحد
    /// <b>ويكرّر أكواد موجودة مطبوعة على ورق</b>. راجع
    /// <c>RepairCode.CounterName</c>.
    /// </param>
    Task<int> NextAsync(Guid tenantId, string counterName, CancellationToken ct = default);
}
