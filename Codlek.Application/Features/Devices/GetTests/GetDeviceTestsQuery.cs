using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetTests;

public sealed record GetDeviceTestsQuery(Guid DeviceId, int? Page, int? PageSize)
    : IRequest<Result<PagedResult<DeviceTestItem>>>;
