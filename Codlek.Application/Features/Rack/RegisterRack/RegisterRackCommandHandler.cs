using System.Text.Json;
using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Rack;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Racks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.RegisterRack;

/// <summary>
/// تسجيل محطة فحص جديدة.
///
/// <para>🔴 <b>ودي النقطة الوحيدة في سطح الراكة اللي بتقبل كتابة
/// من غير مفتاح</b> — لأن المحطة الجديدة <b>مالهاش مفتاح لسه</b>،
/// وده أصل المشكلة اللي بتحلّها. واللي بيحميها هو كود التفعيل:
/// قصير العمر، بيتستهلك مرة واحدة، وبيتقفل بعد خمس محاولات
/// غلط.</para>
///
/// <para>🔴 <b>وقبل كده المدير كان بيعمل مفتاح وينسخه بإيده
/// للراكة.</b> ده معناه إن المفتاح بيعدّي على الحافظة وعلى واتساب
/// أحياناً، وإن أي راكة مستنسخة بتورث مفتاح غيرها من غير ما حد
/// يعرف.</para>
/// </summary>
public sealed class RegisterRackCommandHandler(
    IRackRepository racks,
    IRackKeys keys,
    IActivationCodeHasher codes,
    ITenantCounters counters,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ILogger<RegisterRackCommandHandler> log)
    : IRequestHandler<RegisterRackCommand, Result<RackRegistered>>
{
    public async Task<Result<RackRegistered>> Handle(
        RegisterRackCommand command, CancellationToken cancellationToken)
    {
        string code = RackRegistration.Clean(command.PairingCode);

        if (!RackRegistration.Usable(code))
            return Result.Failure<RackRegistered>(RackRegisterErrors.InvalidCode);

        /*
          🔴 **البحث بالبادئة — عابر للشركات بالضرورة.**

          المحطة الجديدة مالهاش مفتاح ومالهاش شركة؛ الكود هو اللي
          بيحدّد الشركة. فالترشيح بالشركة قبل التحقق دور.
        */
        var candidates = await racks.CodesByPrefixAsync(
            PairingCode.Prefix(code), cancellationToken);

        /*
          ⚠️ **الكود المقفول مابيتطابقش أصلاً.**

          شرط المحاولات جوّه المطابقة، فالرد عليه «غلط أو اتستخدم»
          زي أي كود مش موجود — واللي بيحاول مايعرفش إنه لقى الكود
          الصح وقفله.
        */
        var match = candidates.FirstOrDefault(c =>
            !RackRegistration.LockedOut(c.FailedAttempts)
            && codes.Verify(code, c.CodeHash, c.Salt));

        if (match is null)
        {
            /*
              🔴 **المحاولة الغلط بتتعدّ على <u>كل</u> أكواد نفس
              البادئة.**

              وده اللي بيخلّي التخمين غير مجدي بدل ما يبقى مجرد
              إبطاء. والزيادة الجماعية دي **مقصودة ومش حاجة
              تتصلّح**: التصادم في البادئة (٤ حروف من ٣١ = ٩٢٣٥٢١
              احتمال) نادر لدرجة إن كود شرعي يتأثر بالتخمين على كود
              تاني حالة بتحصل مرة كل مئتين ألف.
            */
            foreach (var candidate in candidates) candidate.FailedAttempts++;

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<RackRegistered>(RackRegisterErrors.InvalidCode);
        }

        /*
          ⚠️ **الانتهاء بيتفحص بعد المطابقة مش قبلها.**

          الرد «انتهت صلاحيته» معلومة مفيدة للفني — بس إداها لكود
          مش متطابق كان بيقول للي بيخمّن «البادئة دي فيها كود».
        */
        if (match.ExpiresAtUtc < DateTime.UtcNow)
            return Result.Failure<RackRegistered>(RackRegisterErrors.CodeExpired);

        /*
          🔴 **الاستهلاك تحديث مشروط بعدّ الصفوف.**

          راكتين بيسجّلوا بنفس الكود في نفس اللحظة لازم **واحدة
          بس** تكسب — والقراية-ثم-الكتابة بتخلّي الاتنين يكسبوا،
          يعني محطتين بمفتاحين من كود واحد.
        */
        bool claimed = await racks.ConsumeCodeAsync(
            match.Id, DateTime.UtcNow, cancellationToken);

        if (!claimed)
            return Result.Failure<RackRegistered>(RackRegisterErrors.CodeUsed);

        string? tenantName = await racks.TenantNameAsync(match.TenantId, cancellationToken);

        /*
          ⚠️ **الشركة المش موجودة حالة مقفولة مش طلب غلط.**

          الكود اتعمل لشركة اتمسحت بعدين — واللي بيسجّل مالوش يد
          فيها، فالرد بيقول «مقفول» (<c>423</c>) مش «غلط».
        */
        if (tenantName is null)
            return Result.Failure<RackRegistered>(RackRegisterErrors.TenantMissing);

        string name = RackRegistration.Name(command.RackName, match.IntendedName);

        int number = await counters.NextAsync(
            match.TenantId, RackRegistration.Counter, cancellationToken);

        string rackCode = RackRegistration.Code(number);

        /*
          🔴 **المفتاح بيرجع <u>مرة واحدة</u>.**

          بصمته هي اللي في القاعدة، فاللي قفل الشاشة قبل ما ياخده
          محتاج كود تفعيل جديد.
        */
        var (apiKey, keyHash, keySalt) = keys.Issue();

        var rack = new Core.Entities.Rack
        {
            TenantId = match.TenantId,
            RackCode = rackCode,
            Name = name,

            // ⚠️ المكان من نيّة المدير وقت عمل الكود — الراكة
            //    مابتعرفش هي في أنهي دور.
            Location = match.IntendedLocation,

            ApiKeyHash = keyHash,
            Salt = keySalt,
            KeyPrefix = RackKey.Prefix(apiKey),

            Status = RackStatus.Active,

            /*
              🔴 **الهوية مربوطة بالقرص مش بالجهاز المضيف.**

              الراكة هارد بيتنقل بين لابات كل شوية — ده شغلها
              الطبيعي. وأي ربط بمعرّف الجهاز كان هيلغي التسجيل في
              كل نقلة.
            */
            InstallationId = (command.InstallationId ?? "").Trim(),
            LastMachineIdentifier = (command.MachineIdentifier ?? "").Trim(),

            AppVersion = (command.AppVersion ?? "").Trim(),

            RegisteredAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            KeyIssuedAtUtc = DateTime.UtcNow,
        };

        racks.Add(rack);

        /*
          ⚠️ **الربط بين الكود والمحطة بعد ما المحطة تاخد
          معرّفها.**

          والصف ده هو الدليل الوحيد على إن المحطة الفلانية اتسجّلت
          بأنهي إذن ومن مين — وعشان كده الكود المستهلك مابيتمسحش.
        */
        await racks.LinkCodeToRackAsync(match.Id, rack.Id, cancellationToken);

        var actor = new RackAuditActor(
            match.TenantId, rack.Id, name, command.Ip ?? "");

        audit.RecordForRack(
            actor,
            AuditActions.RackPaired, "Rack", rack.Id, rackCode,
            $"راكة «{name}» اتسجّلت بكود اقتران من {match.CreatedByName}",

            // ⚠️ تفاصيل الجهاز في السجل — هي اللي بتخلّي اشتباه
            //    الاستنساخ قابل للمراجعة بعد أسبوع.
            JsonSerializer.Serialize(new
            {
                installationId = command.InstallationId,
                machineIdentifier = command.MachineIdentifier,
                appVersion = command.AppVersion,
            }));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await FlagPossibleCloneAsync(rack, cancellationToken);

        log.LogInformation("راكة اتسجّلت: {Code} ({Name})", rackCode, name);

        return Result.Success(new RackRegistered(
            RackId: rack.Id,
            RackCode: rackCode,
            RackName: name,
            TenantId: match.TenantId,
            TenantName: tenantName,

            // 🔴 مرة واحدة وبس.
            ApiKey: apiKey,

            /*
              🔴 **من الإعدادات، مش من ترويسة الطلب.**

              النسخة الأولى في القديم كانت بتبني العنوان من
              `Request.Host` — يعني اللي بيسجّل كان بيحدّد بترويسة
              Host فين الراكة هترفع شغلها بعد كده، والراكة بتخزّن
              العنوان وتفضل عليه.
            */
            SyncUrl: command.SyncUrl,

            Message: "الراكة اتسجّلت."));
    }

    /// <summary>
    /// بيدوّر على محطات تانية بنفس هوية القرص.
    ///
    /// <para>🔴 <b>بننبّه ومنمنعش.</b> الرفض القاطع بيقفل راكة
    /// شرعية يوم ما فني ينقل الهارد لبنش تاني — ومحدّش هيربط
    /// الحدثين.</para>
    ///
    /// <para>⚠️ <b>وبعد الحفظ عن قصد:</b> المحطة الجديدة لازم تكون
    /// في القاعدة عشان البحث يلاقي التوائم، والتنبيه نفسه سطر سجل
    /// مش شرط على التسجيل.</para>
    /// </summary>
    private async Task FlagPossibleCloneAsync(
        Core.Entities.Rack rack, CancellationToken ct)
    {
        if (rack.InstallationId.Length == 0) return;

        var twins = await racks.TwinsByInstallationAsync(
            rack.TenantId, rack.InstallationId, rack.Id, ct);

        if (twins.Count == 0) return;

        /*
          ⚠️ **الفاعل هنا «النظام» مش المحطة.**

          المحطة مااعملتش حاجة غلط — اللي لاحظ هو السيرفر. وكتابة
          المحطة كفاعل كانت بتتقري اتهام.
        */
        audit.RecordForRack(
            new RackAuditActor(rack.TenantId, rack.Id, "النظام", ""),
            AuditActions.RackCloneSuspected, "Rack", rack.Id, rack.RackCode,
            "نفس هوية القرص متسجّلة لأكتر من راكة — يحتمل إن الصورة "
            + "اتستنسخت من غير تسجيل جديد",
            JsonSerializer.Serialize(
                twins.Select(t => new { t.Id, t.RackCode, t.Name })));

        await unitOfWork.SaveChangesAsync(ct);
    }
}
