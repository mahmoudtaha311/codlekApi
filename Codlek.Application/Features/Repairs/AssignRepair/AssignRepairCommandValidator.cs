using FluentValidation;

namespace Codlek.Application.Features.Repairs.AssignRepair;

public sealed class AssignRepairCommandValidator : AbstractValidator<AssignRepairCommand>
{
    public AssignRepairCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        // ⚠️ معرّف فاضي معناه الواجهة بعتت منسدلة مش مختارة — والرسالة
        // لازم تقول كده بدل «الفني مش موجود».
        RuleFor(c => c.TechnicianId)
            .NotEmpty().WithMessage("اختار الفني الأول.");
    }
}
