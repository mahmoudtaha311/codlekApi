using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.UpdateTechnician;

/// <summary>
/// تعديل الاسم والتخصص والقسم والصلاحيات.
///
/// <para>⚠️ <b>والكود واسم الدخول مش هنا.</b> الكود ثابت بعد
/// الإنشاء لأن الفحوص القديمة بتشاور عليه، وتغييره بيفصل الفني عن
/// تاريخه.</para>
///
/// <para>🔴 <b>وكل حقل <c>null</c> معناه «ماتلمسش»</b> — الاسم بس
/// هو الإجباري.</para>
/// </summary>
public sealed class UpdateTechnicianCommandHandler(
    ITechnicianAccountRepository accounts,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<UpdateTechnicianCommand, Result<TechnicianActionResponse>>
{
    public async Task<Result<TechnicianActionResponse>> Handle(
        UpdateTechnicianCommand command, CancellationToken cancellationToken)
    {
        string displayName = (command.DisplayName ?? "").Trim();

        if (displayName.Length < TechnicianAccountRules.MinDisplayName)
            return Result.Failure<TechnicianActionResponse>(
                TechnicianAccountErrors.NameTooShort);

        // 🔴 التخصص لازم يكون من القايمة — نفس ثقب `(UserRole)99`.
        if (command.Specialty is { } picked
            && !Enum.IsDefined((TechnicianSpecialty)picked))
        {
            return Result.Failure<TechnicianActionResponse>(
                TechnicianAccountErrors.UnknownSpecialty);
        }

        var technician = await accounts.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (technician is null)
            return Result.Failure<TechnicianActionResponse>(
                TechnicianAccountErrors.NotFound);

        technician.DisplayName = displayName;

        /*
          🔴 **التخصص بيتغيّر بس لو المنادي بعته.**

          «غير محدد» = صفر، فالكاست المباشر من حقل مش مبعوت
          (`?? 0`) كان بيرجّع كل فني اتعدّل من عميل مش بيبعت الحقل
          لـ«غير محدد» **في صمت**.
        */
        if (command.Specialty is { } specialty)
            technician.Specialty = (TechnicianSpecialty)specialty;

        /*
          🔴 **والقسم زيّه — بس بقيمة حارسة.**

          القسم الفاضي في القاعدة هو `null`، و`null` في الطلب محجوزة
          لـ«ماتلمسش» — فالمدير لازم يقدر يشيل القسم بقرار، والقيمة
          الحارسة (`Guid.Empty`) هي الفرق بين القرار ده وبين عميل
          قديم نسي يبعت الحقل.

          ⚠️ والكنترولر هو اللي بيقرا الجسم الخام ويحوّل «الحقل
          اتبعت فاضي» لـ`Guid.Empty`.
        */
        if (command.DepartmentId is { } sent)
            technician.DepartmentId = TechnicianAccountRules.Department(sent);

        /*
          🔴 **والصلاحيات بتتغيّر بس لو المنادي بعتها.**

          `null` معناها «ماتلمسش». ولو كانت `bool` عادية، أي نداء
          قديم من واجهة مش عارفة الحقول دي كان هيوصل `false` وينزع
          صلاحية الفحص من كل فني اتعدّل اسمه.
        */
        bool capabilityChanged = false;

        if (command.CanTest is { } canTest && canTest != technician.CanTest)
        {
            technician.CanTest = canTest;
            capabilityChanged = true;
        }

        if (command.CanRepair is { } canRepair && canRepair != technician.CanRepair)
        {
            technician.CanRepair = canRepair;
            capabilityChanged = true;
        }

        technician.UpdatedAtUtc = DateTime.UtcNow;

        /*
          🔴 **ووقت تغيير الصلاحية هو دليل الشغل الأوفلاين.**

          الراكة بتشتغل من غير نت، فممكن فني يعمل صيانة وهو مصرّح له
          وبعدين الصلاحية تتسحب قبل ما الشغل يترفع. والوقت ده هو
          اللي بيخلّي السيرفر يفرّق بين «كان مصرّح له وقتها» و«مكانش
          مصرّح له خالص» — ومن غيره، سحب صلاحية بيمسح شغل حصل
          فعلاً.
        */
        if (capabilityChanged) technician.CapabilityChangedAtUtc = DateTime.UtcNow;

        audit.Record(
            AuditActions.TechnicianUpdated, "Technician",
            technician.Id, technician.Code,
            $"اتعدّلت بيانات «{technician.DisplayName}» — "
            + $"التخصص: {TechnicianSpecialtyText.Arabic(technician.Specialty)}");

        /*
          🔴 **وسطر منفصل للصلاحيات، ولما تتغيّر بس.**

          تعديل اسم مالوش يسيب سطر «اتغيّرت الصلاحيات» في السجل —
          اللي بيراجع بيدوّر على التغييرات الحقيقية.
        */
        if (capabilityChanged)
        {
            audit.Record(
                AuditActions.TechnicianCapabilityChanged, "Technician",
                technician.Id, technician.Code,
                $"صلاحيات {technician.DisplayName}: "
                + $"فحص {(technician.CanTest ? "نعم" : "لأ")}، "
                + $"صيانة {(technician.CanRepair ? "نعم" : "لأ")}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TechnicianActionResponse(
            TechnicianAccountMapping.Account(technician), "اتحفظ التعديل"));
    }
}
