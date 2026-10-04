using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetIdentifiers;

public sealed record GetDeviceIdentifiersQuery(Guid DeviceId)
    : IRequest<Result<IReadOnlyList<DeviceIdentifierItem>>>;
