using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportAudit;

/// <summary>⚠️ نفس معاملات شاشة السجل بالحرف — ناقص الصفحة.</summary>
public sealed record ExportAuditQuery(
    DateTime? From,
    DateTime? To,
    string? Action,
    string? EntityType,
    string? Actor,
    string? Search) : IRequest<Result<ExportWorkbook>>;
