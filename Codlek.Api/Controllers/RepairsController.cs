using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Features.Repairs.ApproveRepair;
using Codlek.Application.Features.Repairs.AssignRepair;
using Codlek.Application.Features.Repairs.CancelRepair;
using Codlek.Application.Features.Repairs.GetAssignableTechnicians;
using Codlek.Application.Features.Repairs.GetPendingRepairs;
using Codlek.Application.Features.Repairs.GetRepairDetail;
using Codlek.Application.Features.Repairs.GetRepairs;
using Codlek.Application.Features.Repairs.OpenRepair;
using Codlek.Application.Features.Repairs.OverrideStartRepair;
using Codlek.Application.Features.Repairs.RejectRepair;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/repairs</c> — أوامر الصيانة.
///
/// <para>🔴 <b>السياسة على كل إجراء لوحده، و<c>[Authorize]</c> على
/// الكلاس من غير سياسة.</b> ASP.NET بيجمع سياسة الكلاس وسياسة
/// الإجراء بـ<b>و</b> مش بـ<b>أو</b>. فلو حطّينا
/// <c>ManagerOrAbove</c> على الكلاس، المحاسب بياخد <c>403</c> على
/// <c>GET /repairs</c> — وهو **لازم** يقراها عشان يوافق.</para>
///
/// <para>⚠️ <b>والمشروع القديم كان بيعمل كده بتلات مجموعات مسارات
/// منفصلة على نفس البادئة</b> — واحدة للقراية وواحدة للكتابة
/// وواحدة للموافقة. التقسيم ده عقد أمني مش ترتيب كود.</para>
/// </summary>
[ApiController]
[Route("api/v1/repairs")]
[Authorize]
public sealed class RepairsController(ISender sender) : ControllerBase
{
    // =================================================================
    //  القراية — المحاسب داخل
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الفلاتر كلها «أحسن مجهود».</b> قيمة مش مفهومة بتتجاهل
    /// ومابتردّش <c>400</c> — الداش بورد بتحفظ الفلاتر في الرابط.
    /// </summary>
    [HttpGet("")]
    [Authorize(Policies.RepairsViewer)]
    [ProducesResponseType<PagedResult<RepairListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? approval,
        [FromQuery] Guid? technician,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRepairsQuery(
                search, status, approval, technician, from, to, sort, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// طابور قرار المحاسب.
    ///
    /// <para>🔴 <b>لازم يتسجّل قبل <c>{id:guid}</c>؟ لأ — القيد
    /// <c>:guid</c> هو اللي بيحمي.</b> كلمة <c>pending</c> مش
    /// <c>Guid</c> فمابتتلبسش على المسار التاني. وشيل القيد بيخلّي
    /// الطابور يروح لمعالج التفاصيل ويرجّع «مش موجود».</para>
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policies.RepairsViewer)]
    [ProducesResponseType<IReadOnlyList<PendingRepairRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var result = await sender.Send(new GetPendingRepairsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// الفنيين اللي ينفع ياخدوا صيانة.
    ///
    /// <para>🔴 <b>مجموعة القراية مش المديرين — وده مقصود.</b>
    /// المحاسب بيوافق <b>ويغيّر الفني</b> في نفس الخطوة، فالقايمة
    /// دي هي مصدر الأسماء اللي بيختار منها. تضييقها
    /// لـ<c>ManagerOrAbove</c> بيخلّي شاشة الموافقة بتاعته
    /// فاضية.</para>
    /// </summary>
    [HttpGet("technicians")]
    [Authorize(Policies.RepairsViewer)]
    [ProducesResponseType<IReadOnlyList<RepairTechnicianOption>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Technicians(CancellationToken ct)
    {
        var result = await sender.Send(new GetAssignableTechniciansQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policies.RepairsViewer)]
    [ProducesResponseType<RepairDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRepairDetailQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    // =================================================================
    //  الكتابة — المحاسب برّه
    // =================================================================

    [HttpPost("")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<OpenedRepairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Open(
        [FromBody] OpenRepairRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new OpenRepairCommand(
                body.DeviceId,
                body.SourceReportId,
                body.AssignTechnicianId,
                body.FaultSummary,
                body.RequiredSpecialty), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ⚠️ <b>الحاجز هنا سياسة، والتعدية خاصية.</b> المدير بيعدّي
    /// السياسة؛ وتعدية قيد الماركة بتتقرا جوّه المعالج من
    /// <c>ICurrentUser.IsRepairApprover</c>.
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Policies.RepairAssigner)]
    [ProducesResponseType<RepairActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        Guid id, [FromBody] AssignRepairRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new AssignRepairCommand(id, body.TechnicianId), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<RepairActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] CancelRepairRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CancelRepairCommand(id, body.Reason), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// التجاوز الإداري لبدء الشغل.
    ///
    /// <para>🔴 <b><c>reason</c> من سلسلة الاستعلام مش من الجسم.</b>
    /// الداش بورد بتنده
    /// <c>/repairs/{id}/override/start?reason=…</c> دلوقتي. ونقله
    /// للجسم بيكسر النقطة <b>في صمت</b>: السبب بيوصل فاضي، والرد
    /// بيبقى «التجاوز الإداري محتاج سبب مكتوب» وكأن المستخدم
    /// نسيه.</para>
    ///
    /// <para>⚠️ <b>والسياسة أوسع من الحاجز الحقيقي عن قصد.</b>
    /// السياسة <c>ManagerOrAbove</c>، والمعالج بيضيّق على المالك
    /// ويرجّع <c>403</c>. تضييقها لـ<c>OwnerOnly</c> بيغيّر أنهي طبقة
    /// بترد — وبالتالي شكل الرد نفسه.</para>
    /// </summary>
    [HttpPost("{id:guid}/override/start")]
    [Authorize(Policies.ManagerOrAbove)]
    [ProducesResponseType<RepairActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OverrideStart(
        Guid id,
        [FromBody] StartRepairRequest body,
        [FromQuery] string? reason,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new OverrideStartRepairCommand(id, body.TechnicianId, reason), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    // =================================================================
    //  القرار — المحاسب والمالك بس
    // =================================================================

    /// <summary>
    /// موافقة — <b>ومعاها تغيير الفني لو اتبعت</b>.
    ///
    /// <para>🔴 <b>المدير برّه.</b> السياسة <c>RepairApprover</c> =
    /// محاسب + مالك. والمدير هو اللي الموافقة موجودة عشان
    /// <b>تراجعه</b>، فإدخاله هنا بيلغي الميزة كلها.</para>
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policies.RepairApprover)]
    [ProducesResponseType<RepairActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(
        Guid id, [FromBody] ApproveRepairRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new ApproveRepairCommand(id, body.TechnicianId, body.Note), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// رفض — <b>نفس جسم الموافقة، و<c>TechnicianId</c> بيتجاهل</b>.
    ///
    /// <para>⚠️ ده مش سهو: الشاشة بتبعت نفس الجسم من نفس النموذج،
    /// وإضافة نوع تاني كانت بتخلّي الواجهة تفرّق بين النداءين من غير
    /// داعي.</para>
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policies.RepairApprover)]
    [ProducesResponseType<RepairActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] ApproveRepairRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new RejectRepairCommand(id, body.Note), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
