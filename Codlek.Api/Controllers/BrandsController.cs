using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Brands;
using Codlek.Application.Features.Brands.AddAlias;
using Codlek.Application.Features.Brands.CreateBrand;
using Codlek.Application.Features.Brands.GetBrands;
using Codlek.Application.Features.Brands.GetUnknownBrands;
using Codlek.Application.Features.Brands.RemoveAlias;
using Codlek.Application.Features.Brands.UpdateBrand;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/brands</c> — ماركات اللابات.
///
/// <para>🔴 <b>القايمة دي شرط لقاعدة «الفني ده لماركات معيّنة».</b>
/// اسم الماركة في جدول الأجهزة خام زي ما ويندوز قاله، ونفس الشركة
/// بتيجي <c>HP</c> و<c>Hewlett-Packard</c>. منع مبني على الاسم الخام
/// بيعدّي عليه لابات في صمت.</para>
///
/// <para>⚠️ <b>والقايمة مش سلطة.</b> لاب ماركته مش هنا بيعدّي عادي.
/// واللي بيحمي ده هو <c>/unknown</c>: بتوري الأسماء اللي لسه مش في
/// القايمة بعددها عشان تتضاف بضغطة، بدل ما تفضل مخبّية.</para>
/// </summary>
[ApiController]
[Route("api/v1/brands")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class BrandsController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<BrandRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetBrandsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// الأسماء اللي في الأجهزة ومالهاش ماركة.
    ///
    /// <para>⚠️ <b>المسار ده قبل <c>{id:guid}</c>؟</b> مش مشكلة هنا:
    /// <c>unknown</c> مش <c>Guid</c>، فالقيد <c>:guid</c> بيمنع
    /// التلخبط. ولو حد شال القيد، <c>/unknown</c> كان هيتقرا كمعرّف
    /// ويرجّع ٤٠٠.</para>
    /// </summary>
    [HttpGet("unknown")]
    [ProducesResponseType<IReadOnlyList<UnknownBrandRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unknown(CancellationToken ct)
    {
        var result = await sender.Send(new GetUnknownBrandsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("")]
    [ProducesResponseType<BrandRow>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] SaveBrandRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateBrandCommand(body.Name ?? "", body.SortOrder ?? 0), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<BrandRow>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] SaveBrandRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateBrandCommand(id, body.Name, body.SortOrder, body.IsActive), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{id:guid}/aliases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddAlias(
        Guid id, [FromBody] SaveAliasRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new AddAliasCommand(id, body.Value ?? ""), ct);

        return result.IsSuccess ? Ok(new { message = "اتضاف." }) : result.ToProblem();
    }

    /// <summary>
    /// شيل اسم بديل.
    ///
    /// <para>⚠️ <b>الاسم في المسار، والمسار ممكن يكون فيه حروف
    /// عربية أو مسافات.</b> ASP.NET بيفك ترميز الـURL لوحده، والبحث
    /// بيحصل على الشكل المطبَّع — فالشكل اللي الواجهة بتبعته مش
    /// مهم.</para>
    /// </summary>
    [HttpDelete("{id:guid}/aliases/{value}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAlias(
        Guid id, string value, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveAliasCommand(id, value), ct);

        return result.IsSuccess ? Ok(new { message = "اتشال." }) : result.ToProblem();
    }
}
