using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.AddNote;

/// <summary>
/// ملاحظة جديدة على لاب.
///
/// <para>⚠️ <b>مفيش كاتب ولا شركة هنا.</b> الاتنين من
/// <c>ICurrentUser</c> — الجسم بيبعت النص وبس.</para>
/// </summary>
public sealed record AddDeviceNoteCommand(Guid DeviceId, string? Body)
    : IRequest<Result<DeviceNoteItem>>;
