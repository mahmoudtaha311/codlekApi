using System.Text.Json;
using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Features.TechnicianAccounts.ActivateTechnician;
using Codlek.Application.Features.TechnicianAccounts.CreateTechnician;
using Codlek.Application.Features.TechnicianAccounts.GetAccounts;
using Codlek.Application.Features.TechnicianAccounts.ResetPassword;
using Codlek.Application.Features.TechnicianAccounts.SetBrands;
using Codlek.Application.Features.TechnicianAccounts.SuspendTechnician;
using Codlek.Application.Features.TechnicianAccounts.UpdateTechnician;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/technician-accounts</c> — إدارة حسابات الفنيين.
///
/// <para>🔴 <b>ليه مسار مستقل عن <c>/technicians</c>.</b> المسار
/// التاني بيرجّع <b>إنتاجية</b> محسوبة من الفحوص ومفتاحه كود الفني؛
/// وده بيرجّع <b>حسابات</b> ومفتاحه معرّف الصف. ولو اتحطوا تحت نفس
/// المجموعة، <c>/technicians/accounts</c> كانت هتتلخبط مع
/// <c>/technicians/{code}</c> — نفس الشكل، ومعنيين مختلفين.</para>
///
/// <para>⚠️ <b>والشركة بتيجي من التوكن دايماً.</b> مفيش أي مسار هنا
/// بياخد معرّف شركة من الطلب — ولو أخده، مدير شركة كان هيقدر يعمل
/// فني في شركة تانية.</para>
///
/// <para>⚠️ <b>ودخول الفني وتغييره لباسورده بنفسه مش هنا</b> — دي
/// نقط الراكة (<c>/api/v1/rack/*</c>) وهي مرحلة لوحدها: بتتحقق
/// بمفتاح محطة مش بتوكن مستخدم.</para>
/// </summary>
[ApiController]
[Route("api/v1/technician-accounts")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class TechnicianAccountsController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<TechnicianAccount>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetTechnicianAccountsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// 🔴 <b>الرد فيه الباسورد الأولي — مرة واحدة وبس.</b> المدير
    /// بيسلّمه للفني، وبعدها مش موجود في أي مكان.
    /// </summary>
    [HttpPost("")]
    [ProducesResponseType<TechnicianSecretResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTechnicianRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateTechnicianCommand(
                body.DisplayName, body.Username, body.Password,
                body.Specialty, body.DepartmentId), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id:guid}/brands")]
    [ProducesResponseType<TechnicianBrandsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Brands(
        Guid id, [FromBody] SetTechnicianBrandsRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SetTechnicianBrandsCommand(id, body.BrandIds), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/password")]
    [ProducesResponseType<TechnicianSecretResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Password(
        Guid id, [FromBody] ResetTechnicianPasswordRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new ResetTechnicianPasswordCommand(id, body.Password), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType<TechnicianActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(
        Guid id, [FromBody] SuspendTechnicianRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SuspendTechnicianCommand(id, body.Reason), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<TechnicianActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateTechnicianCommand(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// 🔴 <b>الجسم بيتقرا خام عشان نفرّق بين «الحقل مااتبعتش»
    /// و«اتبعت فاضي».</b>
    ///
    /// <para>JSON مابيفرّقش بينهم في النوع: الاتنين بيوصلوا
    /// <c>null</c>. والقسم محتاج التفريق ده — «ماتلمسش» مقابل «شيل
    /// القسم» — فبنقرا أسماء الخصائص الموجودة فعلاً في الجسم.</para>
    ///
    /// <para>⚠️ <b>والحقول التانية مالهاش المشكلة دي:</b> التخصص
    /// والصلاحيات قيمتهم الفاضية معناها «ماتلمسش» وبس، ومفيش فيهم
    /// «شيل».</para>
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TechnicianActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] JsonElement raw, CancellationToken ct)
    {
        if (raw.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "الطلب لازم يكون كائن JSON" });

        var body = raw.Deserialize<UpdateTechnicianRequest>(JsonOptions);

        if (body is null)
            return BadRequest(new { message = "الطلب لازم يكون كائن JSON" });

        /*
          🔴 **الحقل اتبعت؟ لو أيوه وقيمته فاضية، ده «شيل
          القسم».**

          والقيمة الحارسة بتعدّي للمعالج لأن `null` هناك محجوزة
          لـ«ماتلمسش».
        */
        Guid? department = Sent(raw, "departmentId")
            ? body.DepartmentId ?? Guid.Empty
            : null;

        var result = await sender.Send(
            new UpdateTechnicianCommand(
                id, body.DisplayName, body.Specialty, department,
                body.CanTest, body.CanRepair), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ⚠️ المقارنة بتتجاهل حالة الأحرف — <c>DepartmentId</c>
    /// و<c>departmentId</c> الاتنين بيوصلوا من عملاء مختلفين.
    /// </summary>
    private static bool Sent(JsonElement body, string name)
    {
        foreach (var property in body.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// ⚠️ نفس خيارات المُسلسِل بتاعة التطبيق — الأسماء camelCase،
    /// فقراية الجسم بأي خيارات تانية كانت بترجّع حقول فاضية.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// جسم التعديل — <b>كله اختياري ما عدا الاسم</b>.
    /// </summary>
    public sealed record UpdateTechnicianRequest(
        string? DisplayName,
        int? Specialty,
        Guid? DepartmentId,
        bool? CanTest = null,
        bool? CanRepair = null);
}
