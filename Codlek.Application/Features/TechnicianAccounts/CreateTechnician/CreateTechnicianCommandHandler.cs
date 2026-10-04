using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Technicians;
using Codlek.Core.Text;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.TechnicianAccounts.CreateTechnician;

/// <summary>
/// إنشاء حساب فني.
///
/// <para>🔴 <b>والباسورد الأولي بيرجع <u>مرة واحدة</u></b> عشان
/// المدير يسلّمه للفني — وبعدها مش موجود في أي مكان غير دماغ
/// الفني.</para>
/// </summary>
public sealed class CreateTechnicianCommandHandler(
    ITechnicianAccountRepository accounts,
    ITechnicianPasswords passwords,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me,
    ILogger<CreateTechnicianCommandHandler> log)
    : IRequestHandler<CreateTechnicianCommand, Result<TechnicianSecretResponse>>
{
    public async Task<Result<TechnicianSecretResponse>> Handle(
        CreateTechnicianCommand command, CancellationToken cancellationToken)
    {
        string displayName = (command.DisplayName ?? "").Trim();
        string username = (command.Username ?? "").Trim();
        string password = command.Password ?? "";

        if (displayName.Length < TechnicianAccountRules.MinDisplayName)
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.NameTooShort);

        if (username.Length < TechnicianAccountRules.MinUsername)
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.UsernameTooShort);

        if (password.Length < TechnicianAccountRules.MinPassword)
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.PasswordTooShort);

        /*
          🔴 **التخصص لازم يكون من القايمة — زي نفس ثقب `(UserRole)99`.**

          الكاست المباشر من رقم العميل كان بيخزّن `99` والفني بيفضل
          «غير محدد» للأبد.
        */
        if (command.Specialty is { } picked
            && !Enum.IsDefined((TechnicianSpecialty)picked))
        {
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.UnknownSpecialty);
        }

        string key = LoginName.Normalize(username);

        /*
          🔴 **الاسم فريد <u>جوّه الشركة</u>، مش عالمياً — وده عكس
          مستخدمي اللوحة بالظبط.**

          الفني بيدخل من راكة متحققة بمفتاحها، والشركة معروفة **من
          الراكة** قبل ما الاسم يتقرا — فشركتين ينفع يبقى عندهم
          «ahmed».
        */
        if (await accounts.UsernameTakenAsync(me.TenantId, key, cancellationToken))
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.UsernameTaken);

        string? code = await NewCodeAsync(cancellationToken);

        if (code is null)
            return Result.Failure<TechnicianSecretResponse>(
                TechnicianAccountErrors.CodesExhausted);

        var (hash, salt) = passwords.Create(password);

        var technician = new Technician
        {
            TenantId = me.TenantId,
            Code = code,
            DisplayName = displayName,
            Username = username,

            // ⚠️ والعمود المطبَّع هو اللي الفهرس الفريد عليه — الخام
            // للعرض بس.
            NormalizedUsername = key,

            PasswordHash = hash,
            Salt = salt,
            Specialty = (TechnicianSpecialty)(command.Specialty ?? 0),
            DepartmentId = command.DepartmentId,
            IsActive = true,

            // ⚠️ الباسورد ده المدير حطّه، فالفني لازم يغيّره أول
            // دخول.
            MustChangePassword = true,
        };

        accounts.Add(technician);

        /*
          🔴 **التسجيل على النجاح بس.**

          محاولة وقعت (اسم مكرر، باسورد قصير) ماحصلش فيها حاجة — وسطر
          بيقول «اتعمل فني» لحساب مااتعملش بيخلّي السجل كله مش موثوق.

          ⚠️ **والباسورد الأولي عمره ما بيدخل السجل.**
        */
        audit.Record(
            AuditActions.TechnicianCreated, "Technician",
            technician.Id, technician.Code, $"اتعمل فني «{displayName}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "اتعمل فني {Code} في الشركة {Tenant}.", technician.Code, me.TenantId);

        return Result.Success(new TechnicianSecretResponse(
            TechnicianAccountMapping.Account(technician),
            password,
            $"اتعمل الفني «{displayName}» بكود {technician.Code}"));
    }

    /// <summary>
    /// كود فاضي — <b>بالتجريب</b>.
    ///
    /// <para>⚠️ العشوائية مش كفاية لوحدها: الكود ٦ أرقام، فمجال
    /// ٩٠٠ ألف واحتمال التكرار بيكبر مع كل فني. والحلقة بتجرّب لحد
    /// ما تلاقي فاضي.</para>
    ///
    /// <para>🔴 و<c>null</c> بعد كل المحاولات معناها إن المجال خلص
    /// تقريباً — ودي حاجة المدير لازم يعرفها، مش كود مكرر يتحشر في
    /// القاعدة.</para>
    /// </summary>
    private async Task<string?> NewCodeAsync(CancellationToken ct)
    {
        for (int attempt = 0; attempt < TechnicianAccountRules.CodeAttempts; attempt++)
        {
            string code = AccountCode.New();

            if (!await accounts.CodeTakenAsync(me.TenantId, code, ct)) return code;
        }

        log.LogError(
            "مفيش كود فني فاضي بعد {Attempts} محاولة في الشركة {Tenant}.",
            TechnicianAccountRules.CodeAttempts, me.TenantId);

        return null;
    }
}
