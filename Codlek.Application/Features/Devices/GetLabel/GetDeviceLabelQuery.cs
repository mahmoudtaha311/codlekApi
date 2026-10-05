using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetLabel;

public sealed record GetDeviceLabelQuery(Guid DeviceId) : IRequest<Result<DeviceLabel>>;
