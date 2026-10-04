using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.LookupDevice;

/// <summary>
/// مسح كود لاب — <b>ماسح باركود أو كتابة بالإيد</b>.
///
/// <para>⚠️ والمدخل خام: ممكن يكون كود عاري، أو حمولة ليبل كاملة
/// بالمواصفات، أو رابط قديم، أو نص عشوائي من QR بتاع حاجة تانية.
/// <c>DeviceScan.Parse</c> هو اللي بيحكم.</para>
/// </summary>
public sealed record LookupDeviceQuery(string? Code) : IRequest<Result<DeviceLookupResult>>;
