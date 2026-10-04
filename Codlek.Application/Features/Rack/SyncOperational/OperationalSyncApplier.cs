using Codlek.Application.Contracts.Sync;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;
using Codlek.Core.Sync;
using Codlek.Core.Text;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.SyncOperational;

/// <summary>
/// نتيجة صف تشغيلي واحد — <b>بنفس لغة رد الدفعة</b>.
/// </summary>
/// <param name="Retryable">
/// 🔴 <b>رفض مؤقت</b> — الحمولة سليمة وشرطها لسه ماوصلش (الجهاز أو
/// الفحص). الراكة بتعيد بعدين. والرفض النهائي هنا معناه شغل صيانة
/// بيضيع لأن ترتيب دفعة اتكسر.
/// </param>
public sealed record OperationalOutcome(
    bool Applied, bool Unchanged, string? Code, string? Message, bool Retryable)
{
    public static OperationalOutcome Ok() => new(true, false, null, null, false);

    public static OperationalOutcome Same() => new(false, true, null, null, false);

    public static OperationalOutcome Reject(string code, string message) =>
        new(false, false, code, message, false);

    public static OperationalOutcome Later(string code, string message) =>
        new(false, false, code, message, true);

    public bool Rejected => Code is not null;
}

/// <summary>هوية الراكة اللي الشغل جايّ منها.</summary>
/// <param name="OfflineValidityDays">
/// ⚠️ من الإعدادات — نفس الرقم اللي دخول الفني بيختم بيه المهلة.
/// </param>
public sealed record OperationalSource(Guid TenantId, Guid RackId, int OfflineValidityDays);

/// <summary>
/// بيطبّق الشغل التشغيلي الجايّ من راكة — <b>أوامر الصيانة وحركات
/// السجل</b>.
///
/// <para>🔴 <b>كل عملية هنا لازم تبقى ساكنة (idempotent).</b> الراكة
/// بتعيد الرفع لما الرد يتأخر، والانقطاع بعد ما السيرفر ثبّت بيخلّيها
/// تعيد نفس الحمولة. أمر الصيانة بيتطابق بمعرّفه، والحركة بمعرّف
/// حدثها — فالإعادة بترجّع نفس الصف مش صف تاني.</para>
///
/// <para>⚠️ <b>ومابيحفظش</b> — المنادي بيحفظ بين الطورين.</para>
/// </summary>
public sealed class OperationalSyncApplier(
    IOperationalSyncRepository repo,
    IDeviceReference deviceReference,
    IDeviceWorkflowRecorder workflow,
    ITenantCounters counters,
    ILogger<OperationalSyncApplier> log)
{
    public const string MissingFields = "MissingFields";
    public const string DeviceNotSynced = "DeviceNotSynced";
    public const string ReportNotSynced = "ReportNotSynced";
    public const string TechnicianNotFound = "TechnicianNotFound";
    public const string CapabilityDenied = "CapabilityDenied";
    public const string WorkflowRejected = "WorkflowRejected";

    /// <summary>
    /// اسم الفاعل لما الحركة جاية من غير فني — <b>اسم ثابت بيتعرض في
    /// السجل</b>.
    /// </summary>
    public const string RackActorName = "محطة فحص";

    // =================================================================
    //  صلاحية الفني وقت الحركة
    // =================================================================

    private async Task<string?> DenyAsync(
        OperationalSource source, Technician tech, bool capability,
        DateTime occurredAtUtc, CancellationToken ct)
    {
        var lastLogin = await repo.LastLoginAtAsync(
            source.TenantId, source.RackId, tech.Id, occurredAtUtc, ct);

        return OfflineAuthorisation.Deny(
            occurredAtUtc, DateTime.UtcNow, lastLogin, source.OfflineValidityDays,
            tech.IsActive, tech.SuspendedAtUtc, capability, tech.CapabilityChangedAtUtc);
    }

    // =================================================================
    //  أوامر الصيانة
    // =================================================================

    public async Task<OperationalOutcome> ApplyWorkItemAsync(
        OperationalSource source, RepairWorkItemSyncPayload dto, CancellationToken ct = default)
    {
        if (dto.Id == Guid.Empty)
            return OperationalOutcome.Reject(MissingFields, "أمر صيانة من غير رقم تعريف.");

        if (dto.DeviceId == Guid.Empty)
            return OperationalOutcome.Reject(MissingFields, "أمر صيانة من غير جهاز.");

        /*
          🔴 **الترجمة الأول.** الراكة بتبعت بمعرّفها المحلي، واللي ممكن
          يكون اتعرّف عليه كجهاز موجود وقت مزامنة الأجهزة. من غير
          الخطوة دي، أمر الصيانة بيترفض «الجهاز لسه ماوصلش» وهو واصل —
          وده بالظبط اللي حصل في الإنتاج.

          ⚠️ والرفض **مؤقت**: الأمر سليم تماماً، جهازه بس لسه ماوصلش.
          ونتيجة المترجم بتضمن صف جهاز **حيّ في الشركة دي** — فمفيش
          داعي لاستعلام تاني يتأكد إنه موجود (القديم كان بيعمله).
        */
        if (await deviceReference.ResolveAsync(source.TenantId, dto.DeviceId, ct)
            is not { } deviceId)
        {
            return OperationalOutcome.Later(
                DeviceNotSynced, "جهاز أمر الصيانة لسه ماوصلش. ابعت الجهاز الأول.");
        }

        // ⚠️ الفحص المصدر اختياري، بس لو اتبعت لازم يكون بتاع نفس الشركة.
        if (dto.SourceReportId is { } reportId && reportId != Guid.Empty
            && !await repo.ReportExistsAsync(source.TenantId, reportId, ct))
        {
            return OperationalOutcome.Later(ReportNotSynced, "فحص أمر الصيانة لسه ماوصلش.");
        }

        var status = (RepairStatus)dto.Status;

        /*
          🔴 **الفني اللي استلم الأمر لازم يكون له صلاحية صيانة <u>وقت
          الاستلام</u> — مش وقت الرفع.**
        */
        if (dto.AssignedTechnicianId is { } assignedId && assignedId != Guid.Empty)
        {
            var tech = await repo.FindTechnicianAsync(source.TenantId, assignedId, ct);

            if (tech is null)
                return OperationalOutcome.Reject(
                    TechnicianNotFound, "الفني مش موجود في الشركة دي.");

            DateTime at = dto.ClaimedAtUtc ?? dto.StartedAtUtc ?? dto.OpenedAtUtc;

            if (await DenyAsync(source, tech, tech.CanRepair, at, ct) is { } reason)
                return OperationalOutcome.Reject(CapabilityDenied, reason);
        }

        var row = await repo.FindWorkItemAsync(source.TenantId, dto.Id, ct);

        bool added = row is null;

        if (row is null)
        {
            row = new RepairWorkItem
            {
                Id = dto.Id,
                TenantId = source.TenantId,

                /*
                  🔴 **الموافقة بتتحط هنا بس — عند الإنشاء.**

                  الموافقة بيملكها السيرفر زي الإسناد بالظبط، فالرفعات
                  اللي بعد كده **مابتلمسهاش**: راكة بترفع شغلها كل
                  شوية، ولو كانت بتبعت الموافقة معاها كانت هتدهس قرار
                  المحاسب وترجّع الأمر معلّق بعد ما اتوافق عليه.
                */
                Approval = RepairPolicy.NewOrderApproval,
            };

            repo.AddWorkItem(row);
        }

        /*
          🔴 **الأمر المقفول مابيترجعش لورا.**

          الراكة بترفع الحالة كاملة، والسيرفر بيقبلها لأن الراكة هي
          اللي فرضت ترتيب الانتقالات وهي أوفلاين. بس راكة قديمة — صف
          فضل في طابور أسبوع — ممكن ترفع لقطة أقدم فوق أحدث. الحارس ده
          بيمنع «تمت الصيانة» ترجع «قيد الصيانة».

          ⚠️ ومش رفض: بنرجّع «زي ما هو» عشان الراكة تقفل الصف بدل ما
          تفضل تحاول على حاجة مش هتتغيّر.
        */
        bool closed = row.Status is RepairStatus.Completed
                                 or RepairStatus.UnableToRepair
                                 or RepairStatus.Cancelled;

        if (!added && closed && status != row.Status)
        {
            log.LogWarning(
                "رفع أقدم لأمر صيانة مقفول — أمر {Item}، حالة على السيرفر {Server}، الجاي {Incoming}",
                row.Id, row.Status, status);

            return OperationalOutcome.Same();
        }

        // ⚠️ بالمعرّف **الكانوني** مش اللي الراكة بعتته — المفتاح
        //    الأجنبي بيشاور على جدول الأجهزة، والمعرّف المحلي مالوش صف.
        row.DeviceId = deviceId;
        row.SourceReportId = Empty(dto.SourceReportId);

        // ⚠️ بيتخزّن زي ما هو حتى لو الفحص لسه ماوصلش — مفيش مفتاح
        //    أجنبي عليه، والرابط بيتحل لما الفحص يوصل.
        row.RetestReportId = Empty(dto.RetestReportId);
        row.Status = status;

        /*
          🔴 **راكة قديمة شغّالة على أمر مستني موافقة — بنقبل ونعلّم.**

          الراكات في الميدان بنسخ قديمة مش عارفة الموافقة، وبتشتغل
          أوفلاين. فني بدأ عادي، والشغل وصل بعد ما خلص.

          🔴 **وليه مش رفض:** الرفض في طابور الرفع **نهائي** — الصف
          بيتعلّم «خطأ بيانات» ومابيتحاولش تاني، يعني الشغل اللي اتعمل
          على البنش بيتمسح والسجل الوحيد اللي بيقول إن حد فتح اللاب
          بيضيع. اللاب خلاص اتفك؛ رفض الورقة مابيرجّعهوش.

          ⚠️ **والموافقة مابتتغيّرش** — بتفضل معلّقة، والمحاسب بيقرّر
          وهو شايف إن الشغل اتعمل خلاص. لو حطّيناها «موافَق»
          تلقائياً، أي راكة قديمة كانت بتبقى طريق لتعدية الموافقة
          كلها.
        */
        if (row.Approval == RepairApproval.Pending
            && RepairStatusRules.IsBenchWork(status)
            && !row.StartedWithoutApproval)
        {
            row.StartedWithoutApproval = true;

            log.LogWarning(
                "شغل على أمر صيانة مستني موافقة — أمر {Item}، راكة {Rack}، الحالة {Status}",
                row.Id, source.RackId, status);
        }

        row.RequiredSpecialty = (TechnicianSpecialty)dto.RequiredSpecialty;

        row.AssignedTechnicianId = Empty(dto.AssignedTechnicianId);
        row.CompletedByTechnicianId = Empty(dto.CompletedByTechnicianId);
        row.OpenedByTechnicianId = Empty(dto.OpenedByTechnicianId);

        // ⚠️ الفاعل نوع + معرّف. الأمر الجايّ من راكة فاعله فني دايماً
        //    أو الراكة نفسها.
        row.OpenedByActorType = row.OpenedByTechnicianId.HasValue ? "Technician" : "Rack";
        row.OpenedByName = TextClip.To(dto.OpenedByName, 120);

        row.OpenedAtUtc = dto.OpenedAtUtc == default ? DateTime.UtcNow : dto.OpenedAtUtc;
        row.ClaimedAtUtc = dto.ClaimedAtUtc;
        row.StartedAtUtc = dto.StartedAtUtc;
        row.CompletedAtUtc = dto.CompletedAtUtc;

        row.FaultSummary = dto.FaultSummary ?? "";
        row.RepairActions = dto.RepairActions ?? "";
        row.Notes = dto.Notes ?? "";
        row.OutcomeReason = TextClip.To(dto.OutcomeReason, 400);

        /*
          🔴 **رقم أمر الصيانة بيتوزّع هنا — على السيرفر، ومرة واحدة.**

          والعيب اللي كان في القديم هنا: المسار ده كان بيعمل الصف
          وينساه من غير رقم، وبعدين يقرا الرقم الفاضي ويحطّه في نص
          البحث. أول أمر صيانة حقيقي من الميدان اتخزّن برقم فاضي.

          ⚠️ والشرط «الرقم فاضي» مش «الصف جديد»: كده إعادة الإرسال
          بتحافظ على نفس الرقم، وأي صف قديم اتكتب بالعيب ده بيتصلّح
          لوحده لو اترفع تاني. رقم موجود عمره ما بيتبدل.
        */
        if (string.IsNullOrWhiteSpace(row.PublicCode))
        {
            row.PublicCode = RepairCode.Format(
                await counters.NextAsync(source.TenantId, RepairCode.CounterName, ct));
        }

        /*
          ⚠️ **نسخة المزامنة من نص البحث — من غير كود الجهاز.** نسخة
          الويب (فتح الأمر من الموقع) فيها كود الجهاز. الاتنين موجودين
          في بيانات الإنتاج، واتسابوا منفصلين بالاسم عن قصد.
        */
        row.SearchText = ArabicText.Combine(
            row.PublicCode, row.FaultSummary, row.RepairActions, row.OpenedByName);

        MergeIssues(row, dto.Issues, source.TenantId);
        MergeParts(row, dto.Parts, source.TenantId);

        // ⚠️ القديم بيرجّع «اتطبّق» للجديد وللمتحدّث الاتنين.
        return OperationalOutcome.Ok();
    }

    /// <summary>
    /// بيطابق الأعطال بالمعرّف — <b>مش بيمسح ويعمل من الأول</b>.
    ///
    /// <para>🔴 ده الفرق عن مراحل الفحص: هناك الصفوف بتتحرق في كل
    /// مزامنة فمفاتيحها مش ثابتة؛ هنا المعرّفات بتتولّد على الراكة
    /// وبتعيش، فأي حاجة تشاور عليها بعدين بتفضل صالحة.</para>
    /// </summary>
    private void MergeIssues(
        RepairWorkItem row, List<RepairIssueSyncPayload>? incoming, Guid tenantId)
    {
        if (incoming is null) return;

        foreach (var dto in incoming)
        {
            if (dto.Id == Guid.Empty) continue;

            var existing = row.Issues.FirstOrDefault(i => i.Id == dto.Id);

            if (existing is null)
            {
                existing = new RepairWorkItemIssue
                {
                    Id = dto.Id,
                    TenantId = tenantId,
                    WorkItemId = row.Id,
                };

                // 🔴 إضافة صريحة — شوف IOperationalSyncRepository.AddIssue.
                repo.AddIssue(existing);
                row.Issues.Add(existing);
            }

            existing.IssueCode = TextClip.To(dto.IssueCode, 40);
            existing.IssueTitleSnapshot = TextClip.To(dto.IssueTitleSnapshot, 160);
            existing.Category = TextClip.To(dto.Category, 40);
            existing.Resolved = dto.Resolved;
        }
    }

    private void MergeParts(
        RepairWorkItem row, List<RepairPartSyncPayload>? incoming, Guid tenantId)
    {
        if (incoming is null) return;

        foreach (var dto in incoming)
        {
            if (dto.Id == Guid.Empty) continue;

            var existing = row.Parts.FirstOrDefault(p => p.Id == dto.Id);

            if (existing is null)
            {
                existing = new RepairPart
                {
                    Id = dto.Id,
                    TenantId = tenantId,
                    WorkItemId = row.Id,
                };

                // ⚠️ نفس سبب الأعطال: مفتاح جاهز من الراكة + أب موجود =
                //    Modified بالغلط.
                repo.AddPart(existing);
                row.Parts.Add(existing);
            }

            existing.Name = TextClip.To(dto.Name, 200);
            existing.InventoryCode = TextClip.To(dto.InventoryCode, 60);

            // ⚠️ كمية صفر أو سالبة = واحدة. القطعة اتركّبت.
            existing.Quantity = dto.Quantity <= 0 ? 1 : dto.Quantity;

            existing.SerialNumber = TextClip.To(dto.SerialNumber, 120);
            existing.Notes = dto.Notes ?? "";
        }
    }

    // =================================================================
    //  حركات السجل
    // =================================================================

    public async Task<OperationalOutcome> ApplyWorkflowEventAsync(
        OperationalSource source, DeviceWorkflowEventSyncPayload dto,
        CancellationToken ct = default)
    {
        if (dto.EventId == Guid.Empty)
            return OperationalOutcome.Reject(MissingFields, "حركة من غير رقم حدث.");

        if (dto.DeviceId == Guid.Empty)
            return OperationalOutcome.Reject(MissingFields, "حركة من غير جهاز.");

        DateTime occurredAt = dto.OccurredAtUtc == default ? DateTime.UtcNow : dto.OccurredAtUtc;

        var actor = WorkflowActor.System(RackActorName);

        if (dto.ActorTechnicianId is { } actorId && actorId != Guid.Empty)
        {
            var tech = await repo.FindTechnicianAsync(source.TenantId, actorId, ct);

            if (tech is null)
                return OperationalOutcome.Reject(
                    TechnicianNotFound, "الفني مش موجود في الشركة دي.");

            // 🔴 الحركات اللي جوهرها صيانة محتاجة صلاحية صيانة **وقت
            //    الحركة**.
            var eventType = (DeviceWorkflowEventType)dto.EventType;

            bool needsRepair = eventType is DeviceWorkflowEventType.RepairStarted
                                          or DeviceWorkflowEventType.RepairCompleted;

            if (needsRepair
                && await DenyAsync(source, tech, tech.CanRepair, occurredAt, ct) is { } reason)
            {
                return OperationalOutcome.Reject(CapabilityDenied, reason);
            }

            /*
              ⚠️ **الاسم المبعوت لو موجود، وإلا اسم الفني — والمسافات
              مش اسم.**

              القديم كان بيقص من غير `Trim`، فاسم كله مسافات (طوله ٣)
              كان بيكسب اسم الفني ويتسجّل في خط زمن الجهاز كخانة فاضية.
              ده عكس نيّة السطر نفسه — فرق صغير مقصود.
            */
            string name = TextClip.To((dto.ActorName ?? "").Trim(), 120);

            actor = WorkflowActor.Technician(tech.Id, name.Length > 0 ? name : tech.DisplayName);
        }

        /*
          ⚠️ **والترجمة للكانوني بتحصل جوّه المسجّل نفسه.**

          في القديم مسار الحركات كان بيبعت معرّف الراكة زي ما هو، فحركة
          من راكة جهازها اتعرّف عليه كانت بتاخد «الجهاز لسه ماوصلش»
          **وتتعاد للأبد**. المسجّل في المشروع ده بيترجم قبل ما يدوّر —
          فالفجوة دي مقفولة هنا من المرحلة ٤.
        */
        var result = await workflow.RecordAsync(source.TenantId, new WorkflowMove
        {
            DeviceId = dto.DeviceId,
            EventType = (DeviceWorkflowEventType)dto.EventType,
            Actor = actor,
            ToStage = dto.ToStage.HasValue ? (DeviceOperationalStage)dto.ToStage.Value : null,
            ToTechnicianId = Empty(dto.ToTechnicianId),
            ToLocationId = Empty(dto.ToLocationId),
            ClearsHolder = dto.ClearsHolder,
            RepairWorkItemId = Empty(dto.RepairWorkItemId),
            BaselineReportId = Empty(dto.BaselineReportId),
            BaselineIsFresh = dto.BaselineIsFresh,
            OccurredAtUtc = occurredAt,
            EventId = dto.EventId,
            Reason = dto.Reason ?? "",
            Notes = dto.Notes ?? "",
        }, ct);

        if (result.Ok) return OperationalOutcome.Ok();

        /*
          ⚠️ **«الجهاز مش موجود» رفض مؤقت — باقي الأسباب نهائية.**

          والتفرقة بالنص زي القديم بالحرف — وده هش: لو حد غيّر صياغة
          الرسالة في المسجّل، الرفض المؤقت بيبقى نهائي والشغل بيضيع.
          الثابت `DeviceMissingMessage` هو اللي بيربطهم.
        */
        bool deviceMissing = result.Error!.Contains(
            DeviceMissingMessage, StringComparison.Ordinal);

        return deviceMissing
            ? OperationalOutcome.Later(
                DeviceNotSynced, "جهاز الحركة لسه ماوصلش. ابعت الجهاز الأول.")
            : OperationalOutcome.Reject(WorkflowRejected, result.Error);
    }

    /// <summary>
    /// 🔴 <b>النص اللي المسجّل بيرجّعه لما الجهاز مش موجود</b> — والرفض
    /// المؤقت متعلّق بيه. فحص بيقارنه برسالة المسجّل الحقيقية.
    /// </summary>
    public const string DeviceMissingMessage = "الجهاز مش موجود";

    private static Guid? Empty(Guid? value) =>
        value is null || value == Guid.Empty ? null : value;
}
