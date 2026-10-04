using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class HardwareRepository(AppDbContext db) : IHardwareRepository
{
    /*
      🔴 **الإسقاط مكتوب مرة واحدة ومستعمل في التلاتة.**

      النقط التلاتة بترجّع **نفس** الترويسة. ولو اتكتب تلات مرات، أول
      عمود يتزاد في واحدة بيخلّي شاشة من التلاتة تعرض بيانات ناقصة
      من غير أي خطأ.
    */
    private static readonly System.Linq.Expressions.Expression<
        Func<Report, SnapshotHeaderFacts>> Header =
        r => new SnapshotHeaderFacts(
            r.Id,
            r.DeviceId,
            r.DeviceCode,
            r.StartedAtUtc,
            r.SnapshotCapturedAtUtc,
            r.TechnicianName,
            r.TechnicianCode,
            r.SourceRackId,
            r.SnapshotCollectorVersion,
            r.SnapshotIsPartial,
            r.SnapshotRanAsAdministrator,
            r.SnapshotWarnings,
            r.CommercialModelName ?? "");

    /// <summary>
    /// ⚠️ <b>مابتفلترش <c>IsDeleted</c> — منقول بالحرف.</b> نقطة
    /// الفحص الواحد في القديم بتقرا الجدول مباشرةً، ونقط الجهاز
    /// بتعدّي على استعلام بيفلتر. راجع التعليق على الواجهة.
    /// </summary>
    public Task<SnapshotHeaderFacts?> FindReportHeaderAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default) =>
        db.Reports.AsNoTracking()
            .Where(r => r.Id == reportId && r.TenantId == tenantId)
            .Select(Header)
            .FirstOrDefaultAsync(ct);

    public Task<SnapshotHeaderFacts?> FindLatestSnapshotAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        Scoped(tenantId)
            .Where(r => r.DeviceId == deviceId)

            // 🔴 **«ليه لقطة» = عنده صفوف مكوّنات فعلاً — شرط القديم
            // بالحرف.**
            //
            // مش `SnapshotCapturedAtUtc != null` (اللي قايمة اللقطات
            // بتستعمله): فيه فحوص بوقت لقطة ومفيش ولا صف مكوّن.
            // الشرط ده بيختار اللقطة اللي **فيها حاجة تتعرض**.
            .Where(r => r.SnapshotComponents.Any())
            .OrderByDescending(r => r.StartedAtUtc)

            // ⚠️ **الفاصل ده زيادة على القديم عن قصد.** هناك الترتيب
            // بـ`StartedAtUtc` وبس، فلو فحصين وصلوا بنفس اللحظة من
            // دفعة مزامنة، «أحدث لقطة» بتبقى أي واحدة منهم — والشاشة
            // بتعرض قطع مختلفة مع كل تحديث.
            .ThenBy(r => r.Id)
            .Select(Header)
            .FirstOrDefaultAsync(ct);

    public Task<SnapshotHeaderFacts?> FindHeaderForDeviceAsync(
        Guid tenantId, Guid deviceId, Guid reportId, CancellationToken ct = default) =>
        Scoped(tenantId)

            // 🔴 الشرطين مع بعض: الفحص ده، وبتاع اللاب ده.
            .Where(r => r.Id == reportId && r.DeviceId == deviceId)
            .Select(Header)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ReportSnapshotComponent>> ComponentsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default) =>
        await db.SnapshotComponents.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ReportId == reportId)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.InstanceIndex)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StepOutcomeFacts>> StepsAsync(
        Guid tenantId, Guid leftReportId, Guid rightReportId, CancellationToken ct = default) =>
        await db.Steps.AsNoTracking()
            .Where(s => s.ReportId == leftReportId || s.ReportId == rightReportId)

            /*
              🔴 **المراحل مالهاش <c>TenantId</c> خاص بيها.</b>

              الجدول مربوط بالفحص وبس، فالترشيح بالشركة لازم يمرّ على
              الفحص. ومن غير الشرط ده، معرّف فحص بتاع شركة تانية في
              الرابط كان بيرجّع مراحلها.
            */
            .Where(s => db.Reports.Any(r => r.Id == s.ReportId && r.TenantId == tenantId))
            .Select(s => new StepOutcomeFacts(s.ReportId, s.Title, s.Status))
            .ToListAsync(ct);

    public async Task<string> RackCodeAsync(
        Guid tenantId, Guid? rackId, CancellationToken ct = default)
    {
        if (rackId is not { } id) return "";

        // ⚠️ الراكة اللي اتشالت بترجّع كود فاضي — الفحص القديم بتاعها
        // لسه له قيمة.
        return await db.Racks.AsNoTracking()
            .Where(r => r.Id == id && r.TenantId == tenantId)
            .Select(r => r.RackCode)
            .FirstOrDefaultAsync(ct) ?? "";
    }

    public Task<DeviceCodeFacts?> FindDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices.AsNoTracking()
            .Where(d => d.Id == deviceId && d.TenantId == tenantId)
            .Select(d => new DeviceCodeFacts(d.Id, d.PublicCode))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// ⚠️ الفحص الممسوح منطقياً مش فحص موجود — فيما عدا نقطة الفحص
    /// الواحد، اللي منقولة زي ما هي.
    /// </summary>
    private IQueryable<Report> Scoped(Guid tenantId) =>
        db.Reports.AsNoTracking().Where(r => r.TenantId == tenantId && !r.IsDeleted);
}
