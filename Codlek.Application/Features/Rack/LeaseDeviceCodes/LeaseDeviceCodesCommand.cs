using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using MediatR;

namespace Codlek.Application.Features.Rack.LeaseDeviceCodes;

/// <summary>
/// ⚠️ <b>الشركة والمحطة جايّين من المفتاح المتحقق</b> — مش من
/// الطلب. ولو جوا من الطلب، راكة مسروقة كانت هتاكل أكواد شركة
/// تانية.
/// </summary>
public sealed record LeaseDeviceCodesCommand(
    Guid TenantId,
    Guid RackId,
    int? Size,
    int? ConsumedThrough) : IRequest<Result<DeviceCodeLeaseResponse>>;
