using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportReports;

/// <summary>⚠️ نفس معاملات قايمة الفحوص بالحرف — ناقص الصفحة.</summary>
public sealed record ExportReportsQuery(
    string? Search,
    string? Result,
    string? Technician,
    Guid? Container,
    DateTime? From,
    DateTime? To) : IRequest<Result<ExportWorkbook>>;
