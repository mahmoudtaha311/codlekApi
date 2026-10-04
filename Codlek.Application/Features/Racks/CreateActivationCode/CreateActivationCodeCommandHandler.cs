using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Racks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Racks.CreateActivationCode;

/// <summary>
/// كود تفعيل محطة فحص جديدة.
///
/// <para>🔴 <b>والكود بيرجع <u>مرة واحدة</u></b> — بصمته بس اللي
/// بتتخزّن، زي الباسورد بالظبط. اللي قفل الصفحة قبل ما ياخده لازم
/// يعمل كود جديد.</para>
///
/// <para>⚠️ <b>الاسم اللي المستخدم بيشوفه «كود تفعيل محطة
/// الفحص».</b> الكيان في القاعدة لسه اسمه <c>RackPairingCode</c> —
/// تغيير اسم الجدول هجرة مالهاش أي مكسب تشغيلي، والعقد مع الراكة
/// مجمّد على اسم الحقل <c>pairingCode</c>. المصطلح بيتغيّر في الواجهة
/// وبس.</para>
/// </summary>
public sealed class CreateActivationCodeCommandHandler(
    IRackRepository racks,
    IActivationCodeHasher hasher,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    ILogger<CreateActivationCodeCommandHandler> log)
    : IRequestHandler<CreateActivationCodeCommand, Result<ActivationCodeIssued>>
{
    public async Task<Result<ActivationCodeIssued>> Handle(
        CreateActivationCodeCommand command, CancellationToken cancellationToken)
    {
        string name = (command.Name ?? "").Trim();
        string location = (command.Location ?? "").Trim();

        if (name.Length < RackPolicy.MinStationName)
            return Result.Failure<ActivationCodeIssued>(RackErrors.NameTooShort);

        string code = PairingCode.New();
        var (hash, salt) = hasher.Create(code);

        var row = new RackPairingCode
        {
            TenantId = me.TenantId,

            // 🔴 الكود نفسه مش هنا — بصمته وملحه وبس.
            CodeHash = hash,
            Salt = salt,
            CodePrefix = PairingCode.Prefix(code),

            ExpiresAtUtc = DateTime.UtcNow.Add(PairingCode.Lifetime),
            CreatedByUserId = me.Id,
            CreatedByName = me.DisplayName,
            IntendedName = name,
            IntendedLocation = location,
        };

        racks.AddCode(row);

        /*
          🔴 **والكود نفسه عمره ما يدخل السجل.**

          السجل بيقول «مين عمل كود لأنهي محطة وامتى» وبس — واحد
          بيقرا السجل ماينفعش يطلع منه بكود يفعّل بيه محطة.

          ⚠️ **وكود الكيان فاضي هنا** مش البادئة: وقت الإنشاء
          البادئة مش معلومة مفيدة للي بيراجع، والمسح هو اللي بيكتبها
          عشان يربط السطرين.
        */
        audit.Record(
            AuditActions.RackCodeCreated, "Rack",
            row.Id, "", $"اتعمل كود تفعيل لمحطة «{name}»");

        /*
          🔴 **حفظة واحدة للصف والسجل.**

          القديم كان بيحفظ الصف جوّه الخدمة وبعدين يحفظ السجل تاني.
          وقوع القاعدة بين الاتنين كان بيدّي **كود صالح مالوش سطر في
          السجل** — يعني كود بيفتح محطة ومحدّش يعرف مين عمله.
        */
        await unitOfWork.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "اتعمل كود تفعيل لمحطة {Name} في الشركة {Tenant}.", name, me.TenantId);

        return Result.Success(new ActivationCodeIssued(code, name, row.ExpiresAtUtc));
    }
}
