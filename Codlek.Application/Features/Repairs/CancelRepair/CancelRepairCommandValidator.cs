using Codlek.Core.Text;
using FluentValidation;

namespace Codlek.Application.Features.Repairs.CancelRepair;

/// <summary>
/// 🔴 <b>رفض السبب الفاضي <i>مش</i> هنا — هو جوّه المعالج.</b>
///
/// <para>المتحقّق بيشتغل <b>قبل</b> المعالج. والقديم بيقبل إعادة إلغاء
/// أمر ملغي خلاص <b>من غير سبب</b> — فلو الرفض اتحطّ هنا، الإعادة دي
/// بتترفض والجديد يبقى بيدّي رد مختلف عن القديم على نفس الطلب.</para>
///
/// <para>⚠️ واللي هنا هو الطول بس — ده مالوش علاقة بالترتيب.</para>
/// </summary>
public sealed class CancelRepairCommandValidator : AbstractValidator<CancelRepairCommand>
{
    public CancelRepairCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.Reason)
            .MaximumLength(TextClip.Lengths.Reason)
            .WithMessage("سبب الإلغاء أطول من اللازم.")
            .When(c => c.Reason is not null);
    }
}
