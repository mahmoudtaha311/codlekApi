using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetNotes;

public sealed record GetDeviceNotesQuery(Guid DeviceId)
    : IRequest<Result<IReadOnlyList<DeviceNoteItem>>>;
