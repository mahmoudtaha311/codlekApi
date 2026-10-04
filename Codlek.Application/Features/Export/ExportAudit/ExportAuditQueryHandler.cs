using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using Codlek.Application.Features.Audit;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using MediatR;

namespace Codlek.Application.Features.Export.ExportAudit;

/// <summary>
/// تصدير سجل المراجعة.
///
/// <para>⚠️ <b>الإجراء بيتترجم في الملف زي ما بيتترجم على
/// الشاشة.</b> المالك اللي بيفتح الإكسل بيقرا نفس الكلام —
/// و<c>rack.paired</c> خام جوّه عمود عربي بيخلّي الصف ده مش مقروء
/// لحد.</para>
///
/// <para>⚠️ <b>ونوع الكيان بيتكتب خام.</b> كده في القديم: العمود
/// بيستعمل للفرز في إكسل، والقيمة الخام (<c>Rack</c>) بتفرز مع
/// بعضها حتى لو السجل فيه <c>repair</c> صغيرة و<c>Device</c>
/// كبيرة.</para>
/// </summary>
public sealed class ExportAuditQueryHandler(
    IAuditRepository audit, ICurrentUser me)
    : IRequestHandler<ExportAuditQuery, Result<ExportWorkbook>>
{
    public async Task<Result<ExportWorkbook>> Handle(
        ExportAuditQuery query, CancellationToken cancellationToken)
    {
        var filter = AuditSearchFilters.Build(
            query.From, query.To, query.Action, query.EntityType,
            query.Actor, query.Search);

        var rows = await audit.ExportAsync(
            me.TenantId, filter, ExportLimits.MaxRows, cancellationToken);

        var sheet = new Sheet("سجل المراجعة", ExportColumns.Audit,
            ExportLimits.Capped(rows, a => new object?[]
            {
                ExportValues.Cairo(a.OccurredAtUtc),
                a.ActorName,
                a.ActorType,
                AuditLabels.Action(a.Action),
                a.EntityType,
                a.EntityCode,
                a.Summary,
            }));

        return Result.Success(new ExportWorkbook("سجل المراجعة", [sheet]));
    }
}
