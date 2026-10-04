using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="IOperationalSyncRepository"/>
public sealed class OperationalSyncRepository(AppDbContext db) : IOperationalSyncRepository
{
    public async Task<DateTime?> LastLoginAtAsync(
        Guid tenantId, Guid rackId, Guid technicianId, DateTime atOrBeforeUtc,
        CancellationToken ct = default) =>
        await db.TechnicianLoginAttempts.AsNoTracking()
            .Where(a => a.TenantId == tenantId
                     && a.RackId == rackId
                     && a.TechnicianId == technicianId

                     // 🔴 الناجح بس — الفاشل مش دليل تصريح.
                     && a.Success

                     // ⚠️ دخول **بعد** الحركة مايثبتش إنه كان مصرّح له
                     //    **وقتها**.
                     && a.AtUtc <= atOrBeforeUtc)
            .OrderByDescending(a => a.AtUtc)
            .ThenByDescending(a => a.Id)
            .Select(a => (DateTime?)a.AtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<Technician?> FindTechnicianAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        db.Technicians.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == technicianId && t.TenantId == tenantId, ct);

    public Task<bool> ReportExistsAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default) =>
        db.Reports.AnyAsync(r => r.Id == reportId && r.TenantId == tenantId, ct);

    public Task<RepairWorkItem?> FindWorkItemAsync(
        Guid tenantId, Guid workItemId, CancellationToken ct = default) =>
        db.RepairWorkItems
            .Include(w => w.Issues)
            .Include(w => w.Parts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workItemId && w.TenantId == tenantId, ct);

    public void AddWorkItem(RepairWorkItem item) => db.RepairWorkItems.Add(item);

    public void AddIssue(RepairWorkItemIssue issue) => db.RepairWorkItemIssues.Add(issue);

    public void AddPart(RepairPart part) => db.RepairParts.Add(part);
}
