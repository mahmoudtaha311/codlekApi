using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="IDeviceSyncRepository"/>
public sealed class DeviceSyncRepository(AppDbContext db) : IDeviceSyncRepository
{
    public async Task<IReadOnlyList<Guid>> MatchAnchorAsync(
        Guid tenantId, DeviceIdentifierKind kind, string normalizedValue,
        Guid? exceptDeviceId, int take, CancellationToken ct = default) =>
        await db.DeviceIdentifiers.AsNoTracking()
            .Where(i => i.TenantId == tenantId
                     && i.Kind == kind
                     && i.NormalizedValue == normalizedValue
                     && i.IsActive

                     // 🔴 المدموجين مستبعدين — الكانوني هو اللي
                     //    بيتشاور عليه. من غير ده، الجهاز بيشتبه في
                     //    نفسه بعد كل دمج.
                     && i.Device!.Status != DeviceLifecycleStatus.Merged

                     && (exceptDeviceId == null || i.DeviceId != exceptDeviceId))
            .Select(i => i.DeviceId)
            .Distinct()

            // ⚠️ ترتيب ثابت — عشان نفس المدخلات تدّي نفس القرار.
            .OrderBy(id => id)

            .Take(take)
            .ToListAsync(ct);

    public Task<DeviceAlias?> FindAliasAsync(
        Guid tenantId, Guid aliasDeviceId, CancellationToken ct = default) =>
        db.DeviceAliases.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.AliasDeviceId == aliasDeviceId, ct);

    public Task<Device?> FindWithIdentifiersAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
        db.Devices
            .Include(d => d.Identifiers)
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == deviceId, ct);

    public async Task<Guid?> CodeClashAsync(
        Guid tenantId, string publicCode, Guid exceptDeviceId,
        CancellationToken ct = default)
    {
        var clash = await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId
                     && d.PublicCode == publicCode
                     && d.Id != exceptDeviceId)
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);

        return clash == Guid.Empty ? null : clash;
    }

    public void Add(Device device) => db.Devices.Add(device);

    public void AddAlias(DeviceAlias alias) => db.DeviceAliases.Add(alias);

    public void AddWorkflowEvent(DeviceWorkflowEvent workflowEvent) =>
        db.DeviceWorkflowEvents.Add(workflowEvent);

    public Task<ImportContainer?> FindContainerAsync(
        Guid tenantId, string normalizedCode, CancellationToken ct = default) =>
        db.Containers.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.NormalizedCode == normalizedCode, ct);

    public void AddContainer(ImportContainer container) => db.Containers.Add(container);

    public async Task<IReadOnlyList<Guid>> TwinsByAnchorsAsync(
        Guid tenantId, Guid exceptDeviceId, IReadOnlyCollection<string> normalizedValues,
        CancellationToken ct = default) =>
        normalizedValues.Count == 0
            ? []
            : await db.DeviceIdentifiers.AsNoTracking()
                .Where(i => i.TenantId == tenantId
                         && i.DeviceId != exceptDeviceId
                         && i.IsActive

                         // 🔴 شواهد الدمج مش توائم — نفس سبب
                         //    `MatchAnchorAsync`.
                         && i.Device!.Status != DeviceLifecycleStatus.Merged

                         && normalizedValues.Contains(i.NormalizedValue))
                .Select(i => i.DeviceId)
                .Distinct()
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Device>> ActiveDevicesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0
            ? []
            : await db.Devices
                .Where(d => d.TenantId == tenantId
                         && ids.Contains(d.Id)
                         && d.Status == DeviceLifecycleStatus.Active)
                .ToListAsync(ct);

    /// <summary>
    /// 🔴 <b>والسؤال ده محتاج يشوف المراسي الجديدة كمان</b> — مش
    /// الصف بس.
    ///
    /// <para>مرساة جديدة اتضافت لجهاز ماتغيّرتش فيه خانة واحدة =
    /// الجهاز <b>اتحدّث</b> فعلاً. و<c>Entry(device).State</c> لوحده
    /// بيقول <c>Unchanged</c>، لأن المراسي كيانات تانية.</para>
    /// </summary>
    public bool WasTouched(Device device) =>
        db.Entry(device).State == EntityState.Modified
        || db.ChangeTracker.Entries<DeviceIdentifierRow>()
            .Any(e => e.State is EntityState.Added or EntityState.Modified
                   && e.Entity.DeviceId == device.Id)
        || db.ChangeTracker.Entries<DeviceAlias>()
            .Any(e => e.State == EntityState.Added
                   && e.Entity.CanonicalDeviceId == device.Id);
}
