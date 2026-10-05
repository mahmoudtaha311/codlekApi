using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Account;
using Codlek.Application.Features.Account.ChangePassword;
using Codlek.Application.Features.Account.GetAccount;
using Codlek.Application.Features.Account.UpdateProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/account</c> — صفحة «حسابي».
///
/// <para>🔴 <b>القاعدة الحاكمة في الملف كله: المستخدم بييجي من
/// التوكن، مش من الطلب.</b> مفيش نقطة هنا بتاخد <c>userId</c> ولا
/// <c>tenantId</c> — لا في المسار ولا في الـquery ولا في الجسم. ده مش
/// تفضيل أسلوب: أول ما معرّف مستخدم يُقبل من العميل، الفرق بين «عدّل
/// حسابي» و«عدّل حساب المالك» بيبقى سطر تحقق واحد، وأي غلطة فيه بتبقى
/// ترقية صلاحيات.</para>
///
/// <para>⚠️ <b>ومفيش سياسة دور هنا.</b> الصفحة دي بتاعة صاحبها أياً
/// كان دوره — <c>[Authorize]</c> بتفرض الدخول وبس. والفني ليه شاشته
/// التانية؛ دمج الاتنين كان هيخلّي «غيّر كلمة المرور» سؤال غامض:
/// بتاعة مين؟</para>
/// </summary>
[ApiController]
[Route("api/v1/account")]
[Authorize]
public sealed class AccountController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetAccountQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// تعديل اسم العرض.
    ///
    /// <para>⚠️ <c>PATCH</c> مش <c>PUT</c> — نفس القديم. التعديل
    /// جزئي: حقل واحد، والباقي مابيتلمسش.</para>
    /// </summary>
    [HttpPatch("profile")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateProfileCommand(body.DisplayName ?? ""), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// تغيير كلمة المرور — <b>والرد فيه توكنات جديدة</b>.
    ///
    /// <para>🔴 التغيير بيقفل كل الجلسات (بما فيها بتاعة الجهاز ده)،
    /// فالتوكنات الجديدة في الرد هي اللي بتخلّي اللي غيّر يفضل داخل.
    /// <b>والواجهة لازم تستبدل اللي عندها بيهم</b> — لو سابت القديم،
    /// المستخدم بيتطرد من أول طلب بعدها (النسخة اتغيّرت — شوف
    /// <c>AccountStanding</c>).</para>
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType<PasswordChangedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new ChangePasswordCommand(
                body.CurrentPassword ?? "",
                body.NewPassword ?? "",
                body.ConfirmPassword ?? ""), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
