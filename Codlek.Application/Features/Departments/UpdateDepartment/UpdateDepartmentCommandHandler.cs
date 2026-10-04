using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Departments.UpdateDepartment;

public sealed class UpdateDepartmentCommandHandler(
    IDepartmentRepository departments,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<UpdateDepartmentCommand, Result<DepartmentRow>>
{
    public async Task<Result<DepartmentRow>> Handle(
        UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var department = await departments.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (department is null)
            return Result.Failure<DepartmentRow>(DepartmentErrors.NotFound);

        /*
          ⚠️ **الاسم الفاضي معناه «ماتغيّرش»، مش «فضّيه».**

          نفس سلوك المشروع القديم بالحرف. والسبب إن الواجهة بتبعت
          الحقل اللي اتغيّر بس — فنص فاضي من حقل مابعتوش كان بيمسح
          اسم القسم.
        */
        string name = (command.Name ?? "").Trim();

        if (name.Length > 0)
        {
            // 🔴 **والتحقق من التكرار لازم يستني القسم ده نفسه.**
            //
            // من غير `exceptId`، أي حفظ من غير تغيير الاسم بيترفض
            // بـ«الاسم موجود خلاص» — والاسم الموجود هو اسمه هو.
            if (await departments.NameTakenAsync(
                    me.TenantId, name, department.Id, cancellationToken))
                return Result.Failure<DepartmentRow>(DepartmentErrors.NameTaken(name));

            department.Name = name;
        }

        department.Code = (command.Code ?? department.Code).Trim();

        if (command.SortOrder.HasValue) department.SortOrder = command.SortOrder.Value;
        if (command.IsActive.HasValue) department.IsActive = command.IsActive.Value;

        audit.Record(
            AuditActions.DepartmentUpdated, "Department",
            department.Id, department.Code,
            $"اتعدّل قسم «{department.Name}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        int technicians = await departments.CountTechniciansAsync(
            department.Id, cancellationToken);

        return Result.Success(new DepartmentRow(
            department.Id, department.Code, department.Name,
            department.IsActive, department.SortOrder, technicians));
    }
}
