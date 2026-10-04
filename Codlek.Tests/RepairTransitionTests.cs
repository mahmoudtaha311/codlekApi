using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Features.Repairs;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// انتقالات أمر الصيانة.
///
/// <para>🔴 <b>الانتقالات دي مش بتتنده من HTTP بس — مسار المزامنة
/// من الراكة بيوصل لنفسها.</b> فأي قاعدة بتتكسر هنا بتتكسر
/// <b>لفني أوفلاين</b> كمان، واللي مفيش حد بيراجع شغله لحظة
/// بلحظة.</para>
/// </summary>
public class RepairTransitionTests
{
    private static (RepairTransitions Transitions,
                    FakeRepairRepository Repo,
                    FakeWorkflowRecorder Workflow,
                    Guid Tenant) Build()
    {
        var repo = new FakeRepairRepository();
        var workflow = new FakeWorkflowRecorder();

        return (new RepairTransitions(repo, workflow, NullLogger<RepairTransitions>.Instance),
                repo, workflow, Guid.NewGuid());
    }

    private static WorkflowActor Actor() => WorkflowActor.User(Guid.NewGuid(), "كريم");

    // =================================================================
    //  التعديل
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>null</c> يعني «مابعتّوش»، والنص الفاضي
    /// <b>بيمسح</b>.</b>
    ///
    /// <para>ومفيش <c>IsNullOrWhiteSpace</c> في ولا سطر عن قصد: فني
    /// مسح وصف العطل بإيده لازم يتمسح فعلاً. ولو حطّينا الحاجز،
    /// الفني بيمسح والشاشة بتفضل عارضة القديم — وهو فاكر إنه
    /// عدّل.</para>
    /// </summary>
    [Fact]
    public async Task Update_treats_null_as_untouched_and_empty_as_a_wipe()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);
        item.FaultSummary = "الشاشة";
        item.RepairActions = "اتغيرت الشاشة";
        item.Notes = "ملاحظة قديمة";

        repo.Devices.Add(device);
        repo.Items.Add(item);

        var result = await transitions.UpdateWorkAsync(
            tenant, item.Id, diagnosis: "", actions: null, notes: "جديدة");

        Assert.True(result.IsSuccess);

        // النص الفاضي مسح.
        Assert.Equal("", item.FaultSummary);

        // و`null` ساب القديم زي ما هو.
        Assert.Equal("اتغيرت الشاشة", item.RepairActions);

        Assert.Equal("جديدة", item.Notes);
    }

    /// <summary>
    /// ⚠️ <b>و<c>SearchText</c> مابيتبنيش تاني — باج منقول بقرار.</b>
    ///
    /// <para>وصف العطل داخل في نص البحث، فبعد أي تعديل البحث بيفضل
    /// لاقي الأمر بالوصف <b>القديم</b>. السلوك ده موجود في بيانات
    /// الإنتاج، وتصليحه هنا لوحده بيخلّي نفس الصف يطلع في بحث لوحة
    /// ومايطلعش في التانية.</para>
    /// </summary>
    [Fact]
    public async Task Update_leaves_the_search_text_stale_on_purpose()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);
        item.SearchText = "الوصف القديم";

        repo.Devices.Add(device);
        repo.Items.Add(item);

        await transitions.UpdateWorkAsync(tenant, item.Id, "وصف جديد خالص", null, null);

        Assert.Equal("الوصف القديم", item.SearchText);
    }

    [Theory]
    [InlineData(RepairStatus.Completed)]
    [InlineData(RepairStatus.UnableToRepair)]
    [InlineData(RepairStatus.Cancelled)]
    public async Task A_closed_order_cannot_be_edited(RepairStatus status)
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, status);

        repo.Devices.Add(device);
        repo.Items.Add(item);

        var result = await transitions.UpdateWorkAsync(tenant, item.Id, "حاجة", null, null);

        Assert.True(result.IsFailure);

        // ⚠️ «مينفعش يتعدّل» — رسالة تانية خلاف «مينفعش يتسند».
        Assert.Equal("repair.closed_cannot_update", result.Error.Code);
    }

    // =================================================================
    //  القفل بنجاح
    // =================================================================

    [Fact]
    public async Task Complete_writes_the_outcome_and_records_a_ready_move()
    {
        var (transitions, repo, workflow, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);
        item.StartedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var closedAt = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc);

        var result = await transitions.CompleteAsync(
            tenant, item.Id, tech.Id, "اتغيرت الشاشة", "تمام", Actor(), closedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(RepairStatus.Completed, item.Status);
        Assert.Equal("اتغيرت الشاشة", item.RepairActions);
        Assert.Equal(closedAt, item.CompletedAtUtc);
        Assert.Equal(tech.Id, item.CompletedByTechnicianId);

        var move = Assert.Single(workflow.Moves);

        Assert.Equal(DeviceWorkflowEventType.RepairCompleted, move.EventType);
        Assert.Equal(DeviceOperationalStage.Ready, move.ToStage);

        /*
          🔴 <b>القفل مش بيسلّم اللاب لحد.</b>

          هو لسه في إيد الفني لحد ما التسليم يحصل بإجراء منفصل. ومسح
          الحائز هنا كان بيخلّي اللاب «مع محدش» وهو في الحقيقة على
          البنش.
        */
        Assert.False(move.ClearsHolder);
        Assert.Null(move.ToTechnicianId);
    }

    /// <summary>
    /// 🔴 <b>الإعادة بترجّع نجاح — والشرط ده قبل جدول
    /// الانتقالات.</b>
    ///
    /// <para>الراكة بتبعت القفل من طابورها، والشبكة بتقطع بعد ما
    /// السيرفر يكتب. فالإعادة لازم ترجّع نجاح، وإلا الراكة بتفضل
    /// معلّقة على صف <b>اتحفظ خلاص</b> وبتعيد للأبد.</para>
    /// </summary>
    [Fact]
    public async Task Completing_an_already_completed_order_succeeds_and_changes_nothing()
    {
        var (transitions, repo, workflow, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.Completed);
        item.RepairActions = "اللي اتعمل الأصلي";

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var result = await transitions.CompleteAsync(
            tenant, item.Id, tech.Id, "حاجة تانية خالص", null, Actor());

        Assert.True(result.IsSuccess);

        // مااتغيّرش حرف.
        Assert.Equal("اللي اتعمل الأصلي", item.RepairActions);

        // ⚠️ ومفيش حركة تانية اتكتبت — غير كده «اللاب ده اتحرك كام
        // مرة» بيبقى رقم مالوش معنى.
        Assert.Empty(workflow.Moves);
    }

    [Fact]
    public async Task Complete_refuses_an_empty_work_description()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var result = await transitions.CompleteAsync(
            tenant, item.Id, tech.Id, "   ", null, Actor());

        Assert.True(result.IsFailure);
        Assert.Equal("repair.actions_required", result.Error.Code);
    }

    /// <summary>
    /// 🔴 <b>ومفيش فحص ماركة على القفل خالص.</b>
    ///
    /// <para>اللاب اتصلّح خلاص. رفض التسجيل مش بيرجّع الشغل، هو بيمسح
    /// <b>الدليل</b> إن الشغل حصل — والفني بيفضل مسؤول عن أمر مفتوح
    /// هو قافله فعلاً.</para>
    /// </summary>
    [Fact]
    public async Task Complete_ignores_the_brand_rule()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant, manufacturer: "HP");
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var dell = Guid.NewGuid();
        repo.Rules.Add(new(dell, "Dell", []));

        // ⚠️ الفني ده مخصّص لـDell واللاب HP — ولازم يعدّي بردو.
        repo.TechBrands[tech.Id] = [dell];

        var result = await transitions.CompleteAsync(
            tenant, item.Id, tech.Id, "اتصلّح", null, Actor());

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// 🔴 <b>بداية ناقصة بتتعوّض من وقت القفل.</b>
    ///
    /// <para>صف حالته «قيد الصيانة» و<c>StartedAtUtc</c> فاضي حاجة
    /// <b>موجودة فعلاً</b>: مسار المزامنة من الراكة بيكتب الحالة
    /// مباشرة، وبيانات قديمة من قبل ما العمود ده ينضاف. ومن غير
    /// التعويض، <c>DurationMs</c> بترجّع <c>null</c> وتقرير «متوسط
    /// زمن الصيانة» بيستبعد الصف <b>ساكت</b>.</para>
    ///
    /// <para>⚠️ ولاحظ إن الحالة هنا «قيد الصيانة» مش «بانتظار
    /// الصيانة»: جدول الانتقالات مافيهوش
    /// <c>WaitingForRepair → Completed</c> أصلاً — القفل من الطابور
    /// مباشرةً مرفوض، وده مقصود.</para>
    /// </summary>
    [Fact]
    public async Task Closing_an_order_with_no_recorded_start_backfills_it()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        Assert.Null(item.StartedAtUtc);

        var result = await transitions.CompleteAsync(
            tenant, item.Id, tech.Id, "اتصلّح", null, Actor());

        Assert.True(result.IsSuccess);
        Assert.NotNull(item.CompletedAtUtc);
        Assert.Equal(item.CompletedAtUtc, item.StartedAtUtc);
        Assert.Equal(0, RepairTiming.DurationMs(item.StartedAtUtc, item.CompletedAtUtc));
    }

    /// <summary>
    /// ⚠️ والقفل من الطابور مباشرةً مرفوض — لازم حد يبدأ الأول.
    /// </summary>
    [Fact]
    public async Task An_order_still_in_the_queue_cannot_be_closed()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.WaitingForRepair);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var result = await transitions.CompleteAsync(
            tenant, item.Id, tech.Id, "اتصلّح", null, Actor());

        Assert.True(result.IsFailure);
        Assert.Equal("repair.cannot_close_from_status", result.Error.Code);
    }

    // =================================================================
    //  تعذّر الإصلاح
    // =================================================================

    /// <summary>
    /// 🔴 <b>نفس نوع الحركة بتاع القفل الناجح.</b>
    ///
    /// <para>أي تقرير بيعدّ «الصيانات اللي خلصت» بنوع الحركة بيعدّ
    /// <b>اللي فشلت</b> معاها. اللي بيفرّق هو <c>ToStage</c> أو حالة
    /// الأمر المربوط — مش نوع الحركة.</para>
    ///
    /// <para>⚠️ ومنقول زي ما هو: نوع تاني بيخلّي الراكة تشوف رقم مش
    /// موجود في جدولها، وهي بتقرا الأرقام من على السلك.</para>
    /// </summary>
    [Fact]
    public async Task Unable_writes_the_same_event_type_as_a_successful_close()
    {
        var (transitions, repo, workflow, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var result = await transitions.MarkUnableAsync(
            tenant, item.Id, tech.Id, "البوردة محروقة", Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(RepairStatus.UnableToRepair, item.Status);
        Assert.Equal("البوردة محروقة", item.OutcomeReason);

        var move = Assert.Single(workflow.Moves);

        Assert.Equal(DeviceWorkflowEventType.RepairCompleted, move.EventType);

        // ⚠️ ورجوع لـ«محتاج صيانة» مش «جاهز» — اللاب لسه بايظ.
        Assert.Equal(DeviceOperationalStage.NeedsRepair, move.ToStage);
        Assert.Equal("البوردة محروقة", move.Reason);
    }

    [Fact]
    public async Task Unable_refuses_an_empty_reason_and_is_idempotent()
    {
        var (transitions, repo, workflow, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);

        var open = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);
        var closed = RepairFixtures.Item(tenant, device.Id, RepairStatus.UnableToRepair);
        closed.OutcomeReason = "السبب الأصلي";

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(open);
        repo.Items.Add(closed);

        var blank = await transitions.MarkUnableAsync(tenant, open.Id, tech.Id, "  ", Actor());
        var replay = await transitions.MarkUnableAsync(
            tenant, closed.Id, tech.Id, "سبب تاني", Actor());

        Assert.True(blank.IsFailure);
        Assert.Equal("repair.unable_reason_required", blank.Error.Code);

        Assert.True(replay.IsSuccess);
        Assert.Equal("السبب الأصلي", closed.OutcomeReason);
        Assert.Empty(workflow.Moves);
    }

    /// <summary>
    /// ⚠️ <b>ونفس رسالة القفل بالحرف.</b> المصدر واحد
    /// (<c>CannotCloseFrom</c>) عشان الشاشة تقول نفس الكلام في
    /// الحالتين.
    /// </summary>
    [Fact]
    public async Task Both_closing_paths_share_one_refusal_message()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);

        var a = RepairFixtures.Item(tenant, device.Id, RepairStatus.Cancelled);
        var b = RepairFixtures.Item(tenant, device.Id, RepairStatus.Cancelled);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(a);
        repo.Items.Add(b);

        var complete = await transitions.CompleteAsync(
            tenant, a.Id, tech.Id, "اتصلّح", null, Actor());

        var unable = await transitions.MarkUnableAsync(tenant, b.Id, tech.Id, "فشل", Actor());

        Assert.True(complete.IsFailure);
        Assert.True(unable.IsFailure);
        Assert.Equal(complete.Error.Code, unable.Error.Code);
        Assert.Equal(complete.Error.Description, unable.Error.Description);
    }

    /// <summary>
    /// 🔴 الفني المش موجود بيرجّع نفس رسالة الفني بتاع شركة تانية —
    /// الفرق بيأكّد لحد بره إن المعرّف حقيقي.
    /// </summary>
    [Fact]
    public async Task Closing_with_an_unknown_technician_is_refused()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);

        repo.Devices.Add(device);
        repo.Items.Add(item);

        var result = await transitions.CompleteAsync(
            tenant, item.Id, Guid.NewGuid(), "اتصلّح", null, Actor());

        Assert.True(result.IsFailure);
        Assert.Equal("repair.technician_not_found", result.Error.Code);
    }

    /// <summary>
    /// ⚠️ ومفيش <c>SaveChanges</c> في ولا انتقال — الحفظ مسؤولية
    /// المنادي عشان السجل والانتقال والحركة ينزلوا مع بعض.
    /// </summary>
    [Fact]
    public async Task Transitions_never_save()
    {
        var (transitions, repo, _, tenant) = Build();

        var device = RepairFixtures.Device(tenant);
        var tech = RepairFixtures.Technician(tenant);
        var item = RepairFixtures.Item(tenant, device.Id, RepairStatus.InProgress);

        repo.Devices.Add(device);
        repo.Technicians.Add(tech);
        repo.Items.Add(item);

        var unitOfWork = new FakeUnitOfWork();

        await transitions.CompleteAsync(tenant, item.Id, tech.Id, "اتصلّح", null, Actor());

        Assert.Equal(0, unitOfWork.Saves);
    }
}
