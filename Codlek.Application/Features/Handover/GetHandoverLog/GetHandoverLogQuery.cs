using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.GetHandoverLog;

/// <summary>سجل التسليمات.</summary>
public sealed record GetHandoverLogQuery(
    DateTime? From,
    DateTime? To,
    Guid? Destination,
    string? Receiver,
    int? Page,
    int? PageSize) : IRequest<Result<PagedResult<HandoverLogItem>>>;
