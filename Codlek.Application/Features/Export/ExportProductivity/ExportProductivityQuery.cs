using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportProductivity;

/// <summary>
/// ⚠️ نفس معاملات شاشة الإنتاجية، <b>زيادة <c>Technician</c></b> —
/// الشاشة بتفلتر بيه في المتصفح والملف لازم يفلتر بيه على السيرفر.
/// </summary>
public sealed record ExportProductivityQuery(
    string? Range,
    DateTime? From,
    DateTime? To,
    string? Search,
    string? Sort,
    string? Technician) : IRequest<Result<ExportWorkbook>>;
