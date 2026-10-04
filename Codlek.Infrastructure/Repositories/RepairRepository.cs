using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class RepairRepository(AppDbContext db) : IRepairRepository
{
    // =================================================================
    //  الأمر
    // =================================================================

    public Task<RepairWorkItem?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.RepairWorkItems.FirstOrDefaultAsync(
            w => w.Id == id && w.TenantId == tenantId, ct);

    public Task<RepairWorkItem?> FindWithPartsAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.RepairWorkItems
            .Include(w => w.Parts)
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId, ct);

    public void Add(RepairWorkItem item) => db.RepairWorkItems.Add(item);

    // =================================================================
    //  الجهاز
    // =================================================================

    public Task<Device?> FindDeviceAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.TenantId == tenantId, ct);

    /// <summary>
    /// ⚠️ <c>!IsDeleted</c> جزء من السؤال مش ترشيح زيادة — أمر
    /// متعلّق بفحص اتمسح معناه سلسلة مقطوعة.
    /// </summary>
    public Task<bool> ReportExistsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default) =>
        db.Reports.AnyAsync(
            r => r.Id == reportId && r.TenantId == tenantId && !r.IsDeleted, ct);

    // =================================================================
    //  الفني
    // =================================================================

    public Task<Technician?> FindTechnicianAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        db.Technicians.FirstOrDefaultAsync(
            t => t.Id == technicianId && t.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Technician>> AssignableTechniciansAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Technicians.AsNoTracking()
            // 🔴 نشط **و** يقدر يصلّح — الموقوف مايظهرش حتى لو قدرته
            // شغّالة. إسناد شغل لحد مش قادر يدخل معناه أمر بيقعد.
            .Where(t => t.TenantId == tenantId && t.IsActive && t.CanRepair)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<Guid>> TechnicianBrandsAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        await db.TechnicianBrands.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TechnicianId == technicianId)
            .Select(x => x.BrandId)
            .ToListAsync(ct);

    // =================================================================
    //  الماركات
    // =================================================================

    public async Task<IReadOnlyList<BrandRule>> BrandRulesAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        /*
          ⚠️ **الماركات الموقوفة بتترجع برضه.**

          فني متسند لماركة اتوقفت لازم القيد بتاعه يفضل مفهوم — لو
          اختفت من القواعد، لابات الماركة دي كانت هتبقى «مش معروفة»
          فجأة وتعدّي من القيد في صمت.
        */
        var rows = await db.LaptopBrands.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                Aliases = x.Aliases.Select(a => a.RawValue).ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(x => new BrandRule(x.Id, x.Name, x.Aliases)).ToList();
    }

    public async Task<string> DeviceManufacturerAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        await db.Devices.AsNoTracking()
            .Where(d => d.Id == deviceId && d.TenantId == tenantId)
            .Select(d => d.LastKnownManufacturer)
            .FirstOrDefaultAsync(ct) ?? "";

    // =================================================================
    //  الطابور
    // =================================================================

    public async Task<IReadOnlyList<RepairWorkItem>> PendingAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.RepairWorkItems.AsNoTracking()
            .Include(w => w.Device)
            .Include(w => w.AssignedTechnician)
            .Include(w => w.Parts)
            .Where(w => w.TenantId == tenantId && w.Approval == RepairApproval.Pending)

            // ⚠️ الأقدم الأول — دي طابور شغل مش قايمة أخبار.
            .OrderBy(w => w.OpenedAtUtc)
            .ToListAsync(ct);
}
