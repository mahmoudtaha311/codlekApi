using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Audit;
using MediatR;

namespace Codlek.Application.Features.Audit.GetAuditFilters;

/// <summary>
/// خيارات فلترة السجل — <b>من الصفوف الموجودة فعلاً</b>.
/// </summary>
public sealed record GetAuditFiltersQuery : IRequest<Result<AuditFilters>>;
