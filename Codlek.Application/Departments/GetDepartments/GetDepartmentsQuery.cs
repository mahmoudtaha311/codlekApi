using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using MediatR;

namespace Codlek.Application.Departments.GetDepartments;

/// <summary>
/// قايمة أقسام الشركة.
///
/// <para>⚠️ <b>مفيش <c>TenantId</c> في الاستعلام.</b> الشركة بتتقرا من
/// التوكن — راجع <c>ICurrentUser</c>.</para>
/// </summary>
public sealed record GetDepartmentsQuery : IRequest<Result<IReadOnlyList<DepartmentRow>>>;
