using Codlek.Application.Interfaces.Repositories;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="IDeviceReferenceRepository"/>
public sealed class DeviceReferenceRepository(AppDbContext db) : IDeviceReferenceRepository
{
    /// <summary>
    /// ⚠️ <b>خانتين بس</b> — الحالة واللي اتدمج فيه. تحميل الكيان
    /// كامل هنا معناه رحلة أكبر على مسار بيشتغل مع كل صف في كل دفعة.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, DeviceMergeState>> DeviceStatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, DeviceMergeState>();

        var rows = await db.Devices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && ids.Contains(d.Id))
            .Select(d => new { d.Id, d.Status, d.MergedIntoDeviceId })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => new DeviceMergeState
            {
                Status = r.Status,
                MergedIntoDeviceId = r.MergedIntoDeviceId,
            });
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> AliasTargetsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> aliasIds, CancellationToken ct = default)
    {
        if (aliasIds.Count == 0) return new Dictionary<Guid, Guid>();

        /*
          ⚠️ **ترشيح الشركة هنا حزام تاني — مقسوم إنه زيادة.**

          شيلناه بتحوير مقصود والفحوص كلها عدّت، والسبب إن السلسلة
          بتنتهي دايماً عند **صف جهاز في الشركة دي**: استعلام الحالة
          مربوط بالشركة، فاسم مستعار سرب من ورشة تانية بيوصّلنا لمعرّف
          مالوش صف عندنا — فيطلع «مجهول».

          🔴 **والتسريب محتاج حاجتين مع بعض:** الترشيح ده يتشال،
          **و** نفس الـGuid يبقى موجود كجهاز في الورشتين. ومعرّفات
          الأجهزة Guid بتتولّد على الراكات، فالتصادم ده عملياً مش
          بيحصل.

          ⚠️ **وسايبينه.** الحزام رخيص، والاعتماد على «الاستعلام
          التاني هيحمينا» بيخلّي أي تعديل هناك يفتح باب هنا من غير ما
          حد يلاحظ.
        */
        var rows = await db.DeviceAliases.AsNoTracking()
            .Where(a => a.TenantId == tenantId && aliasIds.Contains(a.AliasDeviceId))
            .Select(a => new { a.AliasDeviceId, a.CanonicalDeviceId })
            .ToListAsync(ct);

        /*
          ⚠️ **مفتاح مكرر مش ممكن هنا** — فيه فهرس فريد على
          (شركة، معرّف مستعار). ولو اتكسر يوم ما، `ToDictionary`
          بترمي وده أحسن من إننا نختار واحد عشوائي ونربط شغل بجهاز
          غلط في صمت.
        */
        return rows.ToDictionary(r => r.AliasDeviceId, r => r.CanonicalDeviceId);
    }
}
