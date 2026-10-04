using Codlek.Core.Entities.Auth;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// حساب المستخدم الحالي.
///
/// <para>🔴 <b>كل دالة بتاخد <c>tenantId</c> كمان، مش المعرّف وبس.</b>
/// المعرّف فريد عالمياً فالشرط ده شكله زيادة — بس لو حساب اتنقل لشركة
/// تانية بعد ما التوكن اتعمل، القراية من غير الشرط بتفتح بيانات شركة
/// التوكن مش شركة الصف. الشرط بيخلّي الجلسة تبطل بدل ما تخلط.</para>
/// </summary>
public interface IAccountRepository
{
    Task<ApplicationUser?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task<string> TenantNameAsync(Guid tenantId, CancellationToken ct = default);
}
