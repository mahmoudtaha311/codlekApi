using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Audit;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Audit.GetAuditFilters;

public sealed class GetAuditFiltersQueryHandler(
    IAuditRepository audit,
    ICurrentUser me)
    : IRequestHandler<GetAuditFiltersQuery, Result<AuditFilters>>
{
    /// <summary>
    /// ⚠️ سقف على أسماء الفاعلين: الأكواد محدودة بطبعها، لكن
    /// الأسماء بتكبر مع كل موظف ومع كل راكة — وقايمة منسدلة فيها
    /// ألف اسم مش فلتر.
    /// </summary>
    private const int MaxActors = 100;

    public async Task<Result<AuditFilters>> Handle(
        GetAuditFiltersQuery query, CancellationToken cancellationToken)
    {
        var (actions, entityTypes, actors) = await audit.DistinctValuesAsync(
            me.TenantId, MaxActors, cancellationToken);

        /*
          ⚠️ **الترتيب بالنص العربي مش بالكود.**

          القايمة بتتعرض للمالك بالعربي، والترتيب بالكود كان بيخلّي
          «إلغاء أمر صيانة» جمب «إنشاء ماركة» لأن `brand.` قبل
          `repair.` — ترتيب مالوش أي معنى للّي بيقرا.
        */
        return Result.Success(new AuditFilters(
            actions
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => new NamedOption(a, AuditLabels.Action(a)))
                .OrderBy(a => a.Label, StringComparer.Ordinal)
                .ToList(),

            entityTypes
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => new NamedOption(e, AuditLabels.Entity(e)))
                .OrderBy(e => e.Label, StringComparer.Ordinal)
                .ToList(),

            actors));
    }
}
