using Codlek.Core.Enums;
using FluentValidation;

namespace Codlek.Application.Features.Repairs.OpenRepair;

/// <summary>
/// 🔴 <b>فحص التخصّص ده <i>زيادة</i> على القديم — بقرار صريح من صاحب
/// الشغل.</b>
///
/// <para>القديم بيعمل <c>(TechnicianSpecialty)(body.RequiredSpecialty
/// ?? 0)</c> من غير أي فحص. فـ<c>{"requiredSpecialty": 99}</c>
/// بيتخزّن، و<c>TechnicianSpecialtyText</c> بيعرضه «غير محدد» —
/// <b>والأمر يفضل كده للأبد</b>. نفس الحاجة اللي اتصلّحت في حسابات
/// اللوحة مع <c>(UserRole)99</c>.</para>
///
/// <para>⚠️ ومعنى ده إن نفس الطلب بينجح من اللوحة القديمة وبيترفض من
/// الجديدة فترة التحويل — والجديدة مابتخدمش حد لحد التحويل.</para>
/// </summary>
public sealed class OpenRepairCommandValidator : AbstractValidator<OpenRepairCommand>
{
    public OpenRepairCommandValidator()
    {
        RuleFor(c => c.DeviceId)
            .NotEmpty().WithMessage("اختار الجهاز الأول.");

        RuleFor(c => c.RequiredSpecialty)
            .Must(v => v is null || Enum.IsDefined(typeof(TechnicianSpecialty), v.Value))
            .WithMessage("التخصّص المطلوب مش من القايمة.");
    }
}
