using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using MediatR;

namespace Codlek.Application.Features.Devices.GetNotes;

public sealed class GetDeviceNotesQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<GetDeviceNotesQuery, Result<IReadOnlyList<DeviceNoteItem>>>
{
    public async Task<Result<IReadOnlyList<DeviceNoteItem>>> Handle(
        GetDeviceNotesQuery query, CancellationToken cancellationToken)
    {
        // 🔴 وجود اللاب الأول — القايمة الفاضية بـ٢٠٠ مش رد على
        //    معرّف غلط.
        if (await devices.FindDetailAsync(me.TenantId, query.DeviceId, cancellationToken) is null)
            return Result.Failure<IReadOnlyList<DeviceNoteItem>>(DeviceErrors.NotFound);

        var rows = await devices.NotesAsync(me.TenantId, query.DeviceId, cancellationToken);

        var items = rows
            .Select(n => new DeviceNoteItem(n.Id, n.Body, n.CreatedByName, n.CreatedAtUtc))
            .ToList();

        return Result.Success<IReadOnlyList<DeviceNoteItem>>(items);
    }
}
