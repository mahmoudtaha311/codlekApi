using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class UserAccountRepository(AppDbContext db) : IUserAccountRepository
{
    public async Task<IReadOnlyList<ApplicationUser>> ListAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Users
            // ⚠️ مقفولة على الشركة بالإيد — مفيش global query filter،
            // فأي `db.Users` من غير الشرط ده بيعرض حسابات شركات تانية
            // في صمت.
            .Where(u => u.TenantId == tenantId)

            // الفنيين تحت — نفس ترتيب الصفحة القديمة عشان اللي بيبص
            // على الاتنين مايحسش إنهم قايمتين مختلفتين.
            .OrderBy(u => u.Role == UserRole.Technician ? 1 : 0)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(ct);

    /// <summary>
    /// ⚠️ <b>متتبَّع مش <c>AsNoTracking</c> عن قصد.</b> الصفوف اللي
    /// بتيجي من استعلام غير متتبَّع بتتعدّل في الذاكرة
    /// و<c>SaveChangesAsync</c> مابتكتبش حاجة — نقطة بترجّع نجاح
    /// والقاعدة زي ما هي.
    /// </summary>
    public Task<ApplicationUser?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct);

    /// <summary>
    /// 🔴 <b>مفيش شرط شركة هنا — وده مقصود.</b>
    ///
    /// <para>مفتاح الدخول عالمي: صفحة الدخول بتستلم اسم وباسورد وبس،
    /// فالاسم لازم يوصل لصف واحد مهما كان عدد الشركات.</para>
    ///
    /// <para>🔴 <b>والمقارنة على العمود <c>NormalizedUserName</c>، مش
    /// بنداء <c>LoginName.Normalize</c> جوّه الاستعلام.</b></para>
    ///
    /// <para>كتبتها كده الأول وكانت هتقع وقت التشغيل: دالة C#
    /// مابتترجمش لـSQL، وEF بترمي. والعمود ده فيه نفس المفتاح
    /// بالطريقة دي بالظبط — راجع <c>LoginNameNormalizer</c>.</para>
    /// </summary>
    public Task<bool> UsernameTakenAnywhereAsync(
        string normalizedUsername, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUsername, ct);

    public Task<bool> CodeTakenAsync(
        Guid tenantId, string code, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.TenantId == tenantId && u.Code == code, ct);

    public void Add(ApplicationUser user) => db.Users.Add(user);
}
