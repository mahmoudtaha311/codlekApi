using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="IMaintenanceRepository"/>
public sealed class MaintenanceRepository(AppDbContext db) : IMaintenanceRepository
{
    /// <summary>
    /// ⚠️ <b>بالحرف زي القديم</b> — مكتوبة كده في <c>SearchTextBackfill</c>
    /// وفي <c>CommercialModelEvidence.TrustedSources</c>.
    /// </summary>
    private const string SystemFamily = "SystemFamily";

    // ── الشركات ────────────────────────────────────────────────────

    public Task<bool> AnyTenantAsync(CancellationToken ct = default) =>
        db.Tenants.AnyAsync(ct);

    public void AddTenant(Tenant tenant) => db.Tenants.Add(tenant);

    public async Task<IReadOnlyList<Guid>> TenantIdsAsync(CancellationToken ct = default) =>
        await db.Tenants.AsNoTracking()
            .OrderBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(ct);

    // ── جهات التسليم ───────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> LocationCodesAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Locations.AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.Code)
            .ToListAsync(ct);

    public void AddLocation(Location location) => db.Locations.Add(location);

    // ── أسامي أكواد المصنّع ─────────────────────────────────────────

    public async Task<IReadOnlyList<CommercialNameRow>> SystemFamilyDeviceNamesAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId
                     && d.CommercialModelSource == SystemFamily
                     && d.CommercialModelName != null
                     && d.Id.CompareTo(after) > 0)
            .OrderBy(d => d.Id)
            .Take(take)
            .Select(d => new CommercialNameRow(d.Id, d.CommercialModelName))
            .ToListAsync(ct);

    public Task<int> ClearDeviceCommercialModelAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default) =>
        deviceIds.Count == 0
            ? Task.FromResult(0)
            : db.Devices
                .Where(d => d.TenantId == tenantId
                         && deviceIds.Contains(d.Id)
                         && d.CommercialModelSource == SystemFamily)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.CommercialModelName, (string?)null)
                    .SetProperty(d => d.CommercialModelSource, (string?)null), ct);

    public async Task<IReadOnlyList<CommercialNameRow>> SystemFamilyReportNamesAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default) =>
        await db.Reports.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                     && r.CommercialModelSource == SystemFamily
                     && r.CommercialModelName != null
                     && r.Id.CompareTo(after) > 0)
            .OrderBy(r => r.Id)
            .Take(take)
            .Select(r => new CommercialNameRow(r.Id, r.CommercialModelName))
            .ToListAsync(ct);

    public Task<int> ClearReportCommercialModelAsync(
        Guid tenantId, IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default) =>
        reportIds.Count == 0
            ? Task.FromResult(0)
            : db.Reports
                .Where(r => r.TenantId == tenantId
                         && reportIds.Contains(r.Id)
                         && r.CommercialModelSource == SystemFamily)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.CommercialModelName, (string?)null)
                    .SetProperty(r => r.CommercialModelSource, (string?)null), ct);

    // ── الفحوص اليتيمة ──────────────────────────────────────────────

    public async Task<IReadOnlyList<OrphanReportRow>> OrphanReportsAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default) =>
        await db.Reports.AsNoTracking()

            // ⚠️ الممسوح كمان، زي القديم بالحرف — الفلتر هناك
            //    «مفيش جهاز» وبس.
            .Where(r => r.TenantId == tenantId
                     && r.DeviceId == null
                     && r.Id.CompareTo(after) > 0)
            .OrderBy(r => r.Id)
            .Take(take)
            .Select(r => new OrphanReportRow(r.Id, r.RawJson, r.NeedsDeviceResolution))
            .ToListAsync(ct);

    public Task<int> LinkReportsAsync(
        Guid tenantId, Guid deviceId, IReadOnlyCollection<Guid> reportIds,
        CancellationToken ct = default) =>
        reportIds.Count == 0
            ? Task.FromResult(0)
            : db.Reports
                .Where(r => r.TenantId == tenantId
                         && reportIds.Contains(r.Id)

                         // 🔴 الراكة ممكن تكون ربطته بين القراية والكتابة —
                         //    ربطها أحدث وأصح من اللفة.
                         && r.DeviceId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.DeviceId, (Guid?)deviceId)
                    .SetProperty(r => r.NeedsDeviceResolution, false), ct);

    public Task<int> FlagReportsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default) =>
        reportIds.Count == 0
            ? Task.FromResult(0)
            : db.Reports
                .Where(r => r.TenantId == tenantId
                         && reportIds.Contains(r.Id)
                         && r.DeviceId == null
                         && !r.NeedsDeviceResolution)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.NeedsDeviceResolution, true), ct);

    // ── «مشكوك إنه مكرر» ────────────────────────────────────────────

    public async Task<HashSet<Guid>> DuplicateSuspectsAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var live = db.DeviceIdentifiers.AsNoTracking()
            .Where(i => i.TenantId == tenantId
                     && i.IsActive

                     // 🔴 المدموجين برّه الحساب كله — مراسيهم بتفضل نشطة
                     //    عن قصد، ومن غير ده الكانوني بيتعلّم في كل إقلاع.
                     && i.Device!.Status != DeviceLifecycleStatus.Merged);

        // ⚠️ استعلام فرعي مش قايمة في الذاكرة — بيتنفّذ كله على السيرفر،
        //    فمفيش آلاف القيم بتتبعت كمعاملات.
        var shared = live
            .GroupBy(i => new { i.Kind, i.NormalizedValue })
            .Where(g => g.Select(i => i.DeviceId).Distinct().Count() > 1)
            .Select(g => g.Key.NormalizedValue);

        /*
          ⚠️ **التانية بالقيمة بس — من غير النوع. وده القديم بالحرف.**

          المشترك بيتحسب بـ(النوع، القيمة)، بس الجهاز المشكوك فيه هو أي
          جهاز شايل **القيمة** دي بأي نوع. ومعلّم المزامنة
          (`TwinsByAnchorsAsync`) بيقارن بالقيمة بس كمان.
        */
        var ids = await live
            .Where(i => shared.Contains(i.NormalizedValue))
            .Select(i => i.DeviceId)
            .Distinct()
            .ToListAsync(ct);

        return [.. ids];
    }

    public async Task<IReadOnlyList<DeviceStatusRow>> ReviewableDevicesAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId
                     && (d.Status == DeviceLifecycleStatus.Active
                      || d.Status == DeviceLifecycleStatus.DuplicateSuspected)
                     && d.Id.CompareTo(after) > 0)
            .OrderBy(d => d.Id)
            .Take(take)
            .Select(d => new DeviceStatusRow(d.Id, d.PublicCode, d.Status))
            .ToListAsync(ct);

    public Task<int> MoveStatusAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds,
        DeviceLifecycleStatus from, DeviceLifecycleStatus to,
        CancellationToken ct = default) =>
        deviceIds.Count == 0
            ? Task.FromResult(0)
            : db.Devices
                .Where(d => d.TenantId == tenantId
                         && deviceIds.Contains(d.Id)

                         // 🔴 جهاز اتدمج أو اتقاعد بين القراية والكتابة
                         //    مايرجعش «شغّال».
                         && d.Status == from)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, to), ct);

    // ── الاسم التجاري ───────────────────────────────────────────────

    public async Task<IReadOnlyList<DeviceModelRow>> DeviceModelsAsync(
        Guid tenantId, Guid after, int take, CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Id.CompareTo(after) > 0)
            .OrderBy(d => d.Id)
            .Take(take)
            .Select(d => new DeviceModelRow(
                d.Id, d.PublicCode,
                d.CommercialModelName, d.CommercialModelSource, d.MachineType))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ModelEvidenceRow>>> ModelEvidenceAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default)
    {
        if (deviceIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<ModelEvidenceRow>>();

        var rows = await db.Reports.AsNoTracking()

            // ⚠️ نفس فلتر الاستقبال (`ReportIngestRepository.ModelEvidenceAsync`)
            //    بالحرف — ومعاه الشركة.
            .Where(r => r.TenantId == tenantId
                     && r.DeviceId != null
                     && deviceIds.Contains(r.DeviceId.Value)
                     && !r.IsDeleted
                     && r.CommercialModelName != null
                     && r.CommercialModelName != "")

            // ⚠️ ونفس الترتيب — الأحدث الأول، وفاصل التعادل بالمعرّف.
            .OrderByDescending(r => r.StartedAtUtc)
            .ThenBy(r => r.Id)

            .Select(r => new
            {
                DeviceId = r.DeviceId!.Value,
                Row = new ModelEvidenceRow
                {
                    CommercialModelName = r.CommercialModelName,
                    CommercialModelSource = r.CommercialModelSource,
                    MachineType = r.MachineType,
                    StartedAtUtc = r.StartedAtUtc,
                },
            })
            .ToListAsync(ct);

        // ⚠️ `GroupBy` في الذاكرة بيحافظ على ترتيب الصفوف جوّه كل مجموعة.
        return rows
            .GroupBy(r => r.DeviceId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ModelEvidenceRow>)[.. g.Select(r => r.Row)]);
    }

    public Task<int> SetCommercialModelAsync(
        Guid tenantId, Guid deviceId, string? name, string? source, string? machineType,
        CancellationToken ct = default) =>
        db.Devices
            .Where(d => d.TenantId == tenantId && d.Id == deviceId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.CommercialModelName, name)
                .SetProperty(d => d.CommercialModelSource, source)
                .SetProperty(d => d.MachineType, machineType), ct);
}
