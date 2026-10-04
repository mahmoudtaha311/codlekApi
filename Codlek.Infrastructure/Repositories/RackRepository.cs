using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class RackRepository(AppDbContext db) : IRackRepository
{
    /// <summary>
    /// ⚠️ غير متتبّعة — للعرض بس.
    ///
    /// <para>⚠️ <b>و<c>ThenBy(Id)</c> مش تزيين.</b> القايمة مش
    /// مقسّمة صفحات، فالمشكلة هنا مش تكرار صف: هي إن محطتين بنفس
    /// الكود (ودي حالة بتحصل لو عدّاد الشركة اتظبّط بالإيد) بيتبدّلوا
    /// في الترتيب مع كل تحديث للصفحة — والمالك بيدوس «إيقاف» على
    /// الصف اللي اتحرك.</para>
    /// </summary>
    public async Task<IReadOnlyList<Rack>> ListAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.Racks.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.RackCode)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    /// <summary>
    /// 🔴 <b>متتبّعة</b> — الإيقاف والإلغاء بيعدّلوا الصف في مكانه،
    /// والحفظ من وحدة العمل.
    /// </summary>
    public Task<Rack?> FindAsync(
        Guid tenantId, Guid rackId, CancellationToken ct = default) =>
        db.Racks.FirstOrDefaultAsync(
            r => r.Id == rackId && r.TenantId == tenantId, ct);

    /// <summary>
    /// 🔴 <b>الشرطين في SQL: الحالة والبادئة.</b>
    ///
    /// <para>⚠️ <b>و<c>r.Status == RackStatus.Active</c> مش
    /// <c>r.IsActive</c>.</b> التانية <c>[NotMapped]</c> — خاصية
    /// محسوبة في C# — واستعمالها هنا مابيترجمش وبيخلّي <b>كل</b>
    /// نداء راكة يرجّع <c>500</c>. والفرق بين السطرين حرف واحد في
    /// الكود وتوقّف كامل في الميدان.</para>
    ///
    /// <para>⚠️ وغير متتبّعة: التحقق قراية بس.</para>
    /// </summary>
    public async Task<IReadOnlyList<Rack>> ActiveByKeyPrefixAsync(
        string keyPrefix, CancellationToken ct = default) =>
        await db.Racks.AsNoTracking()
            .Where(r => r.Status == RackStatus.Active && r.KeyPrefix == keyPrefix)
            .ToListAsync(ct);

    // =================================================================
    //  أكواد التفعيل
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>ConsumedAtUtc == null</c> هو القاعدة نفسها</b> —
    /// شيله بيحوّل القايمة من «أكواد مستنية» لـ«كل كود اتعمل في
    /// الورشة من يوم ما فتحت».
    ///
    /// <para>⚠️ <b>والانتهاء مش في الفلتر ده.</b> الكود المنتهي لازم
    /// يفضل باين عشان المالك يمسحه — الواجهة بتعلّمه «منتهي» وزرار
    /// المسح بيبان عليه هو بس.</para>
    /// </summary>
    public async Task<IReadOnlyList<RackPairingCode>> PendingCodesAsync(
        Guid tenantId, CancellationToken ct = default) =>
        await db.RackPairingCodes.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ConsumedAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

    /// <summary>
    /// 🔴 <b>متتبّع، و<u>من غير</u> فلتر على الاستهلاك.</b> المسح
    /// محتاج يشوف الكود المستهلك عشان يرفضه برسالة بتشرح السبب —
    /// فلترته هنا كانت بترجّع «مش موجود».
    /// </summary>
    public Task<RackPairingCode?> FindCodeAsync(
        Guid tenantId, Guid codeId, CancellationToken ct = default) =>
        db.RackPairingCodes.FirstOrDefaultAsync(
            c => c.Id == codeId && c.TenantId == tenantId, ct);

    public void AddCode(RackPairingCode code) => db.RackPairingCodes.Add(code);

    public void RemoveCode(RackPairingCode code) => db.RackPairingCodes.Remove(code);
}
