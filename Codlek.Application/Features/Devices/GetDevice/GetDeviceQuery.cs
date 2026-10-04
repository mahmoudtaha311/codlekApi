using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetDevice;

public sealed record GetDeviceQuery(Guid DeviceId) : IRequest<Result<DeviceDetail>>;
