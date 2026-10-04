using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetRepairDetail;

public sealed record GetRepairDetailQuery(Guid Id) : IRequest<Result<RepairDetail>>;
