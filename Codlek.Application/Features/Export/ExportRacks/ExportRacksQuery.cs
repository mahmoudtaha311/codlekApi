using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportRacks;

/// <summary>⚠️ مفيش فلاتر — قايمة المحطات نفسها مفيهاش فلاتر.</summary>
public sealed record ExportRacksQuery : IRequest<Result<ExportWorkbook>>;
