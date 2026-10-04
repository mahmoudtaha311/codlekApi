using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.GetAccounts;

/// <summary>كل حسابات فنيي الشركة، ومعاها ماركات كل واحد.</summary>
public sealed record GetTechnicianAccountsQuery
    : IRequest<Result<IReadOnlyList<TechnicianAccount>>>;
