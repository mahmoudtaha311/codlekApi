using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Reports;
using MediatR;

namespace Codlek.Application.Features.Reports.GetReports;

/// <summary>
/// قايمة الفحوص.
///
/// <para>⚠️ <b>بلا سياسة — الفني بيشوف فحوصاته هو.</b> والتضييق في
/// الاستعلام مش في العرض، فالتصدير مابيقدرش يتخطّاه.</para>
/// </summary>
public sealed record GetReportsQuery(
    string? Search,
    string? Result,
    string? Technician,
    Guid? Container,
    DateTime? From,
    DateTime? To,
    int? Page,
    int? PageSize) : IRequest<Result<PagedResult<ReportListItem>>>;
