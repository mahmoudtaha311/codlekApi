using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

public sealed class AuditRepository(AppDbContext db) : IAuditRepository
{
    public async Task<(IReadOnlyList<AuditEvent> Rows, int TotalItems)> SearchAsync(
        Guid tenantId, AuditFilter filter, CancellationToken ct = default)
    {
        var q = Filtered(tenantId, filter);

        int total = await q.CountAsync(ct);

        var rows = await Ordered(q)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (rows, total);
    }

    /// <summary>
    /// 🔴 <b>نفس الفلتر ونفس الترتيب، <u>بلا تصفيح</u>.</b> الملف
    /// اللي بيطلع بصفوف غير اللي قدام المالك أسوأ من مفيش ملف.
    /// </summary>
    public async Task<IReadOnlyList<AuditEvent>> ExportAsync(
        Guid tenantId, AuditFilter filter, int cap, CancellationToken ct = default) =>

        // 🔴 **`cap + 1` — المستودع هو اللي بيزوّد صف الكشف.**
        //
        // المنادي بيبعت السقف زي ما هو، والصف الزيادة هو اللي
        // بيخلّيه يعرف إن فيه قص ويقوله **جوّه الملف**. ولو المنادي
        // هو اللي زوّد، كان لازم كل نقطة تفتكر — وأول واحدة تنسى
        // بتطلّع ملف مقصوص في صمت.
        await Ordered(Filtered(tenantId, filter)).Take(cap + 1).ToListAsync(ct);

    private IQueryable<AuditEvent> Filtered(Guid tenantId, AuditFilter filter)
    {
        var q = db.AuditEvents.AsNoTracking().Where(e => e.TenantId == tenantId);

        if (filter.FromUtc is { } from) q = q.Where(e => e.OccurredAtUtc >= from);

        // 🔴 أصغر من، مش أصغر من أو يساوي.
        if (filter.ToUtc is { } to) q = q.Where(e => e.OccurredAtUtc < to);

        // ⚠️ مقارنة مضبوطة: القيمة جاية من قايمة الفلاتر اللي النقطة
        // نفسها بتبنيها.
        if (filter.Action is { Length: > 0 } action) q = q.Where(e => e.Action == action);

        if (filter.EntityType is { Length: > 0 } entityType)
            q = q.Where(e => e.EntityType == entityType);

        if (filter.ActorName is { Length: > 0 } actor) q = q.Where(e => e.ActorName == actor);

        /*
          ⚠️ **البحث على تلات أعمدة بـ«أو».**

          اللي بيراجع بيكتب كود لاب أو اسم موظف أو كلمة من الملخّص
          وهو مش فاكر هي فين — فالبحث لازم يلاقيها في أي عمود منهم.
        */
        if (filter.SearchPattern is { } pattern)
        {
            q = q.Where(e =>
                EF.Functions.Like(e.Summary, pattern, SearchPattern.Escape)
                || EF.Functions.Like(e.EntityCode, pattern, SearchPattern.Escape)
                || EF.Functions.Like(e.ActorName, pattern, SearchPattern.Escape));
        }

        return q;
    }

    /// <summary>
    /// 🔴 <b>فاصل التعادل هنا إجباري أكتر من أي قايمة تانية.</b>
    ///
    /// <para>دفعة إجراءات واحدة (تسليم ٥٠ لاب) بتكتب سطور بنفس
    /// اللحظة بالحرف — ومن غير الفاصل، السطر بيظهر في صفحتين أو
    /// بيختفي، والمراجعة بتبان ناقصة.</para>
    /// </summary>
    private static IQueryable<AuditEvent> Ordered(IQueryable<AuditEvent> q) =>
        q.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id);

    public async Task<(IReadOnlyList<string> Actions,
                       IReadOnlyList<string> EntityTypes,
                       IReadOnlyList<string> Actors)>
        DistinctValuesAsync(Guid tenantId, int maxActors, CancellationToken ct = default)
    {
        var scoped = db.AuditEvents.AsNoTracking().Where(e => e.TenantId == tenantId);

        var actions = await scoped
            .Select(e => e.Action)
            .Distinct()
            .ToListAsync(ct);

        var entityTypes = await scoped
            .Select(e => e.EntityType)
            .Distinct()
            .ToListAsync(ct);

        /*
          ⚠️ **سقف على أسماء الفاعلين.**

          الأكواد وأنواع الكيانات محدودة بطبعها (عشرين حاجة)، لكن
          الأسماء بتكبر مع كل موظف ومع كل راكة — وقايمة منسدلة فيها
          ألف اسم مش فلتر.
        */
        var actors = await scoped
            .Where(e => e.ActorName != "")
            .Select(e => e.ActorName)
            .Distinct()
            .OrderBy(x => x)
            .Take(maxActors)
            .ToListAsync(ct);

        return (actions, entityTypes, actors);
    }
}
