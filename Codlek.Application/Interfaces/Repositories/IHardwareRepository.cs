using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// لقطات العتاد.
///
/// <para>🔴 <b>التقييد بالشركة لوحده مش كفاية هنا.</b> صفحة
/// المقارنة بتاخد <b>معرّفي فحص من المستخدم</b>، فلو اتقيّدوا
/// بالشركة بس، حد يقدر يقارن جهازين مختلفين بتوع نفس الشركة بمجرد
/// تغيير الأرقام في الرابط — ويطلّع فروق عتاد مالهاش أي معنى.
/// فالفحص لازم يثبت إنه بتاع <b>الجهاز ده</b> كمان؛ وده شغل
/// <see cref="FindHeaderForDeviceAsync"/>.</para>
/// </summary>
public interface IHardwareRepository
{
    /// <summary>
    /// ترويسة فحص بمعرّفه.
    ///
    /// <para>⚠️ <b>ودي مابتستبعدش الفحص الممسوح منطقياً — سلوك
    /// القديم بالحرف.</b> نقطة <c>/reports/{id}/hardware</c> بتقرا
    /// الجدول مباشرةً، بينما نقط الجهاز بتعدّي على استعلام بيفلتر
    /// <c>IsDeleted</c>. الفرق منقول زي ما هو عشان نفس الطلب يدّي
    /// نفس الرد من المشروعين وهما على نفس القاعدة.</para>
    /// </summary>
    Task<SnapshotHeaderFacts?> FindReportHeaderAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// 🔴 <b>أحدث فحص <u>ليه لقطة فعلاً</u> — مش أحدث فحص.</b>
    ///
    /// <para>فيه فحوص اترفعت من غير لقطة عتاد خالص (نسخة أقدم من
    /// الميزة، أو الفني شغّل من غير صلاحيات مسؤول). لو أخدنا أحدث
    /// فحص وخلاص، اللاب اللي آخر فحص له من غير لقطة كان هيبان
    /// «مفيش قطع» وهو عنده لقطة كاملة من الفحص اللي قبله.</para>
    /// </summary>
    Task<SnapshotHeaderFacts?> FindLatestSnapshotAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// ترويسة فحص <b>مربوط باللاب ده</b> — الحارس الحقيقي للمقارنة.
    /// </summary>
    Task<SnapshotHeaderFacts?> FindHeaderForDeviceAsync(
        Guid tenantId, Guid deviceId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// مكوّنات فحص واحد، مرتّبة بالنوع وبعده رقم النسخة.
    /// </summary>
    Task<IReadOnlyList<ReportSnapshotComponent>> ComponentsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// مراحل الفحصين في <b>قراية واحدة</b>.
    ///
    /// <para>⚠️ قرايتين منفصلتين كانوا ضعف الرحلات على قاعدة بعيدة
    /// من غير أي فايدة — المنادي بيفرّقهم بالمعرّف في الذاكرة.</para>
    /// </summary>
    Task<IReadOnlyList<StepOutcomeFacts>> StepsAsync(
        Guid tenantId, Guid leftReportId, Guid rightReportId, CancellationToken ct = default);

    /// <summary>
    /// كود الراكة — فاضي لو مفيش راكة أو الراكة اتشالت.
    /// </summary>
    Task<string> RackCodeAsync(
        Guid tenantId, Guid? rackId, CancellationToken ct = default);

    /// <summary>
    /// اللاب بكوده — <c>null</c> لو مش موجود في الشركة دي.
    /// </summary>
    Task<DeviceCodeFacts?> FindDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);
}
