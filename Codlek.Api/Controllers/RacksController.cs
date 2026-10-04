using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Features.Racks.CreateActivationCode;
using Codlek.Application.Features.Racks.DeleteActivationCode;
using Codlek.Application.Features.Racks.GetActivationCodes;
using Codlek.Application.Features.Racks.GetStations;
using Codlek.Application.Features.Racks.RevokeStation;
using Codlek.Application.Features.Racks.SetStationStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/racks</c> — إدارة محطات الفحص.
///
/// <para>🔴 <b>المالك بس — كل النقط.</b> اللي بيقدر يعمل كود تفعيل
/// بيقدر يضيف محطة تقرا وترفع في الورشة، واللي بيقدر يلغي بيقدر
/// يوقّف خط الفحص كله. ودي مش صلاحية مدير.</para>
///
/// <para>⚠️ <b>ودي إدارة المحطات، مش المحطات نفسها.</b> تسجيل
/// الراكة ورفع الفحوص بيتحققوا <b>بمفتاح محطة</b> مش بتوكن مستخدم،
/// وعايشين تحت <c>/api/v2/*</c> و<c>/api/sync/reports</c> — مرحلة
/// لوحدها.</para>
///
/// <para>🔴 <b>والمسارات دي <u>مجمّدة حرفياً</u>، مش قابلة لإعادة
/// تسمية.</b> تطبيق الراكة شايلها كنصوص ثابتة جوّه البرنامج
/// المنزّل على الراكات في الميدان (<c>spics/Sync/SyncWorker.cs</c>
/// و<c>RackStore.cs</c> و<c>RepairPull.cs</c>)، واللي بييجي من
/// الإعدادات هو <b>العنوان الأساسي بس</b>. فأي بادئة أنضف
/// (<c>/api/v1/rack/*</c>) تبقى تسمية داخلية وبس — والمسار على
/// السلك لازم يفضل زي ما هو.</para>
///
/// <para>⚠️ <b>و<c>activation-codes</c> قبل <c>{id:guid}</c> في
/// الترتيب مش مشكلة:</b> القيد <c>:guid</c> بيخلّي المسار التاني
/// مايقبلش نص — والمقطع الثابت بيكسب على المتغيّر في توجيه السمات
/// أصلاً. بس الترتيب مكتوب كده عشان اللي بيقرا مايحتاجش يعرف
/// الحكاية دي.</para>
/// </summary>
[ApiController]
[Route("api/v1/racks")]
[Authorize(Policies.OwnerOnly)]
public sealed class RacksController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<StationListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetStationsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    // =================================================================
    //  أكواد التفعيل
    // =================================================================

    [HttpGet("activation-codes")]
    [ProducesResponseType<IReadOnlyList<ActivationCodeRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Codes(CancellationToken ct)
    {
        var result = await sender.Send(new GetActivationCodesQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// 🔴 <b>الرد فيه الكود الكامل — مرة واحدة وبس.</b> بعد كده
    /// بصمته هي اللي في القاعدة، ومفيش نقطة بترجّعه تاني.
    /// </summary>
    [HttpPost("activation-codes")]
    [ProducesResponseType<ActivationCodeIssued>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCode(
        [FromBody] CreateActivationCodeRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateActivationCodeCommand(body.Name, body.Location), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// مسح كود تفعيل — <b>المنتهي اللي مااتستعملش بس</b>.
    ///
    /// <para>⚠️ <b>واللوحة بتبعت جسم فاضي (<c>{}</c>) مع
    /// <c>DELETE</c>.</b> مفيش معامل جسم هنا فالجسم بيتجاهل — بس
    /// إضافة <c>[FromBody]</c> في أي وقت جاي هتخلّي الطلب ده يرجّع
    /// <c>400</c>، لأن <c>{}</c> مابيملاش حقل إجباري.</para>
    /// </summary>
    [HttpDelete("activation-codes/{id:guid}")]
    [ProducesResponseType<RackActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCode(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteActivationCodeCommand(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    // =================================================================
    //  حالة المحطة
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الإيقاف مش بيمسح المفتاح.</b> المحطة بترجع بدوسة زرار،
    /// والفحوص اللي عندها بتفضل في طابورها وبترفع لما ترجع.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType<RackActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetStationStatusCommand(id, Resume: false), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<RackActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetStationStatusCommand(id, Resume: true), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// إلغاء نهائي — <b>المفتاح مبقاش ينفع والمحطة محتاجة كود تفعيل
    /// جديد</b>.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType<RackActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid id, [FromBody] RevokeStationRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new RevokeStationCommand(id, body.Reason), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
