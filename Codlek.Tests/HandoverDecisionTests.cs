using Codlek.Application.Contracts.Handover;
using Codlek.Application.Contracts.Workflow;
using Codlek.Application.Features.Handover;
using Codlek.Application.Features.Handover.ExecuteHandover;
using Codlek.Application.Features.Handover.MarkReady;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Handover;

namespace Codlek.Tests;

/// <summary>
/// قرارات التسليم — <b>الحاجز والمخرج المسجّل</b>.
///
/// <para>🔴 <b>الحاجة اللي الملف ده بيحميها:</b> «عدّى الفحص» حكم
/// آلي، و«جاهز يمشي» حكم بني آدم. والباب المقفول بالكامل بيتحايل
/// عليه — فالباب مفتوح <b>بشرط إنه يتكتب</b>، والفحوص دي بتثبّت إن
/// الشرط مش شكل.</para>
/// </summary>
public class HandoverDecisionTests
{
    /// <summary>
    /// مستودع تسليم بديل — كل حاجة في الذاكرة.
    ///
    /// <para>⚠️ <b>بيثبت القرار مش الاستعلام.</b> شرط الأهلية نفسه
    /// محتاج قاعدة حقيقية — راجع
    /// <see cref="HandoverEligibilityTests"/>.</para>
    /// </summary>
    private sealed class FakeHandoverRepository : IHandoverRepository
    {
        public readonly List<Location> Destinations = [];
        public readonly List<Device> Devices = [];
        public readonly HashSet<Guid> Eligible = [];

        public Task<IReadOnlyList<HandoverDestination>> DestinationsAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HandoverDestination>>(
                Destinations.Where(l => l.IsActive)
                    .Select(l => new HandoverDestination(
                        l.Id, l.Code, l.Name, l.Kind.ToString()))
                    .ToList());

        public Task<Location?> FindDestinationAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Destinations.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<Guid>> EligibleIdsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(ids.Where(Eligible.Contains).ToList());

        public Task<IReadOnlyList<string>> CodesAsync(
            Guid t, IReadOnlyCollection<Guid> ids, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                Devices.Where(d => ids.Contains(d.Id))
                    .OrderBy(d => d.PublicCode)
                    .Select(d => d.PublicCode)
                    .Take(take)
                    .ToList());

        public Task<IReadOnlyList<string>> UnreviewedCodesAsync(
            Guid t, IReadOnlyCollection<Guid> ids, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                Devices.Where(d => ids.Contains(d.Id) && d.ReadyForHandoverAtUtc == null)
                    .OrderBy(d => d.PublicCode)
                    .Select(d => d.PublicCode)
                    .Take(take)
                    .ToList());

        public Task<int> CountExistingAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult(Devices.Count(d => ids.Contains(d.Id)));

        public Task<IReadOnlyList<Device>> TrackedForReviewAsync(
            Guid t, IReadOnlyCollection<Guid> ids, bool markedOnly,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Device>>(
                Devices.Where(d => ids.Contains(d.Id))
                    .Where(d => markedOnly
                        ? d.ReadyForHandoverAtUtc != null
                        : d.ReadyForHandoverAtUtc == null)
                    .ToList());

        // --- اللي الفحوص دي مابتستعملهوش ---

        public Task<(IReadOnlyList<HandoverCandidateRow> Rows, int TotalItems)> CandidatesAsync(
            Guid t, HandoverCandidateFilter f, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<Guid> Ids, int TotalItems)> CandidateIdsAsync(
            Guid t, HandoverCandidateFilter f, int cap, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<HandoverRecipientItem>> RecipientsAsync(
            Guid t, DateTime? from, DateTime? to, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<HandoverLogItem> Rows, int TotalItems)> LogAsync(
            Guid t, HandoverLogFilter f, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed record Harness(
        FakeHandoverRepository Repo,
        FakeWorkflowRecorder Workflow,
        FakeAuditTrail Audit,
        FakeUnitOfWork Work,
        FakeCurrentUser Me,
        ExecuteHandoverCommandHandler Execute,
        MarkHandoverReadyCommandHandler Ready,
        Location Warehouse);

    private static Harness Build(LocationKind kind = LocationKind.Warehouse)
    {
        var repo = new FakeHandoverRepository();
        var workflow = new FakeWorkflowRecorder();
        var audit = new FakeAuditTrail();
        var work = new FakeUnitOfWork();
        var me = new FakeCurrentUser(UserRole.FloorManager);

        var warehouse = new Location
        {
            TenantId = me.TenantId,
            Name = kind == LocationKind.Sales ? "المبيعات" : "المخزن",
            Code = "W1",
            Kind = kind,
            IsActive = true,
        };
        repo.Destinations.Add(warehouse);

        return new Harness(
            repo, workflow, audit, work, me,
            new ExecuteHandoverCommandHandler(repo, workflow, audit, work, me),
            new MarkHandoverReadyCommandHandler(repo, audit, work, me),
            warehouse);
    }

    private static Device Device(
        Harness h, string code, bool eligible = true, bool reviewed = true)
    {
        var device = new Core.Entities.Device
        {
            TenantId = h.Me.TenantId,
            PublicCode = code,
            LastKnownManufacturer = "HP",
            LastKnownModel = "6470b",
            LastSeenAtUtc = DateTime.UtcNow,
            ReadyForHandoverAtUtc = reviewed ? DateTime.UtcNow.AddHours(-1) : null,
        };

        h.Repo.Devices.Add(device);

        if (eligible) h.Repo.Eligible.Add(device.Id);

        return device;
    }

    private static ExecuteHandoverCommand Send(
        Harness h, IEnumerable<Guid> ids,
        string receiver = "هاني", string? reason = null, string? over = null) =>
        new([.. ids], h.Warehouse.Id, receiver, reason, null, over);

    // =================================================================
    //  التسليم الناجح
    // =================================================================

    [Fact]
    public async Task A_reviewed_eligible_batch_moves_and_saves_once()
    {
        var h = Build();
        var a = Device(h, "DV-01");
        var b = Device(h, "DV-02");

        var result = await h.Execute.Handle(Send(h, [a.Id, b.Id]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Moved);
        Assert.Equal("المخزن", result.Value.DestinationName);
        Assert.Equal("هاني", result.Value.ReceivedByName);

        // 🔴 حفظة واحدة: الحركات كلها وسجل التدقيق مع بعض.
        Assert.Equal(1, h.Work.Saves);

        Assert.Equal(2, h.Workflow.Moves.Count);

        var line = Assert.Single(h.Audit.Lines);
        Assert.Equal("device.handed_over", line.Action);
        Assert.Contains("هاني", line.Summary);
    }

    /// <summary>
    /// 🔴 <b>اللاب مبقاش مع فني — هو في المخزن دلوقتي.</b>
    /// </summary>
    [Fact]
    public async Task Every_move_clears_the_holder_and_sets_the_destination()
    {
        var h = Build();
        var device = Device(h, "DV-10");

        await h.Execute.Handle(Send(h, [device.Id]), default);

        var move = Assert.Single(h.Workflow.Moves);

        Assert.True(move.ClearsHolder);
        Assert.Equal(h.Warehouse.Id, move.ToLocationId);
        Assert.Equal("هاني", move.ReceivedByName);

        /*
          🔴 **والمرحلة مابتتغيّرش للمخزن بقصد.**

          مفيش قيمة مناسبة: «جاهز» بتوصف الصلاحية مش المكان واللاب
          أصلاً جاهز قبل التسليم، و«نقطة بيع» غلط لمخزن. فالمكان هو
          اللي بيجاوب «هو فين».
        */
        Assert.Null(move.ToStage);
        Assert.Equal(DeviceWorkflowEventType.LocationMoved, move.EventType);
    }

    /// <summary>
    /// ⚠️ إلا المبيعات: «مع المبيعات» موجودة وبتوصف الحالة دي
    /// بالظبط، وليها نوع حركة مخصّص.
    /// </summary>
    [Fact]
    public async Task A_sales_destination_gets_its_own_event_type_and_stage()
    {
        var h = Build(LocationKind.Sales);
        var device = Device(h, "DV-20");

        await h.Execute.Handle(Send(h, [device.Id]), default);

        var move = Assert.Single(h.Workflow.Moves);

        Assert.Equal(DeviceWorkflowEventType.DispatchedToSales, move.EventType);
        Assert.Equal(DeviceOperationalStage.WithSales, move.ToStage);
    }

    /// <summary>
    /// ⚠️ وقت واحد للدفعة كلها — ده اللي بيخلّي السجل يعرضها كشحنة
    /// واحدة.
    /// </summary>
    [Fact]
    public async Task The_whole_batch_shares_one_timestamp()
    {
        var h = Build();
        var ids = Enumerable.Range(0, 4).Select(i => Device(h, $"DV-T{i}").Id).ToList();

        var result = await h.Execute.Handle(Send(h, ids), default);

        var stamps = h.Workflow.Moves.Select(m => m.OccurredAtUtc).Distinct().ToList();

        Assert.Single(stamps);
        Assert.Equal(result.Value.AtUtc, stamps[0]);
    }

    /// <summary>⚠️ والسبب الفاضي بياخد نص افتراضي باسم الجهة.</summary>
    [Fact]
    public async Task An_empty_reason_becomes_a_sentence_naming_the_destination()
    {
        var h = Build();
        var device = Device(h, "DV-30");

        await h.Execute.Handle(Send(h, [device.Id]), default);

        Assert.Equal("تسليم لـالمخزن", Assert.Single(h.Workflow.Moves).Reason);
    }

    // =================================================================
    //  الحاجز
    // =================================================================

    [Fact]
    public async Task An_empty_selection_is_refused()
    {
        var h = Build();

        var result = await h.Execute.Handle(Send(h, []), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.nothing_selected", result.Error.Code);
        Assert.Equal(0, h.Work.Saves);
    }

    /// <summary>
    /// ⚠️ ونفس السقف للتسليم وللمراجعة — لو اتنين اختلفوا، المدير
    /// بيختار دفعة النقطة التانية بترفضها كلها.
    /// </summary>
    [Fact]
    public async Task A_batch_over_the_limit_is_refused_by_both_endpoints()
    {
        var h = Build();

        var tooMany = Enumerable.Range(0, HandoverPolicy.BatchLimit + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var onHandover = await h.Execute.Handle(Send(h, tooMany), default);
        var onReview = await h.Ready.Handle(new MarkHandoverReadyCommand(tooMany, true), default);

        Assert.Equal("handover.batch_too_big", onHandover.Error.Code);
        Assert.Equal("handover.review_batch_too_big", onReview.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ه")]
    public async Task The_receiver_name_is_mandatory(string? receiver)
    {
        var h = Build();
        var device = Device(h, "DV-40");

        var result = await h.Execute.Handle(Send(h, [device.Id], receiver!), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.receiver_required", result.Error.Code);
    }

    [Fact]
    public async Task An_unknown_or_suspended_destination_is_refused()
    {
        var h = Build();
        var device = Device(h, "DV-50");

        var missing = await h.Execute.Handle(
            new ExecuteHandoverCommand([device.Id], Guid.NewGuid(), "هاني", null, null, null),
            default);

        h.Warehouse.IsActive = false;

        var suspended = await h.Execute.Handle(Send(h, [device.Id]), default);

        Assert.Equal("handover.destination_not_found", missing.Error.Code);
        Assert.Equal("handover.destination_suspended", suspended.Error.Code);

        // ⚠️ والرسالة بتقول اسم الجهة عشان المدير يعرف إنه اختار
        // الصح والمشكلة في حالتها.
        Assert.Contains("المخزن", suspended.Error.Description);
    }

    /// <summary>
    /// 🔴 <b>الرسالة بتقول أنهي لاب بالكود.</b>
    ///
    /// <para>«فيه لاب مش مؤهّل» بتخلّي اللي بيسلّم يلغي الدفعة كلها
    /// وهو مش عارف ليه.</para>
    /// </summary>
    [Fact]
    public async Task An_ineligible_laptop_is_named_in_the_refusal()
    {
        var h = Build();
        var good = Device(h, "DV-GOOD");
        var bad = Device(h, "DV-BLOCKED", eligible: false);

        var result = await h.Execute.Handle(Send(h, [good.Id, bad.Id]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.not_eligible", result.Error.Code);
        Assert.Contains("DV-BLOCKED", result.Error.Description);

        // ⚠️ واللاب السليم اسمه مش في الرسالة.
        Assert.DoesNotContain("DV-GOOD", result.Error.Description);

        Assert.Empty(h.Workflow.Moves);
        Assert.Equal(0, h.Work.Saves);
    }

    [Fact]
    public async Task A_device_id_that_does_not_exist_gets_its_own_message()
    {
        var h = Build();
        var device = Device(h, "DV-60");

        var ghost = Guid.NewGuid();
        h.Repo.Eligible.Add(ghost);

        var result = await h.Execute.Handle(Send(h, [device.Id, ghost]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.device_missing", result.Error.Code);
    }

    // =================================================================
    //  المخرج المسجّل
    // =================================================================

    /// <summary>
    /// 🔴 <b>المراجعة شرط — والرسالة بتقول أنهي لاب وبتعرض
    /// المخرج.</b>
    /// </summary>
    [Fact]
    public async Task An_unreviewed_laptop_needs_a_written_reason()
    {
        var h = Build();
        var device = Device(h, "DV-FRESH", reviewed: false);

        var result = await h.Execute.Handle(Send(h, [device.Id]), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.not_reviewed", result.Error.Code);
        Assert.Contains("DV-FRESH", result.Error.Description);
        Assert.Empty(h.Workflow.Moves);
    }

    /// <summary>
    /// 🔴 <b>و«ok» مش سبب.</b>
    ///
    /// <para>الباب مفتوح بشرط إنه يتكتب؛ سبب من حرفين بيخلّي الشرط
    /// ختم مالوش معنى.</para>
    /// </summary>
    /// <summary>
    /// 🔴 <b>الأرقام في الرسايل عربية-هندية — والفحص ده اتكتب بعد
    /// ما فحص HTTP حقيقي لقط الفرق.</b>
    ///
    /// <para>رسايل القديم مكتوبة «٥٠٠ لاب» و«١٠ حروف». والنسخة
    /// الأولى هنا كانت بتحقن الثابت فيطلع «500» و«10» — نفس الطلب
    /// يدّي رسالة مختلفة من الشاشتين وهما على نفس القاعدة.</para>
    ///
    /// <para>⚠️ والرقم لسه جاي من الثابت، مش مكتوب في النص: لو
    /// اتكتب بإيدنا، أول تعديل في السقف بيخلّي الرسالة تكذب على
    /// المستخدم.</para>
    /// </summary>
    [Fact]
    public void The_numbers_inside_the_messages_use_arabic_indic_digits()
    {
        Assert.Equal("٥٠٠", Core.Text.ArabicDigits.Of(HandoverPolicy.BatchLimit));
        Assert.Equal("١٠", Core.Text.ArabicDigits.Of(HandoverPolicy.MinOverrideReason));

        Assert.Contains("٥٠٠ لاب", HandoverErrors.BatchTooBigForHandover.Description);
        Assert.Contains("٥٠٠ لاب", HandoverErrors.BatchTooBigForReview.Description);
        Assert.Contains("١٠ حروف", HandoverErrors.NotReviewed(["DV-1"]).Description);

        /*
          ⚠️ **ومفيش ولا رقم لاتيني في الرسايل اللي مالهاش أكواد.**

          رسالة الرفض بتحقن <b>أكواد اللابات</b> جوّاها، والأكواد
          لاتينية بطبعها (<c>DV-0003</c>) — فالفحص ده على الرسايل
          الثابتة بس. (النسخة الأولى منه حسبت رقم الكود كغلط.)
        */
        foreach (string message in new[]
                 {
                     HandoverErrors.BatchTooBigForHandover.Description,
                     HandoverErrors.BatchTooBigForReview.Description,
                     HandoverErrors.ReceiverRequired.Description,
                     HandoverErrors.NothingSelected.Description,
                 })
        {
            Assert.DoesNotContain(message, c => c is >= '0' and <= '9');
        }
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("مستعجل")]
    [InlineData("         ")]
    public async Task A_short_override_reason_is_not_a_reason(string reason)
    {
        var h = Build();
        var device = Device(h, "DV-SHORT", reviewed: false);

        var result = await h.Execute.Handle(Send(h, [device.Id], over: reason), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.not_reviewed", result.Error.Code);
    }

    /// <summary>
    /// 🔴 <b>والاستثناء بيتكتب في <u>سبب الحركة</u> نفسه، مش في
    /// الملاحظات.</b>
    ///
    /// <para>الملاحظات اختيارية وبتتفلتر في العرض؛ والسبب بيبان في
    /// سجل التسليم جمب كل صف — فاللي بيراجع السجل بعد شهر يشوف إن
    /// ده تسليم استثنائي من غير ما يفتح حاجة.</para>
    /// </summary>
    [Fact]
    public async Task A_written_override_lands_in_the_move_reason_and_the_audit_line()
    {
        var h = Build();
        var device = Device(h, "DV-URGENT", reviewed: false);

        var result = await h.Execute.Handle(
            Send(h, [device.Id], reason: "تسليم عادي",
                over: "العميل مستعجل والمراجعة هتتأخر يومين"),
            default);

        Assert.True(result.IsSuccess);

        string moveReason = Assert.Single(h.Workflow.Moves).Reason;

        Assert.Contains("من غير مراجعة", moveReason);
        Assert.Contains("العميل مستعجل", moveReason);

        // ⚠️ والسبب الأصلي لسه موجود بعد الاستثناء.
        Assert.Contains("تسليم عادي", moveReason);

        string audit = Assert.Single(h.Audit.Lines).Summary;

        Assert.Contains("من غير مراجعة", audit);
        Assert.Contains("العميل مستعجل", audit);
    }

    /// <summary>
    /// ⚠️ والتسليم المراجَع مابياخدش علامة استثناء — غير كده السجل
    /// كله بيبان استثنائي والتمييز بيتوه.
    /// </summary>
    [Fact]
    public async Task A_fully_reviewed_handover_carries_no_override_marker()
    {
        var h = Build();
        var device = Device(h, "DV-CLEAN");

        await h.Execute.Handle(
            Send(h, [device.Id], reason: "تسليم عادي", over: "سبب طويل مش محتاجينه"),
            default);

        Assert.DoesNotContain("من غير مراجعة", Assert.Single(h.Workflow.Moves).Reason);
        Assert.DoesNotContain("من غير مراجعة", Assert.Single(h.Audit.Lines).Summary);
    }

    // =================================================================
    //  الكل أو ولا واحد
    // =================================================================

    /// <summary>
    /// 🔴 <b>لو ٣٩ من ٤٠ نجحوا، الشحنة ناقصة والسجل بيقول إنها
    /// تمّت.</b>
    ///
    /// <para>فأول فشل بيوقّف الدفعة كلها، ومفيش حفظ بيحصل — واللي
    /// نجح قبلها لسه في الذاكرة.</para>
    /// </summary>
    [Fact]
    public async Task One_refused_move_cancels_the_whole_shipment()
    {
        var h = Build();
        var ids = Enumerable.Range(0, 4).Select(i => Device(h, $"DV-B{i}").Id).ToList();

        // الحركة التالتة بترفض.
        h.Workflow.RefuseAt = 2;

        var result = await h.Execute.Handle(Send(h, ids), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.move_refused", result.Error.Code);

        // 🔴 ومفيش حفظ — فاللي اتكتب في الذاكرة بيروح.
        Assert.Equal(0, h.Work.Saves);

        // ⚠️ ومفيش سطر سجل بيقول إن الشحنة تمّت.
        Assert.Empty(h.Audit.Lines);
    }

    // =================================================================
    //  المراجعة
    // =================================================================

    [Fact]
    public async Task Marking_ready_stamps_who_said_so_and_when()
    {
        var h = Build();
        var device = Device(h, "DV-R1", reviewed: false);

        var result = await h.Ready.Handle(
            new MarkHandoverReadyCommand([device.Id], true), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Changed);
        Assert.True(result.Value.Ready);

        Assert.NotNull(device.ReadyForHandoverAtUtc);
        Assert.Equal(h.Me.Id, device.ReadyByUserId);
        Assert.Equal("كريم", device.ReadyByName);

        Assert.Equal(1, h.Work.Saves);
        Assert.Equal("device.marked_ready", Assert.Single(h.Audit.Lines).Action);
    }

    /// <summary>
    /// 🔴 <b>شيل العلامة مالوش شروط.</b>
    ///
    /// <para>الرجوع عن غلطة لازم يفضل ممكن دايماً: لاب اتعلّم جاهز
    /// وبعدين اتفتحله أمر صيانة مبقاش «مؤهّل» — ولو منعنا شيل
    /// العلامة عنه، بيفضل معلّم غلط لحد ما الصيانة تخلص.</para>
    /// </summary>
    [Fact]
    public async Task Clearing_the_mark_works_even_on_a_laptop_that_is_no_longer_eligible()
    {
        var h = Build();
        var device = Device(h, "DV-R2", eligible: false, reviewed: true);

        var result = await h.Ready.Handle(
            new MarkHandoverReadyCommand([device.Id], false), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Changed);
        Assert.False(result.Value.Ready);

        Assert.Null(device.ReadyForHandoverAtUtc);
        Assert.Null(device.ReadyByUserId);
        Assert.Null(device.ReadyByName);
    }

    /// <summary>⚠️ وحطّ العلامة ليه شروط — ومعاها أكواد.</summary>
    [Fact]
    public async Task Marking_an_ineligible_laptop_ready_is_refused_by_code()
    {
        var h = Build();
        var device = Device(h, "DV-R3", eligible: false, reviewed: false);

        var result = await h.Ready.Handle(
            new MarkHandoverReadyCommand([device.Id], true), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.not_eligible_for_review", result.Error.Code);
        Assert.Contains("DV-R3", result.Error.Description);
        Assert.Null(device.ReadyForHandoverAtUtc);
    }

    /// <summary>
    /// ⚠️ ومفيش حفظ ولا سطر سجل لما مفيش حاجة اتغيّرت فعلاً —
    /// الرقم بيوصف الكتابة مش الطلب.
    /// </summary>
    [Fact]
    public async Task A_no_op_review_writes_nothing()
    {
        var h = Build();
        var device = Device(h, "DV-R4", reviewed: true);

        var result = await h.Ready.Handle(
            new MarkHandoverReadyCommand([device.Id], true), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Changed);
        Assert.Equal(0, h.Work.Saves);
        Assert.Empty(h.Audit.Lines);
    }

    /// <summary>⚠️ والمعرّف الفاضي والمكرّر بيتشالوا بدل ما يرجّعوا غلط.</summary>
    [Fact]
    public async Task Empty_and_duplicate_ids_are_dropped()
    {
        var h = Build();
        var device = Device(h, "DV-R5", reviewed: false);

        var result = await h.Ready.Handle(
            new MarkHandoverReadyCommand(
                [Guid.Empty, device.Id, device.Id, Guid.Empty], true),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Changed);
    }

    [Fact]
    public async Task A_selection_of_only_empty_ids_is_refused()
    {
        var h = Build();

        var result = await h.Ready.Handle(
            new MarkHandoverReadyCommand([Guid.Empty], true), default);

        Assert.True(result.IsFailure);
        Assert.Equal("handover.nothing_selected", result.Error.Code);
    }
}
