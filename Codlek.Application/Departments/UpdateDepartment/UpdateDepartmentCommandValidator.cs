using FluentValidation;

namespace Codlek.Application.Departments.UpdateDepartment;

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        // ⚠️ `When` مش `NotEmpty`: الاسم الناقص معناه «ماتغيّرش»،
        // والاسم الفاضي المبعوت عن قصد معناه غلط. الاتنين مختلفين.
        RuleFor(c => c.Name)
            .MaximumLength(120).WithMessage("اسم القسم أطول من اللازم.")
            .When(c => c.Name is not null);

        RuleFor(c => c.Code)
            .MaximumLength(20).WithMessage("كود القسم أطول من اللازم.")
            .When(c => c.Code is not null);
    }
}
