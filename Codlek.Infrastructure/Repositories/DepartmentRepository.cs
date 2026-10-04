using Codlek.Application.Contracts.Departments;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<IReadOnlyList<DepartmentRow>> ListAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Departments
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .Select(d => new DepartmentRow(
                d.Id, d.Code, d.Name, d.IsActive, d.SortOrder,
                // ⚠️ العدّ جوّه الاستعلام — مش استعلام لكل قسم.
                // بـ١٢ قسم الفرق مش بيبان، بس النمط ده بيتكرر في كل
                // قطاع جاي وفيه قطاعات بمئات الصفوف.
                db.Technicians.Count(t => t.DepartmentId == d.Id)))
            .ToListAsync(ct);

    public Task<Department?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Departments.FirstOrDefaultAsync(
            d => d.Id == id && d.TenantId == tenantId, ct);

    public Task<bool> NameTakenAsync(
        Guid tenantId, string name, Guid? exceptId = null, CancellationToken ct = default) =>
        db.Departments.AnyAsync(
            d => d.TenantId == tenantId
                 && d.Name == name
                 && (exceptId == null || d.Id != exceptId), ct);

    public void Add(Department department) => db.Departments.Add(department);

    public Task<int> CountTechniciansAsync(Guid departmentId, CancellationToken ct = default) =>
        db.Technicians.CountAsync(t => t.DepartmentId == departmentId, ct);
}
