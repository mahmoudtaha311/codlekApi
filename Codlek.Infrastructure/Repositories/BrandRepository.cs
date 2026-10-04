using Codlek.Application.Contracts.Brands;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class BrandRepository(AppDbContext db) : IBrandRepository
{
    public async Task<IReadOnlyList<BrandRow>> ListAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.LaptopBrands
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new BrandRow(
                x.Id,
                x.Name,
                x.IsActive,
                x.SortOrder,
                x.Aliases.OrderBy(a => a.RawValue).Select(a => a.RawValue).ToList(),
                db.TechnicianBrands.Count(t => t.BrandId == x.Id)))
            .ToListAsync(ct);

    public Task<LaptopBrand?> FindWithAliasesAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.LaptopBrands
            .Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);

    public Task<LaptopBrand?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.LaptopBrands.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);

    public Task<bool> NameTakenAsync(
        Guid tenantId, string normalizedName, Guid? exceptId = null,
        CancellationToken ct = default) =>
        db.LaptopBrands.AnyAsync(
            x => x.TenantId == tenantId
                 && x.NormalizedName == normalizedName
                 && (exceptId == null || x.Id != exceptId), ct);

    public Task<LaptopBrand?> FindByNormalizedNameAsync(
        Guid tenantId, string normalizedName, Guid? exceptId = null,
        CancellationToken ct = default) =>
        db.LaptopBrands.AsNoTracking().FirstOrDefaultAsync(
            x => x.TenantId == tenantId
                 && x.NormalizedName == normalizedName
                 && (exceptId == null || x.Id != exceptId), ct);

    public async Task<(Guid BrandId, string BrandName)?> FindAliasOwnerAsync(
        Guid tenantId, string normalizedValue, CancellationToken ct = default)
    {
        /*
          ⚠️ **الاستعلام على كل أسماء الشركة، مش على ماركة واحدة.**

          التفرّد على مستوى الشركة: لو ماركتين ادّعوا «HP»، حل اللاب
          بيبقى معتمد على ترتيب الصفوف.
        */
        var row = await db.LaptopBrandAliases.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.NormalizedValue == normalizedValue)
            .Select(a => new { a.BrandId, BrandName = a.Brand!.Name })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.BrandId, row.BrandName);
    }

    public Task<LaptopBrandAlias?> FindAliasAsync(
        Guid tenantId, Guid brandId, string normalizedValue, CancellationToken ct = default) =>
        db.LaptopBrandAliases.FirstOrDefaultAsync(
            a => a.TenantId == tenantId
                 && a.BrandId == brandId
                 && a.NormalizedValue == normalizedValue, ct);

    public void Add(LaptopBrand brand) => db.LaptopBrands.Add(brand);

    public void AddAlias(LaptopBrandAlias alias) => db.LaptopBrandAliases.Add(alias);

    public void RemoveAlias(LaptopBrandAlias alias) => db.LaptopBrandAliases.Remove(alias);

    public Task<int> CountTechniciansAsync(Guid brandId, CancellationToken ct = default) =>
        db.TechnicianBrands.CountAsync(t => t.BrandId == brandId, ct);

    public async Task<IReadOnlyList<string>> AliasValuesAsync(
        Guid brandId, CancellationToken ct = default) =>
        await db.LaptopBrandAliases.AsNoTracking()
            .Where(a => a.BrandId == brandId)
            .OrderBy(a => a.RawValue)
            .Select(a => a.RawValue)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BrandRule>> RulesAsync(
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

        return rows
            .Select(x => new BrandRule(x.Id, x.Name, x.Aliases))
            .ToList();
    }

    public async Task<IReadOnlyList<(string Name, int Count)>> RawManufacturersAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var rows = await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.LastKnownManufacturer != "")
            .GroupBy(d => d.LastKnownManufacturer)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.Select(x => (x.Name, x.Count)).ToList();
    }
}
