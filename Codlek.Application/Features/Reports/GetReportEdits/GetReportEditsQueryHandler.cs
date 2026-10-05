using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Reports.GetReportEdits;

/// <summary>
/// تعديلات الفحص بعد التسليم — القيمة القديمة والجديدة ومين وليه.
///
/// <para>⚠️ <b>القايمة الفاضية رد عادي</b> (<c>200</c> و<c>[]</c>)،
/// والشاشة بتخفي القسم ساعتها — زي القديم. أما الفحص المش موجود في
/// الشركة فـ<c>404</c>، عشان «مفيش تعديلات» ماتبقاش إجابة على معرّف
/// شركة تانية.</para>
///
/// <para>⚠️ <b>وبتشتغل على الفحص الممسوح كمان</b> — صفحته بتفتح،
/// وتعديلاته جزء من الدليل.</para>
/// </summary>
public sealed class GetReportEditsQueryHandler(
    IReportRepository reports,
    ICurrentUser me)
    : IRequestHandler<GetReportEditsQuery, Result<IReadOnlyList<ReportEditItem>>>
{
    public async Task<Result<IReadOnlyList<ReportEditItem>>> Handle(
        GetReportEditsQuery query, CancellationToken cancellationToken)
    {
        if (!await reports.ExistsAsync(me.TenantId, query.Id, cancellationToken))
            return Result.Failure<IReadOnlyList<ReportEditItem>>(ReportErrors.NotFound);

        // ⚠️ السياسة على المسار بتمنع ده قبل ما يوصل هنا — والترتيب
        // (٤٠٤ قبل ٤٠٣) نفس ترتيب صفحة الفحص.
        if (!me.IsManagerOrAbove)
            return Result.Failure<IReadOnlyList<ReportEditItem>>(ReportErrors.EditsForbidden);

        var edits = await reports.EditsAsync(me.TenantId, query.Id, cancellationToken);

        return Result.Success<IReadOnlyList<ReportEditItem>>(edits
            .Select(e => new ReportEditItem(
                e.AtUtc, e.ByName, e.Field, e.OldValue, e.NewValue, e.Reason))
            .ToList());
    }
}
