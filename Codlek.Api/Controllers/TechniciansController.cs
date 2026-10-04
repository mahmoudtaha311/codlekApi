using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Features.Technicians.GetProductivity;
using Codlek.Application.Features.Technicians.GetTechnicianDetail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/technicians</c> — إنتاجية الفنيين.
///
/// <para>⚠️ <b>الفني هنا هوية <u>محطة</u>، مش حساب موقع.</b>
/// والمفتاح <b>كود</b> مش <c>Guid</c> — ده اللي الراكة بتكتبه على
/// الفحص، وفيه فنيين مالهمش حساب في اللوحة خالص.</para>
///
/// <para>🔴 <b>وإدارة الحسابات مسار مستقل</b>
/// (<c>/technician-accounts</c>): ده بيرجّع <b>إنتاجية</b> محسوبة
/// من الفحوص، وده بيرجّع <b>حسابات</b> ومفتاحه معرّف الصف. ولو
/// اتحطوا تحت نفس المجموعة، <c>/technicians/accounts</c> كانت
/// هتتلخبط مع <c>/technicians/{code}</c> — نفس الشكل، ومعنيين
/// مختلفين.</para>
/// </summary>
[ApiController]
[Route("api/v1/technicians")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class TechniciansController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<TechnicianListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Productivity(
        [FromQuery] string? range,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetProductivityQuery(range, from, to, search, sort), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ⚠️ <b>المسار بالكود، من غير قيد نوع.</b> الكود نص حر من
    /// الراكة (<c>T001</c> · <c>100001</c>) — وقيد <c>:guid</c> أو
    /// <c>:int</c> كان بيخلّي نص الفنيين مش قابلين للوصول.
    /// </summary>
    [HttpGet("{code}")]
    [ProducesResponseType<TechnicianDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(
        string code,
        [FromQuery] string? range,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetTechnicianDetailQuery(code, range, from, to), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
