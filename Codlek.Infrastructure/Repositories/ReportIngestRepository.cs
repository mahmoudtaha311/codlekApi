using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="IReportIngestRepository"/>
public sealed class ReportIngestRepository(AppDbContext db) : IReportIngestRepository
{
    public Task<Dictionary<Guid, Report>> ExistingAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        db.Reports
            .Include(r => r.Steps)
            .Include(r => r.Parts)
            .Include(r => r.Edits)
            .Include(r => r.SnapshotComponents)

            // 🔴 أربع مجموعات في استعلام واحد بتتضرب في بعض — ٥٠٠ فحص
            //    بيطلّعوا عشرات الآلاف من الصفوف المكررة على الشبكة.
            .AsSplitQuery()

            .Where(r => r.TenantId == tenantId && ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

    public async Task<HashSet<Guid>> KnownDeviceIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        var found = await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && ids.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(ct);

        return [.. found];
    }

    public async Task<IReadOnlyList<TechnicianIdentityRow>> TechnicianIdentitiesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        /*
          🔴 **مفيش ترشيح بالشركة هنا — وده مقصود.**

          من غيره «فني شركة تانية» و«فني مش موجود» بيبقوا نفس
          الحالة، والأولى لازم **ترفض** والتانية لازم **تعدّي**.
        */
        return await db.Technicians.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new TechnicianIdentityRow
            {
                Id = t.Id,
                TenantId = t.TenantId,
                Code = t.Code,
            })
            .ToListAsync(ct);
    }

    public async Task<Guid?> CanonicalForAliasAsync(
        Guid tenantId, Guid aliasDeviceId, CancellationToken ct = default)
    {
        var canonical = await db.DeviceAliases.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.AliasDeviceId == aliasDeviceId)
            .Select(a => a.CanonicalDeviceId)
            .FirstOrDefaultAsync(ct);

        // ⚠️ `Guid.Empty` هي «مفيش صف» من `FirstOrDefaultAsync` —
        //    بنحوّلها `null` عشان المنادي مايقارنش بقيمة سحرية.
        return canonical == Guid.Empty ? null : canonical;
    }

    public async Task<IReadOnlyList<Guid>> DevicesByIdentifierAsync(
        Guid tenantId, DeviceIdentifierKind kind, string normalizedValue,
        CancellationToken ct = default) =>
        await db.DeviceIdentifiers.AsNoTracking()
            .Where(i => i.TenantId == tenantId
                     && i.Kind == kind
                     && i.NormalizedValue == normalizedValue
                     && i.IsActive)
            .Select(i => i.DeviceId)
            .Distinct()
            .Take(DeviceIdentityStrength.ProbeTake)
            .ToListAsync(ct);

    public void Add(Report report) => db.Reports.Add(report);

    public void RemoveChildren(Report report)
    {
        db.Steps.RemoveRange(report.Steps);
        db.Parts.RemoveRange(report.Parts);
        db.Edits.RemoveRange(report.Edits);
        db.SnapshotComponents.RemoveRange(report.SnapshotComponents);
    }

    /// <summary>
    /// 🔴 <b>متتبّعة عن قصد</b> — المُرطِّب وعلامة «قطعة اتغيّرت»
    /// بيكتبوا على الصفوف دي.
    /// </summary>
    public async Task<IReadOnlyList<Device>> DevicesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0
            ? []
            : await db.Devices
                .Where(d => d.TenantId == tenantId && ids.Contains(d.Id))
                .ToListAsync(ct);

    public async Task<IReadOnlyList<ModelEvidenceRow>> ModelEvidenceAsync(
        Guid deviceId, CancellationToken ct = default) =>
        await db.Reports.AsNoTracking()
            .Where(r => r.DeviceId == deviceId
                     && !r.IsDeleted
                     && r.CommercialModelName != null
                     && r.CommercialModelName != "")

            // ⚠️ الأحدث الأول — والمنادي بياخد أول واحد **بمصدر
            //    موثوق**، مش أول واحد وخلاص.
            .OrderByDescending(r => r.StartedAtUtc)
            .ThenBy(r => r.Id)

            .Select(r => new ModelEvidenceRow
            {
                CommercialModelName = r.CommercialModelName,
                CommercialModelSource = r.CommercialModelSource,
                MachineType = r.MachineType,
                StartedAtUtc = r.StartedAtUtc,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReportCursorRow>> ReportCursorsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds,
        CancellationToken ct = default) =>
        deviceIds.Count == 0
            ? []
            : await db.Reports.AsNoTracking()
                .Where(r => r.TenantId == tenantId
                         && !r.IsDeleted
                         && r.DeviceId != null
                         && deviceIds.Contains(r.DeviceId.Value))

                // ⚠️ نفس ترتيب القديم بالحرف — الأحدث الأول، وفاصل
                //    التعادل بالمعرّف عشان فحصين في نفس الثانية
                //    مايتقلبوش بين الطلبات.
                .OrderByDescending(r => r.StartedAtUtc)
                .ThenBy(r => r.Id)

                .Select(r => new ReportCursorRow
                {
                    DeviceId = r.DeviceId!.Value,
                    ReportId = r.Id,
                    StartedAtUtc = r.StartedAtUtc,
                    SnapshotIsPartial = r.SnapshotIsPartial,
                })
                .ToListAsync(ct);

    public async Task<IReadOnlyList<ReportSnapshotComponent>> SnapshotComponentsAsync(
        IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default) =>
        reportIds.Count == 0
            ? []
            : await db.SnapshotComponents.AsNoTracking()
                .Where(c => reportIds.Contains(c.ReportId))
                .ToListAsync(ct);
}
