using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Technicians;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Rack.TechnicianLogin;

/// <inheritdoc cref="TechnicianLoginCommand"/>
public sealed class TechnicianLoginCommandHandler(
    ITechnicianLoginRepository logins,
    ITechnicianPasswords passwords,
    IUnitOfWork unitOfWork)
    : IRequestHandler<TechnicianLoginCommand, Result<TechnicianLoggedIn>>
{
    public async Task<Result<TechnicianLoggedIn>> Handle(
        TechnicianLoginCommand command, CancellationToken cancellationToken)
    {
        string key = LoginName.Normalize(command.Username);
        var now = DateTime.UtcNow;

        /*
          🔴 **القفل قبل أي حاجة تانية.**

          النقطة دي عرّافة باسوردات، والعدّاد هو الفرملة الوحيدة على
          التخمين **بالاسم** — الحد على مستوى HTTP بيقسّم بالمحطة بس
          ومايقدرش يقرا الاسم من جسم JSON من غير ما يستهلك المجرى.
        */
        if (key.Length > 0)
        {
            int recent = await logins.RecentFailuresAsync(
                command.RackId, key,
                TechnicianLoginRules.WindowStart(now), cancellationToken);

            if (TechnicianLoginRules.Throttled(recent))
            {
                /*
                  ⚠️ **والمحاولة المقفولة بتتسجّل كفاشلة بردو.**

                  يعني القفل بيمدّ نفسه وإحدى عشر محاولة بتخلّي
                  النافذة تعيش خمس دقايق تانية. وده مقصود: اللي
                  بيطرطق على الشاشة مايستفيدش من الطرطقة.
                */
                Record(command, null, key, false, TechnicianLoginRules.ReasonTooMany, now);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return Failure(TechnicianAuthCodes.TooManyAttempts,
                    TechnicianLoginRules.TooManyMessage);
            }
        }

        /*
          🔴 **الشركة من المحطة — والبحث جوّاها.**

          مفيش أي مدخل من الطلب بيقدر يقول «أنا تبع شركة كذا».
        */
        var technician = await logins.FindByLoginKeyAsync(
            command.TenantId, key, cancellationToken);

        /*
          ⚠️ **نفس الرسالة سواء الاسم مش موجود أو الباسورد غلط.**

          التفرقة بينهم بتقول للي بيجرّب أسماء مين موجود — وده نص
          الشغل اللي قبل التخمين.
        */
        if (technician is null
            || !passwords.Verify(
                   command.Password ?? "", technician.PasswordHash, technician.Salt))
        {
            Record(command, technician?.Id, key, false,
                TechnicianLoginRules.ReasonInvalid, now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Failure(TechnicianAuthCodes.InvalidCredentials,
                TechnicianLoginRules.InvalidMessage);
        }

        /*
          🔴 **والإيقاف بيتفحص <u>بعد</u> الباسورد.**

          لو عكسنا، اللي بيجرّب أسماء كان هيعرف مين موجود ومين موقوف
          من غير ما يعرف ولا باسورد واحد. الفني الموقوف بيشوف السبب
          بعد ما يثبت إنه هو.
        */
        if (!technician.IsActive)
        {
            Record(command, technician.Id, key, false,
                TechnicianLoginRules.ReasonSuspended, now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Failure(
                TechnicianAuthCodes.Suspended,
                TechnicianLoginRules.SuspendedMessage(
                    technician.SuspendedReason, technician.SuspendedByName));
        }

        technician.LastSuccessfulLoginUtc = now;

        /*
          🔴 **والصف الناجح ده مش للسجل — هو دليل تصريح.**

          السيرفر بيستعمله وقت رفع الشغل عشان يعرف إن الفني كان مصرّح
          له **وقتها**، مش دلوقتي. من غيره، سحب صلاحية بيمسح شغل حصل
          فعلاً وهو مصرّح به.
        */
        Record(command, technician.Id, key, true, "", now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TechnicianLoggedIn(
            TechnicianId: technician.Id,
            TechnicianCode: technician.Code,
            DisplayName: technician.DisplayName,
            CredentialVersion: technician.CredentialVersion,
            MustChangePassword: technician.MustChangePassword,

            // 🔴 المهلة من **وقت الدخول** — مش من وقت إنشاء الحساب.
            OfflineValidUntilUtc: TechnicianLoginRules.OfflineUntil(
                now, command.OfflineValidityDays),

            CanTest: technician.CanTest,
            CanRepair: technician.CanRepair,
            RackCode: command.RackCode,
            ServerTimeUtc: now));
    }

    private static Result<TechnicianLoggedIn> Failure(string code, string message) =>
        Result.Failure<TechnicianLoggedIn>(
            new Error(code, message, TechnicianAuthCodes.LoginStatus(code)));

    private void Record(
        TechnicianLoginCommand command, Guid? technicianId, string attempted,
        bool success, string reason, DateTime atUtc) =>
        logins.AddAttempt(new TechnicianLoginAttempt
        {
            TenantId = command.TenantId,
            RackId = command.RackId,
            TechnicianId = technicianId,

            /*
              ⚠️ **مفيش قص هنا — `LoginName.Normalize` قصّه قبل
              كده.**

              كان فيه `TextClip.To(...)` في السطر ده، وتحوير مقصود
              شالّه **نجا** من الفحوص كلها — ومعاه حق: المفتاح
              مابيوصلش هنا أطول من `LoginName.MaxLength` أصلاً،
              فالقص كان كود ميت. والحارس الحقيقي من الانحراف فحص
              بيقارن الحدّين (`A_login_key_can_never_overflow_the_column`).
            */
            AttemptedUsername = attempted,

            Success = success,
            Reason = reason,
            AtUtc = atUtc,
        });
}
