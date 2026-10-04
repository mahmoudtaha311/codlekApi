using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;
using Codlek.Core.Text;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Repairs;

/// <summary>
/// انتقالات أمر الصيانة — <b>مكان واحد للنقط وللمزامنة</b>.
/// </summary>
public sealed class RepairTransitions(
    IRepairRepository repairs,
    IDeviceWorkflowRecorder workflow,
    ILogger<RepairTransitions> log) : IRepairTransitions
{
    // =================================================================
    //  الفني
    // =================================================================

    public async Task<Result<Technician>> ResolveTechnicianAsync(
        Guid tenantId, Guid technicianId, Guid? deviceId,
        RepairPolicy.BrandCheck brandCheck, CancellationToken ct = default)
    {
        var tech = await repairs.FindTechnicianAsync(tenantId, technicianId, ct);

        /*
          ⚠️ **الترتيب ده منقول بالحرف.**

          «مش موجود» وبعدين «موقوف» وبعدين «مالوش صلاحية» وبعدين
          الماركة. أي ترتيب تاني بيدّي رسالة مختلفة لنفس الطلب —
          والمدير بيتصرّف على حسب الرسالة.
        */
        if (tech is null) return Result.Failure<Technician>(RepairErrors.TechnicianNotFound);
        if (!tech.IsActive) return Result.Failure<Technician>(RepairErrors.TechnicianSuspended);

        if (!tech.CanRepair)
            return Result.Failure<Technician>(RepairErrors.TechnicianCannotRepair);

        /*
          🔴 **الخروج بدري هنا بيوفّر استعلامين لكل إسناد من المحاسب.**

          `Skip` معناها «الشغل اتعمل خلاص» أو «المحاسب بيعدّي» — وفي
          الحالتين قراية الماركات مالهاش لازمة.
        */
        if (brandCheck == RepairPolicy.BrandCheck.Skip || deviceId is not { } device)
            return Result.Success(tech);

        var allowed = await repairs.TechnicianBrandsAsync(tenantId, tech.Id, ct);

        /*
          ⚠️ **والخروج التاني: فني من غير قيد.**

          القايمة الفاضية معناها «مفتوح على كل حاجة» — وده الوضع
          الطبيعي لأغلب الفنيين. فقراية ماركة الجهاز مالهاش لازمة.
        */
        if (allowed.Count == 0) return Result.Success(tech);

        var rules = await repairs.BrandRulesAsync(tenantId, ct);
        string manufacturer = await repairs.DeviceManufacturerAsync(tenantId, device, ct);

        var resolved = BrandToken.Resolve(rules, manufacturer);

        if (RepairBrandGate.CanAssign(allowed, resolved)) return Result.Success(tech);

        return Result.Failure<Technician>(
            RepairErrors.TechnicianOutsideBrand(resolved.Name));
    }

    // =================================================================
    //  الإسناد
    // =================================================================

    public async Task<Result<RepairWorkItem>> AssignAsync(
        Guid tenantId, Guid workItemId, Guid technicianId, WorkflowActor actor,
        bool allowOutsideBrand, CancellationToken ct = default)
    {
        var item = await repairs.FindAsync(tenantId, workItemId, ct);

        if (item is null) return Result.Failure<RepairWorkItem>(RepairErrors.NotFound);

        // 🔴 عضوية صريحة — مفيش `>=` على الأرقام.
        if (RepairClosure.IsClosed(item.Status))
            return Result.Failure<RepairWorkItem>(RepairErrors.ClosedCannotAssign);

        var resolved = await ResolveTechnicianAsync(
            tenantId, technicianId, item.DeviceId,
            allowOutsideBrand ? RepairPolicy.BrandCheck.Skip : RepairPolicy.BrandCheck.Enforce,
            ct);

        if (resolved.IsFailure) return Result.Failure<RepairWorkItem>(resolved.Error);

        var tech = resolved.Value!;

        // ⚠️ بيتحسب **قبل** الكتابة — بعدها الشرط بيبقى دايماً false.
        bool changed = item.AssignedTechnicianId != tech.Id;

        /*
          🔴 **التعدية بتتسجّل على الأمر نفسه، ومن غير شرط.**

          حتى لو الفني كان جوّه ماركاته أصلاً: اللي اتسجّل هو إن
          <b>الإسناد ده اتعمل بسلطة محاسب</b> — وده اللي بيتسأل عنه
          بعدين، مش إذا كانت الماركة طابقت بالصدفة.
        */
        if (allowOutsideBrand) item.BrandOverride = true;

        item.AssignedTechnicianId = tech.Id;
        item.ClaimedAtUtc ??= DateTime.UtcNow;

        if (item.Status == RepairStatus.New) item.Status = RepairStatus.WaitingForRepair;

        /*
          ⚠️ **الحيازة بتتنقل بس لو الفني فعلاً اتغيّر.**

          إعادة إسناد لنفس الشخص مش حركة تسليم — وسجل الحركة لازم
          يفضل قابل للقراءة.
        */
        if (changed)
        {
            var move = await workflow.RecordAsync(tenantId, new WorkflowMove
            {
                DeviceId = item.DeviceId,
                EventType = DeviceWorkflowEventType.CustodyHandoff,
                Actor = actor,
                ToTechnicianId = tech.Id,
                RepairWorkItemId = item.Id,
                Reason = "إسناد أمر صيانة",

                // ⚠️ **ومفيش `ToStage`** — الإسناد مش بيغيّر مرحلة
                // الجهاز، فـ`StageChangedAtUtc` مابيتلمسش.
            }, ct);

            if (!move.Ok)
                return Result.Failure<RepairWorkItem>(
                    new Error("repair.workflow_refused", move.Error!, 400));
        }

        return Result.Success(item);
    }

    // =================================================================
    //  البدء
    // =================================================================

    public async Task<Result<RepairWorkItem>> StartAsync(
        Guid tenantId, Guid workItemId, Guid technicianId, WorkflowActor actor,
        DateTime? startedAtUtc = null, CancellationToken ct = default)
    {
        var item = await repairs.FindAsync(tenantId, workItemId, ct);

        if (item is null) return Result.Failure<RepairWorkItem>(RepairErrors.NotFound);

        if (!RepairStatusRules.CanMove(item.Status, RepairStatus.InProgress))
            return Result.Failure<RepairWorkItem>(RepairErrors.CannotStartFrom(item.Status));

        /*
          🔴 **حاجز الموافقة — وده كان أوسع باب في الميزة كلها.**

          من غيره، المالك بيقدر يبدأ أمر معلّق ويعدّي على المحاسب
          **في صمت**: السجل بيكتب «تجاوز إداري لبدء الصيانة»،
          ومابيقولش إن الموافقة مااتخدتش أصلاً.
        */
        var block = RepairApprovalGate.CanStart(RepairPolicy.ApprovalEnforced, item.Approval);

        if (block is not RepairApprovalGate.StartBlock.None)
        {
            return Result.Failure<RepairWorkItem>(
                block == RepairApprovalGate.StartBlock.Rejected
                    ? RepairErrors.AlreadyRejected
                    : RepairErrors.NotApprovedYet);
        }

        /*
          ⚠️ **والقيد بيتطبّق دايماً على البدء** — المالك مايعدّيهوش.
          هو بيعدّي الموافقة (وبيسجّلها)، مش الماركة.
        */
        var resolved = await ResolveTechnicianAsync(
            tenantId, technicianId, item.DeviceId, RepairPolicy.BrandCheck.Enforce, ct);

        if (resolved.IsFailure) return Result.Failure<RepairWorkItem>(resolved.Error);

        var tech = resolved.Value!;

        /*
          🔴 **البدء ساكن — وفحص التكرار في الآخر عن قصد.**

          إعادة رفع نفس الأمر بعد انقطاع مالهاش تحرّك وقت البداية ولا
          تعمل حركة تانية.

          ⚠️ **وعشان الفحص في الآخر، الإعادة بتعيد التحقق من
          الماركة.** يعني لو الفني اتشالت منه الماركة في الوقت ده،
          الإعادة بتفشل بدل ما ترجّع الأمر الموجود. ده سلوك القديم
          بالحرف، وعكس ترتيب القفل والإلغاء — <b>وممنوع يتوحّد</b>.
        */
        if (item.Status == RepairStatus.InProgress && item.StartedAtUtc.HasValue)
            return Result.Success(item);

        // ⚠️ `??=` مش `=`: البدء مابيعيدش إسناد أمر مسنود لحد تاني.
        item.AssignedTechnicianId ??= tech.Id;
        item.Status = RepairStatus.InProgress;
        item.StartedAtUtc = startedAtUtc ?? DateTime.UtcNow;
        item.ClaimedAtUtc ??= item.StartedAtUtc;

        var move = await workflow.RecordAsync(tenantId, new WorkflowMove
        {
            DeviceId = item.DeviceId,
            EventType = DeviceWorkflowEventType.RepairStarted,
            Actor = actor,
            ToStage = DeviceOperationalStage.UnderRepair,
            ToTechnicianId = tech.Id,
            RepairWorkItemId = item.Id,

            // ⚠️ بوقت البدء مش بوقت التسجيل — راكة أوفلاين.
            OccurredAtUtc = item.StartedAtUtc,
        }, ct);

        if (!move.Ok)
            return Result.Failure<RepairWorkItem>(
                new Error("repair.workflow_refused", move.Error!, 400));

        return Result.Success(item);
    }

    // =================================================================
    //  الإلغاء
    // =================================================================

    public async Task<Result<RepairWorkItem>> CancelAsync(
        Guid tenantId, Guid workItemId, string? reason, CancellationToken ct = default)
    {
        var item = await repairs.FindAsync(tenantId, workItemId, ct);

        if (item is null) return Result.Failure<RepairWorkItem>(RepairErrors.NotFound);

        /*
          🔴 **نجاح ساكن بدري — والسبب مابيتفحصش.**

          أمر ملغي خلاص بيرجّع نجاح على طول. ومعنى إن الفحص ده **قبل**
          فحص السبب إن إعادة الإلغاء من غير سبب بترجّع ٢٠٠ وبتكتب سطر
          تاني في السجل سببه فاضي.

          ⚠️ **ده باج في القديم، ومنقول زي ما هو بقرار صريح.** تصليحه
          في الجديد لوحده بيخلّي نفس الطلب يدّي ردّين مختلفين من
          اللوحتين وهما شغّالين على نفس القاعدة.

          ⚠️ وعشان كده فحص السبب **ماينفعش يروح للمتحقّق**: المتحقّق
          بيشتغل قبل المعالج، فكان هيرفض الإعادة اللي القديم بيقبلها.
        */
        if (item.Status == RepairStatus.Cancelled)
        {
            log.LogInformation(
                "إلغاء مكرّر على أمر ملغي خلاص — {PublicCode}. سلوك منقول من القديم.",
                item.PublicCode);

            return Result.Success(item);
        }

        /*
          ⚠️ **وأمر شغّال مايتلغيش.** الجدول مافيهوش
          <c>InProgress → Cancelled</c> — اللي بيتعمل هو «تعذر
          الإصلاح». والفرق بيبان في التقارير: الملغي يعني محدش لمسه،
          و«تعذر» يعني حد اشتغل ومعرفش.
        */
        if (!RepairStatusRules.CanMove(item.Status, RepairStatus.Cancelled))
            return Result.Failure<RepairWorkItem>(RepairErrors.CannotCancelFrom(item.Status));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<RepairWorkItem>(RepairErrors.CancelReasonRequired);

        item.Status = RepairStatus.Cancelled;
        item.OutcomeReason = TextClip.To(reason, TextClip.Lengths.Reason);

        /*
          ⚠️ **ومفيش حركة جهاز خالص.**

          الإلغاء مش بيحرّك اللاب: هو لسه في نفس المكان ومع نفس
          الشخص. وحركة وهمية هنا كانت بتخلّي «اللاب ده اتنقل كام
          مرة» رقم مالوش معنى.
        */
        return Result.Success(item);
    }

    // =================================================================
    //  التعديل والقفل — مفيش نقط HTTP عليهم
    // =================================================================

    public async Task<Result<RepairWorkItem>> UpdateWorkAsync(
        Guid tenantId, Guid workItemId,
        string? diagnosis, string? actions, string? notes,
        CancellationToken ct = default)
    {
        var item = await repairs.FindAsync(tenantId, workItemId, ct);

        if (item is null) return Result.Failure<RepairWorkItem>(RepairErrors.NotFound);

        // ⚠️ «مينفعش يتعدّل» — رسالة تانية خلاف «مينفعش يتسند»، وهي
        // نفس الشرط. منقولة لكل مكان نداء بنصّه.
        if (RepairClosure.IsClosed(item.Status))
            return Result.Failure<RepairWorkItem>(RepairErrors.ClosedCannotEdit);

        /*
          🔴 **<c>null</c> يعني «مابعتّوش»، والنص الفاضي **بيمسح**.**

          ومفيش <c>IsNullOrWhiteSpace</c> في ولا سطر هنا عن قصد: فني
          مسح وصف العطل بإيده لازم يتمسح فعلاً. ولو حطّينا الحاجز،
          الفني بيمسح والشاشة بتفضل عارضة القديم — وهو فاكر إنه عدّل.
        */
        if (diagnosis is not null) item.FaultSummary = diagnosis;
        if (actions is not null) item.RepairActions = actions;
        if (notes is not null) item.Notes = notes;

        /*
          ⚠️ **و<c>SearchText</c> مابيتبنيش تاني — وده باج منقول.**

          وصف العطل داخل في نص البحث، فبعد أي تعديل البحث بيفضل
          لاقي الأمر بالوصف **القديم**. السلوك ده موجود في بيانات
          الإنتاج دلوقتي، وتصليحه هنا لوحده بيخلّي نفس الصف يطلع في
          بحث لوحة ومايطلعش في التانية.
        */
        return Result.Success(item);
    }

    public async Task<Result<RepairWorkItem>> CompleteAsync(
        Guid tenantId, Guid workItemId, Guid technicianId,
        string actions, string? notes, WorkflowActor actor,
        DateTime? completedAtUtc = null, CancellationToken ct = default)
    {
        var item = await repairs.FindAsync(tenantId, workItemId, ct);

        if (item is null) return Result.Failure<RepairWorkItem>(RepairErrors.NotFound);

        /*
          🔴 **الإعادة الأول — قبل جدول الانتقالات.**

          الراكة بتبعت القفل من طابورها، والشبكة بتقطع بعد ما
          السيرفر يكتب. فالإعادة لازم ترجّع نجاح، وإلا الراكة بتفضل
          معلّقة على صف **اتحفظ خلاص**.
        */
        if (item.Status == RepairStatus.Completed) return Result.Success(item);

        if (!RepairStatusRules.CanMove(item.Status, RepairStatus.Completed))
            return Result.Failure<RepairWorkItem>(RepairErrors.CannotCloseFrom(item.Status));

        // ⚠️ اللي اتعمل إجباري — سجل صيانة من غيره مالوش قيمة للعميل.
        if (string.IsNullOrWhiteSpace(actions))
            return Result.Failure<RepairWorkItem>(RepairErrors.WorkDescriptionRequired);

        /*
          🔴 **ومفيش فحص ماركة خالص — <c>Skip</c> بالاسم.</b>

          اللاب اتصلّح خلاص. رفض التسجيل مش بيرجّع الشغل، هو بيمسح
          **الدليل** إن الشغل حصل — والفني بيفضل مسؤول عن أمر مفتوح
          هو قافله فعلاً.
        */
        var resolved = await ResolveTechnicianAsync(
            tenantId, technicianId, deviceId: null,
            RepairPolicy.BrandCheck.Skip, ct);

        if (resolved.IsFailure)
            return Result.Failure<RepairWorkItem>(resolved.Error);

        item.Status = RepairStatus.Completed;
        item.RepairActions = actions;

        if (notes is not null) item.Notes = notes;

        // ⚠️ وقت الراكة الحقيقي لو بعتته — الشغل الأوفلاين بيتسجّل
        // بوقته مش بوقت وصول الطابور.
        item.CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;
        item.CompletedByTechnicianId = resolved.Value.Id;

        /*
          🔴 **وبداية ناقصة بتتعوّض من وقت القفل.**

          أمر اتقفل من غير ما يتبدأ (مسار المزامنة بيعمل كده) كان
          بيسيب <c>StartedAtUtc</c> فاضي — و<c>DurationMs</c> بترجّع
          <c>null</c>، فتقرير «متوسط زمن الصيانة» بيستبعد الصف ساكت.
        */
        item.StartedAtUtc ??= item.CompletedAtUtc;

        await workflow.RecordAsync(tenantId, new WorkflowMove
        {
            DeviceId = item.DeviceId,
            EventType = DeviceWorkflowEventType.RepairCompleted,
            Actor = actor,
            ToStage = DeviceOperationalStage.Ready,
            RepairWorkItemId = item.Id,
            OccurredAtUtc = item.CompletedAtUtc,

            /*
              ⚠️ **ومفيش <c>ClearsHolder</c> ومفيش فني جديد.**

              القفل مش بيسلّم اللاب لحد: هو لسه في إيد الفني لحد ما
              التسليم يحصل بإجراء منفصل. ومسح الحائز هنا كان بيخلّي
              اللاب «مع محدش» وهو في الحقيقة على البنش.
            */
        }, ct);

        return Result.Success(item);
    }

    public async Task<Result<RepairWorkItem>> MarkUnableAsync(
        Guid tenantId, Guid workItemId, Guid technicianId,
        string reason, WorkflowActor actor,
        DateTime? atUtc = null, CancellationToken ct = default)
    {
        var item = await repairs.FindAsync(tenantId, workItemId, ct);

        if (item is null) return Result.Failure<RepairWorkItem>(RepairErrors.NotFound);

        // 🔴 الإعادة الأول — نفس سبب القفل الناجح.
        if (item.Status == RepairStatus.UnableToRepair) return Result.Success(item);

        // ⚠️ ونفس رسالة القفل بالحرف — المصدر واحد عن قصد.
        if (!RepairStatusRules.CanMove(item.Status, RepairStatus.UnableToRepair))
            return Result.Failure<RepairWorkItem>(RepairErrors.CannotCloseFrom(item.Status));

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<RepairWorkItem>(RepairErrors.UnableReasonRequired);

        var resolved = await ResolveTechnicianAsync(
            tenantId, technicianId, deviceId: null,
            RepairPolicy.BrandCheck.Skip, ct);

        if (resolved.IsFailure)
            return Result.Failure<RepairWorkItem>(resolved.Error);

        item.Status = RepairStatus.UnableToRepair;
        item.OutcomeReason = TextClip.To(reason, TextClip.Lengths.Reason);
        item.CompletedAtUtc = atUtc ?? DateTime.UtcNow;
        item.CompletedByTechnicianId = resolved.Value.Id;
        item.StartedAtUtc ??= item.CompletedAtUtc;

        await workflow.RecordAsync(tenantId, new WorkflowMove
        {
            DeviceId = item.DeviceId,

            /*
              🔴 **نفس نوع الحركة بتاع القفل الناجح — <c>RepairCompleted</c>.</b>

              يعني أي تقرير بيعدّ «الصيانات اللي خلصت» بـ<c>EventType == 5</c>
              بيعدّ **اللي فشلت** معاها. اللي بيفرّق هو
              <c>ToStage</c> أو حالة الأمر المربوط — مش نوع الحركة.

              ⚠️ ومنقول زي ما هو: نوع تاني بيخلّي الراكة تشوف رقم
              مش موجود في جدولها، وهي بتقرا الأرقام من على السلك.
            */
            EventType = DeviceWorkflowEventType.RepairCompleted,
            Actor = actor,

            // ⚠️ ورجوع لـ«محتاج صيانة» مش «جاهز» — اللاب لسه بايظ.
            ToStage = DeviceOperationalStage.NeedsRepair,
            RepairWorkItemId = item.Id,
            OccurredAtUtc = item.CompletedAtUtc,
            Reason = TextClip.To(reason, TextClip.Lengths.Reason),
        }, ct);

        return Result.Success(item);
    }
}
