using System.Security.Claims;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Features.Auth.Login;
using Codlek.Application.Features.Auth.Logout;
using Codlek.Application.Features.Auth.Refresh;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>الدخول والتجديد والخروج.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new LoginCommand(request.Username, request.Password), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RefreshCommand(request.RefreshToken), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// خروج — بيقفل كل جلسات الحساب.
    ///
    /// <para>🔴 <b>المستخدم بييجي من التوكن، مش من الطلب.</b> لو
    /// الطلب قال مين يخرج، أي حد معاه توكن سليم كان يقدر يطرد أي
    /// حساب تاني بإنه يبعت معرّفه.</para>
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out Guid userId))
            return Unauthorized();

        var result = await sender.Send(new LogoutCommand(userId), ct);

        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    /// <summary>
    /// مين أنا — بيتقرا من التوكن.
    ///
    /// <para>⚠️ الواجهة بتندهه بعد إعادة تحميل الصفحة عشان تعرف
    /// الحالة من غير ما تخزّن بيانات المستخدم في المتصفح.</para>
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        UserId = User.FindFirstValue("sub"),
        DisplayName = User.FindFirstValue("display"),
        Code = User.FindFirstValue("code"),
        Role = User.FindFirstValue(ClaimTypes.Role),
        TenantId = User.FindFirstValue("tenant"),
        MustChangePassword = User.FindFirst("must") is not null,
    });
}
