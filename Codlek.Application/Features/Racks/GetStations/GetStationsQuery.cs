using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.GetStations;

/// <summary>
/// ⚠️ مفيش ترشيح ولا صفحات — ورشة عندها عشرين محطة بالكتير، والقايمة
/// دي صفحة إدارة بتتفتح لما فيه مشكلة.
/// </summary>
public sealed record GetStationsQuery
    : IRequest<Result<IReadOnlyList<StationListItem>>>;
