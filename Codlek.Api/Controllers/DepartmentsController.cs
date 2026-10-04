using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Departments;
using Codlek.Application.Features.Departments.CreateDepartment;
using Codlek.Application.Features.Departments.GetDepartments;
using Codlek.Application.Features.Departments.UpdateDepartment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/departments</c> — أقسام الورشة.
///
/// <para>⚠️ <b>نفس عناوين المشروع القديم بالحرف.</b> الداش بورد
/// الحالية بتنده المسارات دي، فالتحويل مايحتاجش تعديل فيها.</para>
/// </summary>
[ApiController]
[Route("api/v1/departments")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class DepartmentsController(ISender sender) : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType<IReadOnlyList<DepartmentRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetDepartmentsQuery(), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("")]
    [ProducesResponseType<DepartmentRow>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] SaveDepartmentRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateDepartmentCommand(
                body.Name ?? "", body.Code ?? "", body.SortOrder ?? 0), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<DepartmentRow>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] SaveDepartmentRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateDepartmentCommand(
                id, body.Name, body.Code, body.SortOrder, body.IsActive), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
