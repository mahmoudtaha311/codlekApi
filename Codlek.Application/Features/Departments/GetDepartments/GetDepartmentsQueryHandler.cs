using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Departments.GetDepartments;

public sealed class GetDepartmentsQueryHandler(
    IDepartmentRepository departments,
    ICurrentUser me)
    : IRequestHandler<GetDepartmentsQuery, Result<IReadOnlyList<DepartmentRow>>>
{
    public async Task<Result<IReadOnlyList<DepartmentRow>>> Handle(
        GetDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var rows = await departments.ListAsync(me.TenantId, cancellationToken);

        return Result.Success(rows);
    }
}
