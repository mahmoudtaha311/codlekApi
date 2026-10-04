using FluentValidation;

namespace Codlek.Application.Features.Departments.CreateDepartment;

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        // ⚠️ الأطوال دي = أطوال الأعمدة في القاعدة بالحرف. لو الفحص
        // هنا أطول، SQL Server بيرمي خطأ قص مالوش معنى عند المستخدم.
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("اسم القسم مطلوب.")
            .MaximumLength(120).WithMessage("اسم القسم أطول من اللازم.");

        RuleFor(c => c.Code)
            .MaximumLength(20).WithMessage("كود القسم أطول من اللازم.");
    }
}
