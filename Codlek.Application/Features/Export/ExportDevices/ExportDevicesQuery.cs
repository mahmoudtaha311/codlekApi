using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportDevices;

/// <summary>
/// ⚠️ <b>نفس معاملات قايمة الأجهزة بالحرف</b> — ناقص الصفحة. أي
/// معامل هنا مش هناك معناه إن الملف بيختلف عن الشاشة.
/// </summary>
public sealed record ExportDevicesQuery(
    string? Search,
    string? Status,
    string? Confidence,
    string? Outcome,
    string? Technician,
    Guid? Rack,
    DateTime? From,
    DateTime? To,
    string? Stage,
    string? Sort,
    Guid? Container,
    string? Flag,
    string? Handover,
    Guid? Location) : IRequest<Result<ExportWorkbook>>;
