using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using MediatR;

namespace Codlek.Application.Features.Reports.GetReportDetail;

/// <summary>
/// فحص واحد بكل تفاصيله.
///
/// <para>⚠️ <b>بلا سياسة، والحارس جوّه المعالج:</b> الفني بيشوف
/// فحوصاته هو، والمدير بيشوف الكل — والحارس محتاج يقرا الصف الأول
/// عشان يعرف الفحص بتاع مين.</para>
/// </summary>
public sealed record GetReportDetailQuery(Guid Id) : IRequest<Result<ReportDetail>>;
