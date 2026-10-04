namespace Codlek.Core.Entities;

/// <summary>
/// دفعة مزامنة اتستقبلت — <b>سجل عدم التكرار</b>.
///
/// <para><b>ليه ده مش زيادة على مطابقة معرّف الفحص.</b> الحالة الكلاسيكية:
/// السيرفر استقبل الدفعة وكتبها، وبعدين الرد ضاع في الشبكة. الراكة مش
/// عارفة إن الشغل وصل، فبتعيد. مطابقة معرّف الفحص بتمنع تكرار
/// <b>الصفوف</b> — بس مش بتعرف تقول للراكة <b>أنهي صفوف في طابورها</b>
/// اتقبلت، فالطابور بيفضل عالق.</para>
///
/// <para>بتخزين الرد نفسه، إعادة إرسال نفس الدفعة بترجّع نفس النتيجة
/// بالظبط — والراكة بتقفل صفوفها وتكمّل.</para>
/// </summary>
public class SyncBatch
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid RackId { get; set; }

    /// <summary>معرّف الدفعة اللي الراكة ولّدته.</summary>
    public Guid BatchId { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }

    /// <summary>الرد كامل — بيترجّع زي ما هو لو الدفعة اتعادت.</summary>
    public string ResponseJson { get; set; } = "";
}
