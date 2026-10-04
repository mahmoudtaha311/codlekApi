using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// الشغل التشغيلي الجايّ من الراكة — أوامر الصيانة.
///
/// <para>🔴 <b>ومفيش شركة بتتقرا من الحمولة.</b> الشركة من مفتاح الراكة
/// وبس — الحمولة ممكن تدّعي أي حاجة.</para>
/// </summary>
public interface IOperationalSyncRepository
{
    /// <summary>
    /// آخر دخول <b>ناجح</b> للفني ده <b>على المحطة دي</b>، في وقت معيّن
    /// أو قبله.
    ///
    /// <para>🔴 <b>والصف ده هو دليل التصريح الأوفلاين</b> — مسار دخول
    /// الفني بيكتبه. من غيره، الراكة كانت تقدر تكتب أي تاريخ قديم في
    /// الحمولة وتعدّي.</para>
    /// </summary>
    Task<DateTime?> LastLoginAtAsync(
        Guid tenantId, Guid rackId, Guid technicianId, DateTime atOrBeforeUtc,
        CancellationToken ct = default);

    /// <summary>
    /// الفني <b>جوّه الشركة دي</b>.
    ///
    /// <para>⚠️ <c>null</c> مش «مش موجود» — ممكن يكون موجود في شركة
    /// تانية. والرسالة مابتفرّقش عن قصد: راكة مش المفروض تعرف مين موجود
    /// برّه شركتها.</para>
    /// </summary>
    Task<Technician?> FindTechnicianAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default);

    Task<bool> ReportExistsAsync(Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>الأمر بأعطاله وقطعه — <b>متتبّع</b>.</summary>
    Task<RepairWorkItem?> FindWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken ct = default);

    void AddWorkItem(RepairWorkItem item);

    /// <summary>
    /// 🔴 <b>إضافة صريحة — مش إضافة للمجموعة وبس.</b>
    ///
    /// <para>المعرّف بيتولّد على الراكة، يعني الصف الجديد بيوصل ومعاه
    /// مفتاح مش فاضي. EF بيستعمل «المفتاح فاضي؟» كعلامة على إن الكيان
    /// جديد، فصف بمفتاح جاهز اتحط في مجموعة أب <b>موجود</b> بيتعلّم
    /// <c>Modified</c> بدل <c>Added</c> — وبيطلع UPDATE على صف مالوش
    /// وجود، فالحفظ كله بيرمي.</para>
    ///
    /// <para>⚠️ <b>والعطل ده مابيبانش في أول رفع</b>: وقتها الأب نفسه
    /// جديد فأولاده بيورثوا الحالة. بيبان في <b>إعادة</b> الرفع بس —
    /// يعني بالظبط في الحالة اللي المزامنة بتعتمد عليها.</para>
    /// </summary>
    void AddIssue(RepairWorkItemIssue issue);

    /// <inheritdoc cref="AddIssue"/>
    void AddPart(RepairPart part);
}
