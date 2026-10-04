using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportRepairs;

/// <summary>⚠️ نفس معاملات قايمة الصيانة بالحرف — ناقص الصفحة.</summary>
public sealed record ExportRepairsQuery(
    string? Search,
    string? Status,
    string? Approval,
    Guid? Technician,
    DateTime? From,
    DateTime? To,
    string? Sort) : IRequest<Result<ExportWorkbook>>;
