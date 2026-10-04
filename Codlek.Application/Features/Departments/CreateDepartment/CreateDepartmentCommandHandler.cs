using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using MediatR;

namespace Codlek.Application.Features.Departments.CreateDepartment;

public sealed class CreateDepartmentCommandHandler(
    IDepartmentRepository departments,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<CreateDepartmentCommand, Result<DepartmentRow>>
{
    public async Task<Result<DepartmentRow>> Handle(
        CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        string name = command.Name.Trim();

        if (await departments.NameTakenAsync(me.TenantId, name, null, cancellationToken))
            return Result.Failure<DepartmentRow>(DepartmentErrors.NameTaken(name));

        var department = new Department
        {
            TenantId = me.TenantId,
            Name = name,
            Code = command.Code.Trim(),
            SortOrder = command.SortOrder,
            IsActive = true,
        };

        departments.Add(department);

        audit.Record(
            AuditActions.DepartmentCreated, "Department",
            department.Id, department.Code,
            $"اتعمل قسم «{department.Name}»");

        // ⚠️ حفظة واحدة للقسم والسجل مع بعض — راجع `IAuditTrail`.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new DepartmentRow(
            department.Id, department.Code, department.Name,
            department.IsActive, department.SortOrder, 0));
    }
}
