using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using MediatR;

namespace Codlek.Application.Features.Rack.RegisterRack;

/// <summary>
/// ⚠️ <b><c>Ip</c> و<c>SyncUrl</c> بيتحطّوا من الكنترولر</b> — مش
/// من جسم الطلب. العنوان من الاتصال نفسه، ورابط المزامنة من
/// الإعدادات.
/// </summary>
public sealed record RegisterRackCommand(
    string? PairingCode,
    string? RackName,
    string? InstallationId,
    string? MachineIdentifier,
    string? AppVersion,
    string? Ip,
    string SyncUrl) : IRequest<Result<RackRegistered>>;
