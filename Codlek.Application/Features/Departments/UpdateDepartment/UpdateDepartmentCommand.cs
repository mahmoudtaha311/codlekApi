using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using MediatR;

namespace Codlek.Application.Features.Departments.UpdateDepartment;

/// <summary>
/// تعديل قسم.
///
/// <para>⚠️ <b>كل الحقول nullable.</b> التعديل بيبعت اللي اتغيّر بس،
/// و<c>false</c> جاي من حقل ناقص كان هيوقف أقسام شغّالة.</para>
/// </summary>
public sealed record UpdateDepartmentCommand(
    Guid Id,
    string? Name,
    string? Code,
    int? SortOrder,
    bool? IsActive) : IRequest<Result<DepartmentRow>>;
