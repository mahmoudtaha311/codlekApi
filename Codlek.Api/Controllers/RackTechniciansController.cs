using Codlek.Api.Racks;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Features.Rack.TechnicianChangePassword;
using Codlek.Application.Features.Rack.TechnicianLogin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Codlek.Api.Controllers;

/// <summary>
/// دخول الفني من محطة فحص، وتغيير باسورده.
///
/// <para>🔴 <b>والترتيب هو الأمان نفسه:</b> مفتاح المحطة ← المحطة
/// ← شركتها ← الفني جوّه الشركة دي. الطلب <b>مالوش</b> شركة، فالدخول
/// العابر للشركات مش «ممنوع» — هو مش موجود كمسار.</para>
///
/// <para>🔴 <b>والردود كلها <c>{code, message}</c> مش
/// <c>ProblemDetails</c>.</b> الراكة بتتفرّع على <c>code</c>
/// وبتعرض رسالة محلية بيه.</para>
/// </summary>
[ApiController]
[AllowAnonymous]
[RackKey]
public sealed class RackTechniciansController(
    ISender sender, IOptions<RackServerOptions> server) : ControllerBase
{
    public sealed record LoginBody(string? Username, string? Password);

    public sealed record ChangeBody(
        string? Username,
        string? CurrentPassword,
        string? NewPassword,
        string? ConfirmPassword);

    /// <summary>
    /// <c>POST /api/v2/technicians/login</c>
    ///
    /// <para>🔴 <b>والقفل بيرجع <c>429</c> — مش <c>401</c>.</b>
    /// الراكة بتحسب <c>401</c>/<c>403</c> «رفض قاطع» وبتوقف عندهم
    /// خالص، أما <c>429</c> بتحسبها «السيرفر تعبان» وبتسمح بالدخول
    /// من النسخة المحفوظة. يعني <c>401</c> على القفل معناها إن فني
    /// كتب باسورده غلط عشر مرات في ورشة نتها قاطع <b>مابيقدرش يدخل
    /// خالص</b>.</para>
    /// </summary>
    [HttpPost("api/v2/technicians/login")]
    [EnableRateLimiting(RackRateLimits.TechnicianLogin)]
    [ProducesResponseType<TechnicianLoggedIn>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        var result = await sender.Send(
            new TechnicianLoginCommand(
                rack.TenantId, rack.Id, rack.RackCode,
                body.Username, body.Password,
                server.Value.OfflineTechnicianValidityDays), ct);

        return result.IsSuccess ? Ok(result.Value) : Rejected(result.Error);
    }

    /// <summary>
    /// <c>POST /api/v2/technicians/change-password</c>
    ///
    /// <para>⚠️ <b>وإضافة بحتة:</b> الراكات القديمة في الميدان
    /// مابتعرفش النقطة دي ومابتندهاش عليها، فالسيرفر بينزل الأول
    /// والراكة بعده.</para>
    ///
    /// <para>🔴 <b>وسياسة الباسورد الجديد <c>400</c> مش
    /// <c>401</c>.</b> لو رجعت <c>401</c>، الشاشة بتعرض «الباسورد
    /// الحالي غلط» على باسورد حالي صح — وده بالظبط العطل اللي النقطة
    /// دي اتعملت عشانه.</para>
    /// </summary>
    [HttpPost("api/v2/technicians/change-password")]
    [EnableRateLimiting(RackRateLimits.TechnicianLogin)]
    [ProducesResponseType<TechnicianPasswordChanged>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeBody body, CancellationToken ct)
    {
        var rack = HttpContext.Rack();

        var result = await sender.Send(
            new TechnicianChangePasswordCommand(
                rack.TenantId, rack.Id, rack.RackCode,
                body.Username, body.CurrentPassword,
                body.NewPassword, body.ConfirmPassword), ct);

        return result.IsSuccess ? Ok(result.Value) : Rejected(result.Error);
    }

    /// <summary>
    /// ⚠️ <b>الجسم <c>{code, message}</c> — والكود هو اللي الراكة
    /// بتتفرّع عليه.</b> <c>ProblemDetails</c> فيه <c>title</c> مش
    /// <c>message</c>، فالراكة بتلاقي جسم مش فاهمه.
    /// </summary>
    private IActionResult Rejected(Application.Abstractions.Error error) =>
        StatusCode(
            error.StatusCode ?? StatusCodes.Status401Unauthorized,
            new { code = error.Code, message = error.Description });
}
