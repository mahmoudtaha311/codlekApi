using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.SyncOperational;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Repairs;
using Codlek.Core.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// الشغل التشغيلي الجايّ من الراكة — أوامر الصيانة وحركات السجل.
///
/// <para>🔴 <b>كل عملية هنا لازم تبقى ساكنة.</b> الراكة بتعيد الرفع لما
/// الرد يتأخر، فالأمر بيتطابق بمعرّفه والحركة بمعرّف حدثها — والإعادة
/// بترجّع نفس الصف مش صف تاني. من غير كده، «سلّمت الجهاز» واحدة كانت
/// بتتسجّل مرتين.</para>
/// </summary>
public class OperationalSyncTests
{
    private sealed class FakeOperational : IOperationalSyncRepository
    {
        public readonly List<RepairWorkItem> Items = [];
        public readonly List<Technician> Technicians = [];
        public readonly HashSet<Guid> Reports = [];
        public readonly List<(Guid Rack, Guid Tech, DateTime At)> Logins = [];

        public readonly List<RepairWorkItemIssue> AddedIssues = [];
        public readonly List<RepairPart> AddedParts = [];

        public Task<DateTime?> LastLoginAtAsync(
            Guid tenantId, Guid rackId, Guid technicianId, DateTime atOrBeforeUtc,
            CancellationToken ct = default) =>
            Task.FromResult(Logins
                .Where(l => l.Rack == rackId && l.Tech == technicianId && l.At <= atOrBeforeUtc)
                .Select(l => (DateTime?)l.At)
                .OrderByDescending(a => a)
                .FirstOrDefault());

        public Task<Technician?> FindTechnicianAsync(
            Guid tenantId, Guid technicianId, CancellationToken ct = default) =>
            Task.FromResult(Technicians.FirstOrDefault(
                t => t.Id == technicianId && t.TenantId == tenantId));

        public Task<bool> ReportExistsAsync(
            Guid tenantId, Guid reportId, CancellationToken ct = default) =>
            Task.FromResult(Reports.Contains(reportId));

        public Task<RepairWorkItem?> FindWorkItemAsync(
            Guid tenantId, Guid workItemId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == workItemId));

        public void AddWorkItem(RepairWorkItem item) => Items.Add(item);

        public void AddIssue(RepairWorkItemIssue issue) => AddedIssues.Add(issue);

        public void AddPart(RepairPart part) => AddedParts.Add(part);
    }

    private sealed class FakeReference : IDeviceReference
    {
        public readonly Dictionary<Guid, Guid> Map = [];

        public Task<Guid?> ResolveAsync(
            Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
            Task.FromResult(Map.TryGetValue(deviceId, out var c) ? c : (Guid?)null);

        public Task<IReadOnlyDictionary<Guid, Guid>> ResolveManyAsync(
            Guid tenantId, IReadOnlyCollection<Guid> deviceIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(
                deviceIds.Where(Map.ContainsKey).ToDictionary(id => id, id => Map[id]));
    }

    private sealed class FakeCounters : ITenantCounters
    {
        public int Next = 41;
        public int Calls;

        public Task<int> NextAsync(Guid t, string name, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Next++);
        }

        public Task<int> ReserveAsync(Guid t, string name, int count, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed record Harness(
        FakeOperational Repo,
        FakeReference Devices,
        FakeWorkflowRecorder Workflow,
        FakeCounters Counters,
        OperationalSyncApplier Applier,
        OperationalSource Source,
        Guid DeviceId);

    private static Harness Build()
    {
        var repo = new FakeOperational();
        var devices = new FakeReference();
        var workflow = new FakeWorkflowRecorder();
        var counters = new FakeCounters();

        var source = new OperationalSource(Guid.NewGuid(), Guid.NewGuid(), 7);
        var deviceId = Guid.NewGuid();

        devices.Map[deviceId] = deviceId;

        return new Harness(
            repo, devices, workflow, counters,
            new OperationalSyncApplier(
                repo, devices, workflow, counters,
                NullLogger<OperationalSyncApplier>.Instance),
            source, deviceId);
    }

    private static Technician Tech(Harness h, bool canRepair = true, bool loggedIn = true)
    {
        var tech = new Technician
        {
            TenantId = h.Source.TenantId,
            Code = "100200",
            DisplayName = "سامي",
            CanRepair = canRepair,
            IsActive = true,
        };

        h.Repo.Technicians.Add(tech);

        if (loggedIn)
            h.Repo.Logins.Add((h.Source.RackId, tech.Id, DateTime.UtcNow.AddDays(-1)));

        return tech;
    }

    private static RepairWorkItemSyncPayload Item(
        Harness h, Guid? id = null, RepairStatus status = RepairStatus.WaitingForRepair,
        Guid? assigned = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            DeviceId = h.DeviceId,
            Status = (int)status,
            AssignedTechnicianId = assigned,
            OpenedByName = "كريم",
            OpenedAtUtc = DateTime.UtcNow.AddHours(-3),
            ClaimedAtUtc = DateTime.UtcNow.AddHours(-2),
            FaultSummary = "الشاشة بتطفى",
        };

    // =================================================================
    //  أوامر الصيانة — المدخلات
    // =================================================================

    [Fact]
    public async Task A_work_item_with_no_id_or_device_is_refused_for_good()
    {
        var h = Build();

        var noId = Item(h);
        noId.Id = Guid.Empty;

        var noDevice = Item(h);
        noDevice.DeviceId = Guid.Empty;

        foreach (var dto in new[] { noId, noDevice })
        {
            var outcome = await h.Applier.ApplyWorkItemAsync(h.Source, dto);

            Assert.Equal(OperationalSyncApplier.MissingFields, outcome.Code);
            Assert.False(outcome.Retryable);
        }

        Assert.Empty(h.Repo.Items);
    }

    /// <summary>
    /// 🔴 <b>الجهاز اللي لسه ماوصلش = رفض <u>مؤقت</u>.</b> الأمر سليم
    /// تماماً، جهازه بس لسه ماوصلش. رفض نهائي هنا معناه شغل صيانة
    /// بيضيع لأن ترتيب دفعة اتكسر.
    /// </summary>
    [Fact]
    public async Task A_device_not_yet_synced_is_a_retryable_refusal()
    {
        var h = Build();

        var dto = Item(h);
        dto.DeviceId = Guid.NewGuid();

        var outcome = await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(OperationalSyncApplier.DeviceNotSynced, outcome.Code);
        Assert.True(outcome.Retryable);
        Assert.Empty(h.Repo.Items);
    }

    /// <summary>
    /// 🔴 <b>والأمر بيتخزّن بالمعرّف <u>الكانوني</u></b> — المفتاح
    /// الأجنبي بيشاور على جدول الأجهزة، والمعرّف المحلي مالوش صف هناك.
    /// </summary>
    [Fact]
    public async Task The_work_item_is_stored_against_the_canonical_device()
    {
        var h = Build();

        var local = Guid.NewGuid();
        var canonical = Guid.NewGuid();

        h.Devices.Map[local] = canonical;

        var dto = Item(h);
        dto.DeviceId = local;

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(canonical, Assert.Single(h.Repo.Items).DeviceId);
    }

    [Fact]
    public async Task A_source_report_not_yet_synced_is_retryable()
    {
        var h = Build();

        var dto = Item(h);
        dto.SourceReportId = Guid.NewGuid();

        var outcome = await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(OperationalSyncApplier.ReportNotSynced, outcome.Code);
        Assert.True(outcome.Retryable);
    }

    [Fact]
    public async Task An_empty_source_report_is_not_a_reference()
    {
        var h = Build();

        var dto = Item(h);
        dto.SourceReportId = Guid.Empty;

        var outcome = await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.False(outcome.Rejected);
        Assert.Null(Assert.Single(h.Repo.Items).SourceReportId);
    }

    // =================================================================
    //  أوامر الصيانة — الفني المسند
    // =================================================================

    [Fact]
    public async Task A_technician_outside_the_workshop_is_not_found()
    {
        var h = Build();

        var outcome = await h.Applier.ApplyWorkItemAsync(
            h.Source, Item(h, assigned: Guid.NewGuid()));

        Assert.Equal(OperationalSyncApplier.TechnicianNotFound, outcome.Code);
        Assert.False(outcome.Retryable);
    }

    /// <summary>
    /// 🔴 <b>الفني اللي استلم الأمر لازم يكون له صلاحية صيانة <u>وقت
    /// الاستلام</u>.</b>
    /// </summary>
    [Fact]
    public async Task A_technician_without_a_recorded_login_cannot_claim()
    {
        var h = Build();
        var tech = Tech(h, loggedIn: false);

        var outcome = await h.Applier.ApplyWorkItemAsync(
            h.Source, Item(h, assigned: tech.Id));

        Assert.Equal(OperationalSyncApplier.CapabilityDenied, outcome.Code);
        Assert.Equal(OfflineAuthorisation.NoLoginMessage, outcome.Message);
        Assert.Empty(h.Repo.Items);
    }

    [Fact]
    public async Task A_logged_in_repairer_can_claim()
    {
        var h = Build();
        var tech = Tech(h);

        var outcome = await h.Applier.ApplyWorkItemAsync(
            h.Source, Item(h, assigned: tech.Id));

        Assert.False(outcome.Rejected);
        Assert.Equal(tech.Id, Assert.Single(h.Repo.Items).AssignedTechnicianId);
    }

    /// <summary>
    /// ⚠️ <b>ووقت الاستلام هو اللي بيتحسب — مش وقت الفتح.</b> الصلاحية
    /// اتسحبت بين الفتح والاستلام = رفض.
    /// </summary>
    [Fact]
    public async Task The_claim_time_decides_not_the_open_time()
    {
        var h = Build();
        var tech = Tech(h, canRepair: false);

        var dto = Item(h, assigned: tech.Id);

        // سحب الصلاحية حصل **بين** الفتح والاستلام.
        tech.CapabilityChangedAtUtc = dto.OpenedAtUtc.AddMinutes(30);

        var outcome = await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(OperationalSyncApplier.CapabilityDenied, outcome.Code);

        // وحراسة: لو الاستلام كان قبل السحب — يعدّي.
        var h2 = Build();
        var tech2 = Tech(h2, canRepair: false);
        var dto2 = Item(h2, assigned: tech2.Id);

        tech2.CapabilityChangedAtUtc = dto2.ClaimedAtUtc!.Value.AddMinutes(10);

        Assert.False((await h2.Applier.ApplyWorkItemAsync(h2.Source, dto2)).Rejected);
    }

    // =================================================================
    //  أوامر الصيانة — الكتابة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الموافقة بتتحط عند الإنشاء بس.</b> الرفعات اللي بعد كده
    /// مابتلمسهاش — وإلا راكة كانت هتدهس قرار المحاسب.
    /// </summary>
    [Fact]
    public async Task Approval_is_set_once_and_never_overwritten_by_a_resync()
    {
        var h = Build();
        var dto = Item(h);

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        var row = Assert.Single(h.Repo.Items);

        Assert.Equal(RepairPolicy.NewOrderApproval, row.Approval);

        // المحاسب وافق.
        row.Approval = RepairApproval.Approved;

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(RepairApproval.Approved, row.Approval);
    }

    /// <summary>
    /// 🔴 <b>الأمر المقفول مابيترجعش لورا — و«زي ما هو» مش رفض.</b>
    /// راكة قديمة بترفع لقطة أقدم فوق أحدث؛ الحارس بيمنع «تمت الصيانة»
    /// ترجع «قيد الصيانة»، والراكة بتقفل الصف بدل ما تفضل تحاول.
    /// </summary>
    [Theory]
    [InlineData(RepairStatus.Completed)]
    [InlineData(RepairStatus.UnableToRepair)]
    [InlineData(RepairStatus.Cancelled)]
    public async Task A_closed_order_never_moves_backward(RepairStatus closed)
    {
        var h = Build();
        var id = Guid.NewGuid();

        await h.Applier.ApplyWorkItemAsync(h.Source, Item(h, id: id, status: closed));

        var outcome = await h.Applier.ApplyWorkItemAsync(
            h.Source, Item(h, id: id, status: RepairStatus.InProgress));

        Assert.True(outcome.Unchanged);
        Assert.False(outcome.Rejected);
        Assert.Equal(closed, Assert.Single(h.Repo.Items).Status);
    }

    /// <summary>
    /// ⚠️ <b>والحارس على الأمر <u>الموجود</u> بس.</b> أمر جديد بيوصل
    /// مقفول من الأول (فني فتحه وخلّصه أوفلاين) — ده بيتقبل عادي.
    /// </summary>
    [Fact]
    public async Task A_new_order_may_arrive_already_closed()
    {
        var h = Build();

        var outcome = await h.Applier.ApplyWorkItemAsync(
            h.Source, Item(h, status: RepairStatus.Completed));

        Assert.True(outcome.Applied);
        Assert.Equal(RepairStatus.Completed, Assert.Single(h.Repo.Items).Status);
    }

    /// <summary>
    /// 🔴 <b>راكة قديمة شغّالة على أمر مستني موافقة — بنقبل ونعلّم،
    /// والموافقة مابتتغيّرش.</b>
    /// </summary>
    [Fact]
    public async Task Bench_work_on_an_unapproved_order_is_accepted_and_flagged()
    {
        var h = Build();

        var outcome = await h.Applier.ApplyWorkItemAsync(
            h.Source, Item(h, status: RepairStatus.InProgress));

        var row = Assert.Single(h.Repo.Items);

        Assert.True(outcome.Applied);
        Assert.True(row.StartedWithoutApproval);
        Assert.Equal(RepairApproval.Pending, row.Approval);
    }

    [Fact]
    public async Task Waiting_is_not_bench_work_and_is_not_flagged()
    {
        var h = Build();

        await h.Applier.ApplyWorkItemAsync(h.Source, Item(h, status: RepairStatus.WaitingForRepair));

        Assert.False(Assert.Single(h.Repo.Items).StartedWithoutApproval);
    }

    /// <summary>
    /// 🔴 <b>رقم الأمر من السيرفر — مرة واحدة.</b> وإعادة الإرسال بتحافظ
    /// على نفس الرقم.
    /// </summary>
    [Fact]
    public async Task The_public_code_is_issued_once_by_the_server()
    {
        var h = Build();
        var dto = Item(h);

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        var row = Assert.Single(h.Repo.Items);
        string code = row.PublicCode;

        Assert.Equal(RepairCode.Format(41), code);

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(code, row.PublicCode);
        Assert.Equal(1, h.Counters.Calls);
    }

    /// <summary>
    /// ⚠️ <b>وصف قديم اتكتب برقم فاضي بيتصلّح لوحده لو اترفع تاني.</b>
    /// والشرط «الرقم فاضي» مش «الصف جديد» — عشان كده.
    /// </summary>
    [Fact]
    public async Task An_old_row_with_an_empty_code_heals_on_resync()
    {
        var h = Build();
        var id = Guid.NewGuid();

        h.Repo.Items.Add(new RepairWorkItem
        {
            Id = id,
            TenantId = h.Source.TenantId,
            DeviceId = h.DeviceId,
            PublicCode = "",
            Status = RepairStatus.WaitingForRepair,
        });

        await h.Applier.ApplyWorkItemAsync(h.Source, Item(h, id: id));

        Assert.Equal(RepairCode.Format(41), h.Repo.Items[0].PublicCode);
    }

    [Fact]
    public async Task The_search_text_carries_the_code_and_the_fault()
    {
        var h = Build();

        await h.Applier.ApplyWorkItemAsync(h.Source, Item(h));

        var row = Assert.Single(h.Repo.Items);

        Assert.Contains(RepairCode.Format(41), row.SearchText);

        // ⚠️ العمود مطبَّع — «ة» بقت «ه» — فالمقارنة بالمطبَّع.
        Assert.Contains(Codlek.Core.Text.ArabicText.Normalize("الشاشة"), row.SearchText);
    }

    /// <summary>⚠️ الفاعل: فني لو فيه معرّف فاتح، وإلا الراكة.</summary>
    [Fact]
    public async Task The_opener_type_follows_the_technician_id()
    {
        var h = Build();

        var byRack = Item(h);
        await h.Applier.ApplyWorkItemAsync(h.Source, byRack);

        var byTech = Item(h);
        byTech.OpenedByTechnicianId = Guid.NewGuid();
        await h.Applier.ApplyWorkItemAsync(h.Source, byTech);

        Assert.Equal("Rack", h.Repo.Items.Single(i => i.Id == byRack.Id).OpenedByActorType);
        Assert.Equal("Technician", h.Repo.Items.Single(i => i.Id == byTech.Id).OpenedByActorType);
    }

    // =================================================================
    //  الأعطال والقطع — مطابقة بالمعرّف
    // =================================================================

    /// <summary>
    /// 🔴 <b>الأعطال والقطع بتتطابق بالمعرّف — ومفيش مسح وإعادة.</b>
    /// والجديد بيتضاف <b>بإضافة صريحة</b> — وإلا EF بيعلّمه
    /// <c>Modified</c> وبيطلع UPDATE على صف مالوش وجود.
    /// </summary>
    [Fact]
    public async Task Issues_and_parts_merge_by_id_and_new_ones_are_added_explicitly()
    {
        var h = Build();
        var dto = Item(h);

        var issueId = Guid.NewGuid();
        var partId = Guid.NewGuid();

        dto.Issues = [new RepairIssueSyncPayload { Id = issueId, IssueCode = "SCR-01", IssueTitleSnapshot = "الشاشة" }];
        dto.Parts = [new RepairPartSyncPayload { Id = partId, Name = "شاشة", Quantity = 1 }];

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        // 🔴 الإضافة الصريحة حصلت.
        Assert.Equal(issueId, Assert.Single(h.Repo.AddedIssues).Id);
        Assert.Equal(partId, Assert.Single(h.Repo.AddedParts).Id);

        // إعادة رفع بتعديل — نفس المعرّف.
        dto.Issues[0].Resolved = true;
        dto.Parts[0].Quantity = 2;

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        var row = Assert.Single(h.Repo.Items);

        Assert.Single(row.Issues);
        Assert.True(row.Issues.First().Resolved);
        Assert.Equal(2, row.Parts.First().Quantity);

        // ⚠️ ومفيش إضافة تانية — نفس الصف اتعدّل.
        Assert.Single(h.Repo.AddedIssues);
        Assert.Single(h.Repo.AddedParts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task A_non_positive_quantity_is_one(int quantity)
    {
        var h = Build();
        var dto = Item(h);

        dto.Parts = [new RepairPartSyncPayload { Id = Guid.NewGuid(), Name = "مروحة", Quantity = quantity }];

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        Assert.Equal(1, Assert.Single(h.Repo.Items).Parts.First().Quantity);
    }

    [Fact]
    public async Task An_issue_or_part_without_an_id_is_skipped()
    {
        var h = Build();
        var dto = Item(h);

        dto.Issues = [new RepairIssueSyncPayload { Id = Guid.Empty, IssueCode = "X" }];
        dto.Parts = [new RepairPartSyncPayload { Id = Guid.Empty, Name = "X" }];

        await h.Applier.ApplyWorkItemAsync(h.Source, dto);

        var row = Assert.Single(h.Repo.Items);

        Assert.Empty(row.Issues);
        Assert.Empty(row.Parts);
    }

    // =================================================================
    //  حركات السجل
    // =================================================================

    private static DeviceWorkflowEventSyncPayload Move(
        Harness h, DeviceWorkflowEventType type = DeviceWorkflowEventType.CustodyHandoff,
        Guid? actor = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            DeviceId = h.DeviceId,
            EventType = (int)type,
            ActorTechnicianId = actor,
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            Reason = "تسليم",
        };

    [Fact]
    public async Task A_move_with_no_event_id_or_device_is_refused_for_good()
    {
        var h = Build();

        var noEvent = Move(h);
        noEvent.EventId = Guid.Empty;

        var noDevice = Move(h);
        noDevice.DeviceId = Guid.Empty;

        foreach (var dto in new[] { noEvent, noDevice })
        {
            var outcome = await h.Applier.ApplyWorkflowEventAsync(h.Source, dto);

            Assert.Equal(OperationalSyncApplier.MissingFields, outcome.Code);
            Assert.False(outcome.Retryable);
        }

        Assert.Empty(h.Workflow.Moves);
    }

    /// <summary>
    /// ⚠️ <b>من غير فني = الفاعل «محطة فحص».</b> و<c>EventId</c> بيوصل
    /// المسجّل — هو مفتاح عدم التكرار.
    /// </summary>
    [Fact]
    public async Task A_move_without_a_technician_is_recorded_as_the_rack()
    {
        var h = Build();
        var dto = Move(h);

        var outcome = await h.Applier.ApplyWorkflowEventAsync(h.Source, dto);

        Assert.True(outcome.Applied);

        var move = Assert.Single(h.Workflow.Moves);

        Assert.Equal(OperationalSyncApplier.RackActorName, move.Actor.Name);
        Assert.Equal("System", move.Actor.Type);
        Assert.Equal(dto.EventId, move.EventId);
    }

    /// <summary>
    /// 🔴 <b>وحركات الصيانة محتاجة صلاحية صيانة وقت الحركة.</b>
    /// </summary>
    [Theory]
    [InlineData(DeviceWorkflowEventType.RepairStarted)]
    [InlineData(DeviceWorkflowEventType.RepairCompleted)]
    public async Task A_repair_move_needs_repair_capability(DeviceWorkflowEventType type)
    {
        var h = Build();
        var tech = Tech(h, canRepair: false);

        var outcome = await h.Applier.ApplyWorkflowEventAsync(h.Source, Move(h, type, tech.Id));

        Assert.Equal(OperationalSyncApplier.CapabilityDenied, outcome.Code);
        Assert.Empty(h.Workflow.Moves);
    }

    /// <summary>⚠️ والحركات التانية مش محتاجاها — التسليم مش صيانة.</summary>
    [Fact]
    public async Task A_handover_move_needs_no_repair_capability()
    {
        var h = Build();
        var tech = Tech(h, canRepair: false);

        var outcome = await h.Applier.ApplyWorkflowEventAsync(
            h.Source, Move(h, DeviceWorkflowEventType.CustodyHandoff, tech.Id));

        Assert.True(outcome.Applied);
        Assert.Equal("Technician", Assert.Single(h.Workflow.Moves).Actor.Type);
    }

    [Fact]
    public async Task The_actor_name_falls_back_to_the_technicians_display_name()
    {
        var h = Build();
        var tech = Tech(h);

        var dto = Move(h, DeviceWorkflowEventType.CustodyHandoff, tech.Id);
        dto.ActorName = "   ";

        await h.Applier.ApplyWorkflowEventAsync(h.Source, dto);

        Assert.Equal("سامي", Assert.Single(h.Workflow.Moves).Actor.Name);
    }

    /// <summary>
    /// 🔴 <b>«الجهاز مش موجود» من المسجّل = رفض <u>مؤقت</u>.</b> وأي
    /// سبب تاني نهائي.
    /// </summary>
    [Fact]
    public async Task A_missing_device_from_the_recorder_is_retryable()
    {
        var h = Build();

        h.Workflow.Refuse = true;
        h.Workflow.RefuseWith = "الجهاز مش موجود.";

        var outcome = await h.Applier.ApplyWorkflowEventAsync(h.Source, Move(h));

        Assert.Equal(OperationalSyncApplier.DeviceNotSynced, outcome.Code);
        Assert.True(outcome.Retryable);
    }

    [Fact]
    public async Task Any_other_recorder_refusal_is_final()
    {
        var h = Build();

        h.Workflow.Refuse = true;
        h.Workflow.RefuseWith = "الموقع مش موجود.";

        var outcome = await h.Applier.ApplyWorkflowEventAsync(h.Source, Move(h));

        Assert.Equal(OperationalSyncApplier.WorkflowRejected, outcome.Code);
        Assert.False(outcome.Retryable);
    }

    /// <summary>
    /// 🔴 <b>والنص اللي المسجّل الحقيقي بيرجّعه لازم يطابق الثابت.</b>
    ///
    /// <para>التفرقة بين المؤقت والنهائي <b>بالنص</b> — زي القديم
    /// بالحرف. لو حد غيّر صياغة الرسالة في المسجّل، الرفض المؤقت بيبقى
    /// نهائي والشغل بيضيع. الفحص ده بيقرا ملف المسجّل نفسه.</para>
    /// </summary>
    [Fact]
    public void The_recorders_missing_device_text_matches_the_constant()
    {
        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Codlek.Infrastructure", "Data", "DeviceWorkflowRecorder.cs"));

        Assert.Contains(
            "MoveResult.Fail(\"" + OperationalSyncApplier.DeviceMissingMessage, source);
    }

    [Fact]
    public async Task A_missing_occurrence_time_becomes_now()
    {
        var h = Build();

        var dto = Move(h);
        dto.OccurredAtUtc = default;

        await h.Applier.ApplyWorkflowEventAsync(h.Source, dto);

        var at = Assert.Single(h.Workflow.Moves).OccurredAtUtc!.Value;

        Assert.InRange(at, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }
}
