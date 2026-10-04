using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class TechnicianAccountRepository(AppDbContext db)
    : ITechnicianAccountRepository
{
    // ⚠️ القايمة للعرض بس — غير متتبّعة.
    public async Task<IReadOnlyList<Technician>> ListAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Technicians.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(ct);

    /// <summary>
    /// 🔴 <b>متتبّع</b> — كل إجراء بيعدّل الصف في مكانه، والحفظ من
    /// وحدة العمل.
    /// </summary>
    public Task<Technician?> FindAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        db.Technicians.FirstOrDefaultAsync(
            t => t.Id == technicianId && t.TenantId == tenantId, ct);

    /// <summary>
    /// 🔴 <b>الترشيح بالشركة هنا هو القاعدة نفسها</b> — شيله بيخلّي
    /// اسم الدخول فريد عالمياً، وده <b>مش</b> المطلوب.
    /// </summary>
    public Task<bool> UsernameTakenAsync(
        Guid tenantId, string normalizedUsername, CancellationToken ct = default) =>
        db.Technicians.AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId
                        && t.NormalizedUsername == normalizedUsername, ct);

    public Task<bool> CodeTakenAsync(
        Guid tenantId, string code, CancellationToken ct = default) =>
        db.Technicians.AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.Code == code, ct);

    public void Add(Technician technician) => db.Technicians.Add(technician);

    // =================================================================
    //  الماركات
    // =================================================================

    /// <summary>
    /// ⚠️ <b>قراية واحدة لكل الربط، مش قراية لكل فني.</b> الشاشة
    /// بتعرض الماركات جمب كل سطر، ولو اتحمّلت سطر سطر كانت بتبقى
    /// N+1 على صفحة فيها كل فنيي الورشة.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> BrandLinksAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var links = await db.TechnicianBrands.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.TechnicianId, x.BrandId })
            .ToListAsync(ct);

        return links
            .GroupBy(x => x.TechnicianId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(x => x.BrandId).ToList());
    }

    // ⚠️ متتبّعة: الاستبدال بيمسح الصفوف دي ويضيف غيرها.
    public async Task<IReadOnlyList<TechnicianBrand>> BrandLinksForAsync(
        Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
        await db.TechnicianBrands
            .Where(x => x.TenantId == tenantId && x.TechnicianId == technicianId)
            .ToListAsync(ct);

    public Task<int> CountKnownBrandsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> brandIds, CancellationToken ct = default) =>
        db.LaptopBrands.AsNoTracking()
            .CountAsync(b => b.TenantId == tenantId && brandIds.Contains(b.Id), ct);

    public async Task<IReadOnlyList<string>> BrandNamesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> brandIds, CancellationToken ct = default) =>
        await db.LaptopBrands.AsNoTracking()
            .Where(b => b.TenantId == tenantId && brandIds.Contains(b.Id))
            .OrderBy(b => b.Name)
            .Select(b => b.Name)
            .ToListAsync(ct);

    public void RemoveBrandLinks(IEnumerable<TechnicianBrand> links) =>
        db.TechnicianBrands.RemoveRange(links);

    public void AddBrandLink(TechnicianBrand link) => db.TechnicianBrands.Add(link);
}
