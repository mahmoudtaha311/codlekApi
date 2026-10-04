using Codlek.Api.Extensions;
using Codlek.Api.Racks;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Features.Rack.RegisterRack;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>POST /api/v2/racks/register</c> — تسجيل محطة فحص.
///
/// <para>🔴 <b>النقطة الوحيدة في سطح الراكة اللي بتقبل كتابة من
/// غير مفتاح</b> — لأن المحطة الجديدة <b>مالهاش مفتاح لسه</b>.
/// واللي بيحميها هو كود التفعيل وحدّ الطلبات.</para>
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class RackPairingController(
    ISender sender, CloudAddresses cloud) : ControllerBase
{
    /// <summary>
    /// ⚠️ <b>الجسم فيه الكود وبس — مفيش معرّف شركة.</b> الكود هو
    /// اللي بيحدّد الشركة، ولو الطلب أخدها كان أي حد يقدر يسجّل
    /// محطة في أي شركة.
    /// </summary>
    public sealed record Body(
        string? PairingCode,
        string? RackName,
        string? InstallationId,
        string? MachineIdentifier,
        string? AppVersion);

    [HttpPost("api/v2/racks/register")]
    [EnableRateLimiting(RackRateLimits.RackRegister)]
    [ProducesResponseType<RackRegistered>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Register(
        [FromBody] Body body, CancellationToken ct)
    {
        var result = await sender.Send(
            new RegisterRackCommand(
                body.PairingCode,
                body.RackName,
                body.InstallationId,
                body.MachineIdentifier,
                body.AppVersion,

                // ⚠️ العنوان من الاتصال — مش من ترويسة.
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",

                // 🔴 ورابط المزامنة من الإعدادات.
                cloud.SyncUrl), ct);

        /*
          🔴 **والفشل بيرجع `{code, message}` مش `ProblemDetails`.**

          الراكة بتقرا `code` من الجسم وبتعرض رسالة محلية بيه —
          و`ProblemDetails` فيه `title` مش `message`، فالراكة
          بتلاقي جسم مش فاهمه وبتعرض رسالة عامة.
        */
        if (result.IsFailure)
        {
            return StatusCode(
                result.Error.StatusCode ?? 400,
                new { code = result.Error.Code, message = result.Error.Description });
        }

        // ⚠️ ٢٠١ مش ٢٠٠ — نفس القديم.
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
