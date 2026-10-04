using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities.Auth;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class AccountRepository(AppDbContext db) : IAccountRepository
{
    /// <summary>
    /// ⚠️ <b>متتبَّع مش <c>AsNoTracking</c></b> — المنادي ممكن يكتب
    /// (تعديل الاسم). ولو رجّعناه غير متتبَّع، الحفظ بيعدّي من غير ما
    /// يغيّر حاجة <b>ومن غير أي خطأ</b>.
    /// </summary>
    public Task<ApplicationUser?> FindAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

    public async Task<string> TenantNameAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "";
}
