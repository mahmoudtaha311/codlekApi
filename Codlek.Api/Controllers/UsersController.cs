using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Features.Users.ActivateUser;
using Codlek.Application.Features.Users.CreateUser;
using Codlek.Application.Features.Users.GetUsers;
using Codlek.Application.Features.Users.ResetUserPassword;
using Codlek.Application.Features.Users.SuspendUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/users</c> — حسابات <b>الدخول للوحة التحكم</b>.
///
/// <para>🔴 <b>فيه جدولين اسمهم بيتلخبط، ودي مش تفصيلة.</b></para>
///
/// <list type="bullet">
///   <item><c>/api/v1/technician-accounts</c> بيدير حسابات
///     <b>الراكة</b>. الفني بيدخل بيها على محطة الفحص، مش على
///     الموقع. اسمها فريد <b>جوّه الشركة</b> لأن المحطة متحققة
///     بمفتاحها فالشركة معروفة قبل ما الاسم يتقرا.</item>
///
///   <item><c>/api/v1/users</c> (الملف ده) بيدير حسابات <b>لوحة
///     التحكم</b>. بيدخل بيها من صفحة فيها اسم وباسورد وبس، فالاسم
///     لازم يبقى فريد <b>على مستوى النظام كله</b>.</item>
/// </list>
///
/// <para>⚠️ <b>والخلط بينهم بيعمل عطل صامت.</b> «اعمل حساب لأحمد»
/// بيتنفّذ في الجدول الغلط، والحساب بيتعمل بنجاح — وأحمد مش عارف يدخل
/// من المكان اللي هو محتاجه، والصفحة بتعرضه موجود. حتى
/// <c>UserRole.Technician</c> هنا هو <b>دور موقع</b>، مالوش علاقة
/// بجدول الفنيين خالص.</para>
///
/// <para>⚠️ <b>والسياسة على الكنترولر كله <c>ManagerOrAbove</c>:</b>
/// الفني عمره ما يشوف القايمة أصلاً. ومدير الدور بيعدّي الحاجز ده —
/// وبيتقفل <b>جوّه كل نقطة</b> بـ<c>UserManagementRules.CanManage</c>،
/// مش هنا.</para>
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<UserAccountItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetUsersQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("")]
    [ProducesResponseType<UserAccountResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateUserCommand(
                body.Username ?? "", body.DisplayName ?? "",
                body.Password ?? "", body.Role), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/password")]
    [ProducesResponseType<UserAccountResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id, [FromBody] ResetUserPasswordRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new ResetUserPasswordCommand(id, body.Password ?? ""), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType<UserAccountResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(
        Guid id, [FromBody] SuspendUserRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SuspendUserCommand(id, body.Reason ?? ""), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<UserAccountResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateUserCommand(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
