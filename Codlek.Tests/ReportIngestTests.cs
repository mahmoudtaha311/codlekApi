using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Hardware;
using Codlek.Core.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// استقبال الفحوص من المحطة.
///
/// <para>🔴 <b>والمبدأ الأساسي: التكرار مش بيعمل ضرر.</b> الفني ممكن
/// يرفع نفس الملف مرتين، أو النت يقطع في نص الرفع فيعيد. كل فحص ليه
/// <c>Id</c> بيتولّد على الراكة، فالسجل بيتحدّث مش بيتكرر — ولو
/// اتكرر كان عدد الأجهزة هيزيد غلط، وده رقم بيتبني عليه تقييم
/// الفني.</para>
/// </summary>
public class ReportIngestTests
{
    private sealed class FakeIngest : IReportIngestRepository, IDeviceReference
    {
        public readonly Dictionary<Guid, Report> Reports = [];
        public readonly HashSet<Guid> Devices = [];
        public readonly List<Device> DeviceRows = [];
        public readonly List<TechnicianIdentityRow> Technicians = [];
        public readonly Dictionary<Guid, Guid> Aliases = [];

        /// <summary>(شركة، نوع، قيمة) → أجهزة.</summary>
        public readonly Dictionary<(DeviceIdentifierKind, string), List<Guid>> Anchors = [];

        public readonly Dictionary<Guid, List<ModelEvidenceRow>> Evidence = [];
        public readonly List<ReportCursorRow> Cursors = [];
        public readonly List<ReportSnapshotComponent> Components = [];

        public readonly List<Report> Added = [];
        public readonly List<Guid> ChildrenRemoved = [];

        public int AnchorQueries;

        public Task<Dictionary<Guid, Report>> ExistingAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult(Reports
                .Where(p => ids.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value));

        public Task<HashSet<Guid>> KnownDeviceIdsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult(ids.Where(Devices.Contains).ToHashSet());

        public Task<IReadOnlyList<TechnicianIdentityRow>> TechnicianIdentitiesAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TechnicianIdentityRow>>(
                [.. Technicians.Where(r => ids.Contains(r.Id))]);

        public Task<Guid?> CanonicalForAliasAsync(
            Guid t, Guid alias, CancellationToken ct = default) =>
            Task.FromResult(Aliases.TryGetValue(alias, out var c) ? c : (Guid?)null);

        /// <summary>
        /// ⚠️ <b>والمزيّف بيعمل الترجمة كمان</b> — الجهاز الموجود في
        /// <c>Devices</c> حيّ، واللي في <c>Aliases</c> مستعار. سلسلة الدمج
        /// الكاملة متقاسة على قاعدة حقيقية في
        /// <c>ReportIngestRepositoryTests</c>.
        /// </summary>
        public Task<Guid?> ResolveAsync(
            Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
            Task.FromResult(Resolve(deviceId));

        public Task<IReadOnlyDictionary<Guid, Guid>> ResolveManyAsync(
            Guid tenantId, IReadOnlyCollection<Guid> deviceIds,
            CancellationToken ct = default)
        {
            var map = new Dictionary<Guid, Guid>();

            foreach (var id in deviceIds.Where(i => i != Guid.Empty).Distinct())
                if (Resolve(id) is { } found) map[id] = found;

            return Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(map);
        }

        private Guid? Resolve(Guid id) =>
            Devices.Contains(id) ? id
            : Aliases.TryGetValue(id, out var target) && Devices.Contains(target) ? target
            : null;

        public Task<IReadOnlyList<Guid>> DevicesByIdentifierAsync(
            Guid t, DeviceIdentifierKind kind, string value,
            CancellationToken ct = default)
        {
            AnchorQueries++;

            return Task.FromResult<IReadOnlyList<Guid>>(
                Anchors.TryGetValue((kind, value), out var list) ? list : []);
        }

        public void Add(Report report)
        {
            Added.Add(report);
            Reports[report.Id] = report;
        }

        public void RemoveChildren(Report report) => ChildrenRemoved.Add(report.Id);

        public Task<IReadOnlyList<Device>> DevicesAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Device>>(
                [.. DeviceRows.Where(d => ids.Contains(d.Id))]);

        public Task<IReadOnlyList<ModelEvidenceRow>> ModelEvidenceAsync(
            Guid deviceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModelEvidenceRow>>(
                Evidence.TryGetValue(deviceId, out var rows) ? rows : []);

        public Task<IReadOnlyList<ReportCursorRow>> ReportCursorsAsync(
            Guid t, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReportCursorRow>>(
                [.. Cursors
                    .Where(c => deviceIds.Contains(c.DeviceId))
                    .OrderByDescending(c => c.StartedAtUtc)
                    .ThenBy(c => c.ReportId)]);

        public Task<IReadOnlyList<ReportSnapshotComponent>> SnapshotComponentsAsync(
            IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReportSnapshotComponent>>(
                [.. Components.Where(c => reportIds.Contains(c.ReportId))]);
    }

    private sealed record Harness(
        FakeIngest Repo,
        FakeUnitOfWork Work,
        IngestReportsCommandHandler Handler,
        Guid Tenant,
        Guid RackId);

    private static Harness Build()
    {
        var repo = new FakeIngest();
        var work = new FakeUnitOfWork();

        return new Harness(
            repo, work,
            new IngestReportsCommandHandler(
                repo, repo, work,
                NullLogger<IngestReportsCommandHandler>.Instance),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static LaptopReportPayload Payload(
        Guid? id = null,
        DateTime? startedAtUtc = null,
        Guid? deviceId = null,
        string deviceCode = "LP-00000042",
        string technicianId = "",
        string technicianCode = "",
        string? uuid = null,
        string? biosSerial = null,
        string? boardSerial = null,
        string? diskSerial = null,
        string? commercial = null,
        string? commercialSource = null,
        List<StepResultPayload>? steps = null,
        HardwareSnapshotPayload? snapshot = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            StartedAtUtc = startedAtUtc ?? new DateTime(2026, 10, 4, 9, 0, 0),
            DeviceId = deviceId,
            DeviceCode = deviceCode,
            TechnicianId = technicianId,
            TechnicianCode = technicianCode,
            TechnicianName = "أحمد",
            Steps = steps ?? [],
            Snapshot = snapshot,
            Specs = new DeviceSpecsPayload
            {
                Manufacturer = "Dell Inc.",
                Model = "Latitude 5400",
                CommercialModelName = commercial,
                CommercialModelSource = commercialSource,
                SystemUuid = uuid ?? "",
                SerialNumber = biosSerial ?? "",
                BoardSerial = boardSerial ?? "",
                InternalDisks = diskSerial is null
                    ? []
                    : [new DiskInfoPayload
                        {
                            SerialNumber = diskSerial,
                            SizeBytes = 512_000_000_000,
                            MediaType = "SSD",
                        }],
            },
        };

    private static Task<Application.Abstractions.Result<IngestResult>> Run(
        Harness h, params LaptopReportPayload[] payload) =>
        h.Handler.Handle(
            new IngestReportsCommand(h.Tenant, h.RackId, payload), default);

    // =================================================================
    //  التكرار مش بيعمل ضرر
    // =================================================================

    [Fact]
    public async Task An_empty_batch_touches_nothing()
    {
        var h = Build();

        var result = await Run(h);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Total);
        Assert.Equal(0, h.Work.Saves);
    }

    [Fact]
    public async Task A_fresh_report_is_added_once()
    {
        var h = Build();
        var device = Guid.NewGuid();

        h.Repo.Devices.Add(device);

        var result = await Run(h, Payload(deviceId: device));

        Assert.Equal(1, result.Value.Added);
        Assert.Equal(0, result.Value.Updated);

        var row = Assert.Single(h.Repo.Added);

        Assert.Equal(h.Tenant, row.TenantId);
        Assert.Equal(h.RackId, row.SourceRackId);
        Assert.Equal(device, row.DeviceId);
        Assert.False(row.NeedsDeviceResolution);
    }

    /// <summary>
    /// 🔴 <b>نفس الحمولة مرتين = <c>Unchanged</c> — ومفيش أي
    /// كتابة.</b>
    ///
    /// <para>ودي الحالة الشايعة: الراكة بتعيد إرسال نفس الدفعة لأن
    /// الرد ضاع في الشبكة. من غير المقارنة دي، كل إعادة بتمسح
    /// الأولاد وتكتبهم من تاني — رحلات كتابة على حاجة
    /// ماتغيّرتش.</para>
    /// </summary>
    [Fact]
    public async Task The_same_payload_twice_changes_nothing()
    {
        var h = Build();
        var dto = Payload();

        await Run(h, dto);

        h.Repo.ChildrenRemoved.Clear();

        var again = await Run(h, dto);

        Assert.Equal(1, again.Value.Unchanged);
        Assert.Equal(0, again.Value.Added);
        Assert.Equal(0, again.Value.Updated);
        Assert.Empty(h.Repo.ChildrenRemoved);
    }

    [Fact]
    public async Task A_changed_payload_replaces_the_children()
    {
        var h = Build();
        var dto = Payload();

        await Run(h, dto);

        dto.GeneralNote = "ملاحظة جديدة";

        var again = await Run(h, dto);

        Assert.Equal(1, again.Value.Updated);
        Assert.Equal(dto.Id, Assert.Single(h.Repo.ChildrenRemoved));
    }

    /// <summary>
    /// ⚠️ <b>ونفس المعرّف مرتين في <u>نفس</u> الدفعة بيتحدّث مش
    /// بيتكرر.</b>
    /// </summary>
    [Fact]
    public async Task The_same_id_twice_in_one_batch_is_one_row()
    {
        var h = Build();

        var id = Guid.NewGuid();
        var first = Payload(id: id);
        var second = Payload(id: id);

        second.GeneralNote = "تاني";

        var result = await Run(h, first, second);

        Assert.Equal(1, result.Value.Added);
        Assert.Equal(1, result.Value.Updated);
        Assert.Single(h.Repo.Added);
    }

    // =================================================================
    //  الرفض قبل أي استعلام
    // =================================================================

    [Fact]
    public async Task A_report_with_no_id_is_refused()
    {
        var h = Build();

        var result = await Run(h, Payload(id: Guid.Empty));

        Assert.Equal(1, result.Value.Rejected);
        Assert.Empty(h.Repo.Added);
        Assert.Contains("رقم تعريف", Assert.Single(result.Value.Problems));
    }

    [Fact]
    public async Task A_report_with_no_start_date_is_refused()
    {
        var h = Build();

        // ⚠️ `DateTime.MinValue` هو `default(DateTime)` — واللي
        //    الراكة بتبعته لما الحقل مايتكتبش.
        var result = await Run(h, Payload(startedAtUtc: DateTime.MinValue));

        Assert.Equal(1, result.Value.Rejected);
        Assert.Empty(h.Repo.Added);
    }

    /// <summary>
    /// ⚠️ <b>والرفض ده مش في <c>RejectedById</c></b> — مفيش معرّف
    /// يتعلّق عليه، فمسار الدفعات مالوش صف يقفله.
    /// </summary>
    [Fact]
    public async Task A_report_with_no_id_has_nowhere_to_hang_its_rejection()
    {
        var h = Build();

        var result = await Run(h, Payload(id: Guid.Empty));

        Assert.Empty(result.Value.RejectedById);
    }

    // =================================================================
    //  هوية الفني — والشركة هي الحكم
    // =================================================================

    /// <summary>
    /// ⚠️ <b>راكة قديمة بتبعت <c>""</c> أو معرّف حسابها المحلي — ودي
    /// <u>بتتقبل</u>.</b>
    ///
    /// <para>رفضها كان هيوقف مزامنة كل راكة ماترقّتش.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("مش-GUID")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_report_with_no_central_identity_is_stored_with_the_snapshot(
        string technicianId)
    {
        var h = Build();

        var result = await Run(h, Payload(technicianId: technicianId));

        Assert.Equal(1, result.Value.Added);
        Assert.Null(Assert.Single(h.Repo.Added).TechnicianId);
        Assert.Equal("أحمد", h.Repo.Added[0].TechnicianName);
    }

    [Fact]
    public async Task An_identity_the_server_never_heard_of_is_stored_without_it()
    {
        var h = Build();

        var result = await Run(h, Payload(technicianId: Guid.NewGuid().ToString()));

        Assert.Equal(1, result.Value.Added);
        Assert.Null(Assert.Single(h.Repo.Added).TechnicianId);
    }

    /// <summary>
    /// 🔴 <b>فني من شركة تانية = <u>رفض</u>، مش نسبة فاضية.</b>
    ///
    /// <para>تخزينه «من غير فني» معناه إن محاولة النسبة العابرة
    /// للشركات <b>بتنجح</b> كصف مجهول، والصف المجهول ده بيتنسب بإيد
    /// مدير بعدين وهو أصلاً مش بتاعه.</para>
    /// </summary>
    [Fact]
    public async Task A_technician_from_another_workshop_is_a_rejection()
    {
        var h = Build();
        var intruder = Guid.NewGuid();

        h.Repo.Technicians.Add(new TechnicianIdentityRow
        {
            Id = intruder, TenantId = Guid.NewGuid(), Code = "999999",
        });

        var dto = Payload(technicianId: intruder.ToString());

        var result = await Run(h, dto);

        Assert.Equal(1, result.Value.Rejected);
        Assert.Empty(h.Repo.Added);

        var rejection = result.Value.RejectedById[dto.Id];

        Assert.Equal(ReportIngestRules.TechnicianTenantMismatch, rejection.Code);

        // 🔴 ومش قابل للإعادة — نفس الحمولة هتترفض تاني.
        Assert.False(rejection.Retryable);
    }

    /// <summary>
    /// 🔴 <b>والكود اللي مش بتاع الهوية = رفض كمان.</b> الكود ثابت
    /// بعد الإنشاء، فاختلافه معناه إن الحمولة اتلغبطت في الطريق —
    /// وقبولها بيدّي فحص <b>بهويتين</b>.
    /// </summary>
    [Fact]
    public async Task A_code_that_belongs_to_someone_else_is_a_rejection()
    {
        var h = Build();
        var tech = Guid.NewGuid();

        h.Repo.Technicians.Add(new TechnicianIdentityRow
        {
            Id = tech, TenantId = h.Tenant, Code = "100200",
        });

        var dto = Payload(technicianId: tech.ToString(), technicianCode: "777777");

        var result = await Run(h, dto);

        Assert.Equal(1, result.Value.Rejected);
        Assert.Equal(
            ReportIngestRules.TechnicianCodeMismatch,
            result.Value.RejectedById[dto.Id].Code);
    }

    /// <summary>
    /// ⚠️ <b>والكود الفاضي مابيتفحصش</b> — راكة قديمة بتبعت الهوية من
    /// غير كود.
    /// </summary>
    [Fact]
    public async Task An_empty_code_is_not_compared()
    {
        var h = Build();
        var tech = Guid.NewGuid();

        h.Repo.Technicians.Add(new TechnicianIdentityRow
        {
            Id = tech, TenantId = h.Tenant, Code = "100200",
        });

        var result = await Run(
            h, Payload(technicianId: tech.ToString(), technicianCode: ""));

        Assert.Equal(1, result.Value.Added);
        Assert.Equal(tech, Assert.Single(h.Repo.Added).TechnicianId);
    }

    /// <summary>
    /// ⚠️ <b>والكودين لازم يتطابقوا بالحرف — مش بتجاهل الحالة.</b>
    /// الكود أرقام، فاختلاف الحالة مستحيل يبقى إدخال بشري.
    /// </summary>
    [Fact]
    public async Task The_two_codes_match_exactly()
    {
        var h = Build();
        var tech = Guid.NewGuid();

        h.Repo.Technicians.Add(new TechnicianIdentityRow
        {
            Id = tech, TenantId = h.Tenant, Code = "abc123",
        });

        var result = await Run(
            h, Payload(technicianId: tech.ToString(), technicianCode: "ABC123"));

        Assert.Equal(1, result.Value.Rejected);
    }

    // =================================================================
    //  الجهاز — ربط بس، مفيش إنشاء
    // =================================================================

    /// <summary>
    /// 🔴 <b>الترجمة قبل أي حاجة.</b> من غيرها الفحص بيروح <b>لجهاز
    /// مكرر</b> بدل الكانوني.
    /// </summary>
    [Fact]
    public async Task An_alias_resolves_to_the_canonical_device()
    {
        var h = Build();

        var local = Guid.NewGuid();
        var canonical = Guid.NewGuid();

        /*
          ⚠️ **والمعرّف المحلي مالوش صف جهاز — وده شكل الواقع.**

          لما السيرفر يتعرّف على جهاز الراكة بمراسيه، بيضيف **اسم
          مستعار** ومابيعملش صف جديد. فالمعرّف اللي الراكة بتبعته
          مالوش وجود في جدول الأجهزة خالص.

          🔴 **ونسخة أولى من الفحص ده كانت بتحط المعرّف في الجدولين**
          عشان «تقيس الأولوية» — وهي حالة **مستحيلة** في النظام،
          والمسارين في القديم بيختلفوا عليها أصلاً. الفحص اللي تحت
          بيقيس الأولوية الحقيقية: صف **شاهد قبر**.
        */
        h.Repo.Aliases[local] = canonical;
        h.Repo.Devices.Add(canonical);

        await Run(h, Payload(deviceId: local));

        Assert.Equal(canonical, Assert.Single(h.Repo.Added).DeviceId);
    }

    [Fact]
    public async Task An_unknown_device_id_is_flagged_for_review_not_created()
    {
        var h = Build();

        await Run(h, Payload(deviceId: Guid.NewGuid()));

        var row = Assert.Single(h.Repo.Added);

        Assert.Null(row.DeviceId);
        Assert.True(row.NeedsDeviceResolution);
    }

    /// <summary>
    /// ⚠️ <b>ومفيش معرّف ← مطابقة بالمراسي.</b> فحص قديم من قبل هوية
    /// الأجهزة.
    /// </summary>
    [Fact]
    public async Task With_no_id_a_single_anchor_match_links_the_device()
    {
        var h = Build();
        var device = Guid.NewGuid();

        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] = [device];

        await Run(h, Payload(deviceId: null, biosSerial: "abc12345"));

        Assert.Equal(device, Assert.Single(h.Repo.Added).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>ومرساة واحدة على جهازين = مراجعة، مش تخمين.</b>
    ///
    /// <para>الاختيار العشوائي هنا معناه فحص بيروح لجهاز غلط ومحدّش
    /// بيعرف.</para>
    /// </summary>
    [Fact]
    public async Task An_anchor_pointing_at_two_devices_links_nothing()
    {
        var h = Build();

        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] =
            [Guid.NewGuid(), Guid.NewGuid()];

        await Run(h, Payload(deviceId: null, biosSerial: "abc12345"));

        var row = Assert.Single(h.Repo.Added);

        Assert.Null(row.DeviceId);
        Assert.True(row.NeedsDeviceResolution);
    }

    /// <summary>
    /// 🔴 <b>وترتيب القوة: <c>SystemUuid</c> قبل سيريال
    /// البيوس.</b>
    ///
    /// <para>أي اختلاف بين السيرفر والراكة معناه إن <b>نفس اللاب
    /// بياخد هوية مختلفة حسب مين اللي طابق</b>.</para>
    /// </summary>
    [Fact]
    public async Task The_strongest_anchor_decides()
    {
        var h = Build();

        var byUuid = Guid.NewGuid();
        var byBios = Guid.NewGuid();

        h.Repo.Anchors[(DeviceIdentifierKind.SystemUuid, "AAAA-BBBB-CCCC")] = [byUuid];
        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] = [byBios];

        await Run(h, Payload(
            deviceId: null, uuid: "aaaa-bbbb-cccc", biosSerial: "abc12345"));

        Assert.Equal(byUuid, Assert.Single(h.Repo.Added).DeviceId);
    }

    /// <summary>
    /// ⚠️ <b>وسيريال الهارد آخر واحد — أضعف دليل.</b> الهارد بيتنقل
    /// بين لابات.
    /// </summary>
    [Fact]
    public async Task The_disk_serial_is_the_last_resort()
    {
        var h = Build();

        var byBoard = Guid.NewGuid();
        var byDisk = Guid.NewGuid();

        h.Repo.Anchors[(DeviceIdentifierKind.BoardSerial, "BOARD999")] = [byBoard];
        h.Repo.Anchors[(DeviceIdentifierKind.DiskSerial, "DISK1234")] = [byDisk];

        await Run(h, Payload(
            deviceId: null, boardSerial: "board999", diskSerial: "disk1234"));

        Assert.Equal(byBoard, Assert.Single(h.Repo.Added).DeviceId);
    }

    /// <summary>
    /// 🔴 <b>والحشو مابيتسألش عنه خالص.</b> «Default string» بتتكرر
    /// على <b>مئات</b> اللابات — ولو اتسأل عليها، كلهم بيبقوا نفس
    /// الجهاز.
    /// </summary>
    [Theory]
    [InlineData("Default string")]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("None")]
    [InlineData("0000")]
    [InlineData("12")]
    [InlineData("")]
    public async Task A_placeholder_anchor_is_never_queried(string junk)
    {
        var h = Build();

        await Run(h, Payload(deviceId: null, biosSerial: junk));

        Assert.Equal(0, h.Repo.AnchorQueries);
    }

    /// <summary>
    /// 🔴 <b>والربط بيتكسب ومبيتفقدش.</b>
    ///
    /// <para>فحص كان مربوط بجهاز وبعدين اتبعت تاني ومبقاش فيه تطابق
    /// واضح — الربط القديم <b>بيفضل</b>. لولا كده، كل مزامنة كاملة
    /// كانت هتفصل الفحوص عن أجهزتها <b>في صمت</b>.</para>
    /// </summary>
    [Fact]
    public async Task A_report_never_loses_a_device_it_already_had()
    {
        var h = Build();
        var device = Guid.NewGuid();

        h.Repo.Devices.Add(device);

        var dto = Payload(deviceId: device);

        await Run(h, dto);

        // تاني مرة: الراكة مابعتتش معرّف خالص، ومفيش مرساة بتطابق.
        var second = Payload(id: dto.Id, deviceId: null);
        second.GeneralNote = "تاني";

        await Run(h, second);

        var row = h.Repo.Reports[dto.Id];

        Assert.Equal(device, row.DeviceId);
        Assert.False(row.NeedsDeviceResolution);
    }

    /// <summary>⚠️ وكود الجهاز الفاضي مابيدهسش كود موجود.</summary>
    [Fact]
    public async Task An_empty_device_code_does_not_erase_the_stored_one()
    {
        var h = Build();

        var dto = Payload(deviceCode: "LP-00000042");

        await Run(h, dto);

        var second = Payload(id: dto.Id, deviceCode: "");
        second.GeneralNote = "تاني";

        await Run(h, second);

        Assert.Equal("LP-00000042", h.Repo.Reports[dto.Id].DeviceCode);
    }

    // =================================================================
    //  الاسم التجاري على الجهاز
    // =================================================================

    private static Harness WithDevice(out Device device, out Guid deviceId)
    {
        var h = Build();

        deviceId = Guid.NewGuid();

        device = new Device
        {
            Id = deviceId,
            TenantId = h.Tenant,
            PublicCode = "LP-00000042",
            LastKnownManufacturer = "LENOVO",
            LastKnownModel = "81FK",
        };

        h.Repo.Devices.Add(deviceId);
        h.Repo.DeviceRows.Add(device);

        return h;
    }

    /// <summary>
    /// 🔴 <b>الاسم التجاري بينزل على الجهاز بعد ما الفحص
    /// يتخزّن.</b>
    ///
    /// <para>من غير الخطوة دي، الفحص بيشيل
    /// <c>ideapad 330-15ICH</c> وصفحة الأجهزة تفضل تعرض
    /// <c>LENOVO 81FK</c> — وده بالظبط اللي حصل.</para>
    /// </summary>
    [Fact]
    public async Task A_trusted_commercial_name_lands_on_the_device()
    {
        var h = WithDevice(out var device, out var deviceId);

        h.Repo.Evidence[deviceId] =
        [
            new ModelEvidenceRow
            {
                CommercialModelName = "ideapad 330-15ICH",
                CommercialModelSource = "SMBIOS (Product Version)",
                MachineType = "81FK",
                StartedAtUtc = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc),
            },
        ];

        await Run(h, Payload(deviceId: deviceId));

        Assert.Equal("ideapad 330-15ICH", device.CommercialModelName);
        Assert.Equal("SMBIOS (Product Version)", device.CommercialModelSource);
        Assert.Equal("81FK", device.MachineType);

        // ⚠️ وحفظة تانية — الأولى للفحص والتانية للجهاز.
        Assert.Equal(2, h.Work.Saves);
    }

    /// <summary>
    /// 🔴 <b>ومصدر مش موثوق مابيكتبش حاجة.</b> ده اللي بيمنع «إدخال
    /// يدوي» غلط أو مصدر جديد مش متحقق منه إنه يتسرّب لصفحة
    /// الأجهزة.
    /// </summary>
    [Fact]
    public async Task An_untrusted_source_is_ignored()
    {
        var h = WithDevice(out var device, out var deviceId);

        h.Repo.Evidence[deviceId] =
        [
            new ModelEvidenceRow
            {
                CommercialModelName = "اللي المدير كتبه",
                CommercialModelSource = "Manual",
                StartedAtUtc = DateTime.UtcNow,
            },
        ];

        await Run(h, Payload(deviceId: deviceId));

        Assert.Null(device.CommercialModelName);
    }

    /// <summary>
    /// 🔴 <b>والمصدر الموثوق مش كفاية — القيمة نفسها لازم
    /// تتفحص.</b>
    ///
    /// <para><c>SystemFamily</c> مصدر موثوق فعلاً، بس على HP بيرجّع
    /// <c>103C_5336AN HP EliteBook</c> — كود مصنّع مش اسم. <b>١٢
    /// جهاز في الإنتاج</b> اتخزّنوا كده.</para>
    /// </summary>
    [Fact]
    public async Task An_oem_code_is_refused_even_from_a_trusted_source()
    {
        var h = WithDevice(out var device, out var deviceId);

        h.Repo.Evidence[deviceId] =
        [
            new ModelEvidenceRow
            {
                CommercialModelName = "103C_5336AN HP EliteBook",
                CommercialModelSource = "SystemFamily",
                StartedAtUtc = DateTime.UtcNow,
            },
        ];

        await Run(h, Payload(deviceId: deviceId));

        Assert.Null(device.CommercialModelName);
    }

    /// <summary>
    /// ⚠️ <b>والأحدث مش معناه الأصح</b> — أحدث فحص <b>بمصدر
    /// موثوق</b>، والأحدث من غير مصدر مابيدهسش.
    /// </summary>
    [Fact]
    public async Task The_newest_trusted_evidence_wins_not_the_newest()
    {
        var h = WithDevice(out var device, out var deviceId);

        h.Repo.Evidence[deviceId] =
        [
            new ModelEvidenceRow
            {
                CommercialModelName = "اسم مشكوك",
                CommercialModelSource = "Manual",
                StartedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
            },
            new ModelEvidenceRow
            {
                CommercialModelName = "Legion 5 15ARH05",
                CommercialModelSource = "SKU",
                StartedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        ];

        await Run(h, Payload(deviceId: deviceId));

        Assert.Equal("Legion 5 15ARH05", device.CommercialModelName);
    }

    /// <summary>⚠️ وكود المصنع الفاضي مابيمسحش قيمة موجودة.</summary>
    [Fact]
    public async Task An_empty_machine_type_does_not_clear_the_stored_one()
    {
        var h = WithDevice(out var device, out var deviceId);

        device.MachineType = "81FK";

        h.Repo.Evidence[deviceId] =
        [
            new ModelEvidenceRow
            {
                CommercialModelName = "ideapad 330",
                CommercialModelSource = "Model",
                MachineType = "",
                StartedAtUtc = DateTime.UtcNow,
            },
        ];

        await Run(h, Payload(deviceId: deviceId));

        Assert.Equal("81FK", device.MachineType);
    }

    // =================================================================
    //  «قطعة اتغيّرت»
    // =================================================================

    private static ReportSnapshotComponent Component(
        Guid reportId, int type, string serial, int instance = 0,
        bool present = true) =>
        new()
        {
            ReportId = reportId,
            Type = type,
            InstanceIndex = instance,
            ManufacturerSerial = serial,
            HardwareFingerprint = serial,
            IsPresent = present,

            /*
              🔴 **١ = ثقة «أ» (سيريال حقيقي) — وهي شرط على
              `Removed`.**

              أي رقم تاني معناه «القراءة مش مؤكدة»، والمقارنة
              بترفض تبني اتهام عليها. حطّينا ٣ في أول نسخة من
              الفحص ده والعلامة مانزلتش — والمقارنة كانت **صح**.
            */
            IdentityConfidence = 1,
        };

    /// <summary>
    /// 🔴 <b>والوقت هو وقت <u>الفحص</u> اللي كشف التبديل، مش وقت
    /// الاستقبال</b> — الفحص ممكن يكون اتعمل أوفلاين من يومين.
    /// </summary>
    [Fact]
    public async Task A_removed_disk_flags_the_device_with_the_report_time()
    {
        var h = WithDevice(out var device, out var deviceId);

        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();

        var at = new DateTime(2026, 10, 2, 11, 0, 0, DateTimeKind.Utc);

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = older,
            StartedAtUtc = at.AddDays(-5), SnapshotIsPartial = false,
        });

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = newer,
            StartedAtUtc = at, SnapshotIsPartial = false,
        });

        h.Repo.Components.Add(Component(older, ComponentType.Storage, "DISK-AAA"));
        h.Repo.Components.Add(Component(older, ComponentType.Memory, "RAM-111"));

        /*
          🔴 **والفئة لازم تفضل «متفحوصة» في اللقطة الأحدث.**

          فئة ماطلّعتش ولا صف بتتقرا «ماحدش بصّ» مش «اتشال» — وده
          مقصود: الفرق بين تقرير سليم واتهام. فالقارئ بيكتب صف غياب
          صريح (`IsPresent = false`) لما مايلاقيش هارد خالص.
        */
        h.Repo.Components.Add(
            Component(newer, ComponentType.Storage, "", present: false));

        h.Repo.Components.Add(Component(newer, ComponentType.Memory, "RAM-111"));

        await Run(h, Payload(deviceId: deviceId));

        Assert.Equal(at, device.PartChangedAtUtc);
        Assert.Contains("الهارد", device.PartChangeSummary);
    }

    /// <summary>
    /// 🔴 <b>ولقطة ناقصة عمرها ما تتهم.</b> اللقطة الناقصة معناها إن
    /// البرنامج اشتغل من غير صلاحيات مسؤول أو إن قراءة فشلت، والغياب
    /// فيها <b>مايتقاسش عليه</b>.
    /// </summary>
    [Fact]
    public async Task A_partial_snapshot_never_accuses()
    {
        var h = WithDevice(out var device, out var deviceId);

        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = older,
            StartedAtUtc = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
            SnapshotIsPartial = false,
        });

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = newer,
            StartedAtUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),

            // 🔴 الأحدث ناقصة.
            SnapshotIsPartial = true,
        });

        h.Repo.Components.Add(Component(older, ComponentType.Storage, "DISK-AAA"));

        await Run(h, Payload(deviceId: deviceId));

        Assert.Null(device.PartChangedAtUtc);
        Assert.Equal("", device.PartChangeSummary);
    }

    /// <summary>
    /// ⚠️ <b>وفحص واحد مافيهوش مقارنة.</b> «مفيش لقطة على ناحية»
    /// معناها مفيش مقارنة — مش «اتشال كل حاجة».
    /// </summary>
    [Fact]
    public async Task One_report_alone_is_never_a_change()
    {
        var h = WithDevice(out var device, out var deviceId);

        var only = Guid.NewGuid();

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = only,
            StartedAtUtc = DateTime.UtcNow, SnapshotIsPartial = false,
        });

        h.Repo.Components.Add(Component(only, ComponentType.Storage, "DISK-AAA"));

        await Run(h, Payload(deviceId: deviceId));

        Assert.Null(device.PartChangedAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>والعلامة مابتتشالش لوحدها.</b> فحص جديد من غير فروقات
    /// مابيمسحش تحذير قديم: التبديل حصل فعلاً، وإخفاؤه لأن الجهاز
    /// اتفحص تاني بيضيّع الواقعة.
    /// </summary>
    [Fact]
    public async Task A_clean_report_does_not_clear_an_old_warning()
    {
        var h = WithDevice(out var device, out var deviceId);

        var before = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        device.PartChangedAtUtc = before;
        device.PartChangeSummary = "اتشال: الهارد";

        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = older,
            StartedAtUtc = before.AddDays(10), SnapshotIsPartial = false,
        });

        h.Repo.Cursors.Add(new ReportCursorRow
        {
            DeviceId = deviceId, ReportId = newer,
            StartedAtUtc = before.AddDays(20), SnapshotIsPartial = false,
        });

        // نفس اللقطة في الاتنين — مفيش فرق.
        h.Repo.Components.Add(Component(older, ComponentType.Memory, "RAM-111"));
        h.Repo.Components.Add(Component(newer, ComponentType.Memory, "RAM-111"));

        await Run(h, Payload(deviceId: deviceId));

        Assert.Equal(before, device.PartChangedAtUtc);
        Assert.Equal("اتشال: الهارد", device.PartChangeSummary);
    }

    // =================================================================
    //  الحكم النقي
    // =================================================================

    [Fact]
    public void A_partial_snapshot_short_circuits_the_verdict()
    {
        var report = Guid.NewGuid();

        var left = new[] { Component(report, ComponentType.Storage, "A") };
        var right = Array.Empty<ReportSnapshotComponent>();

        Assert.False(
            PartChangeDetector.Compare(left, right, true, false).Changed);

        Assert.False(
            PartChangeDetector.Compare(left, right, false, true).Changed);
    }

    /// <summary>
    /// ⚠️ <b>والملخّص بكلام الفني مش بعناوين الصفحة.</b>
    /// </summary>
    [Theory]
    [InlineData(ComponentType.Storage, "الهارد")]
    [InlineData(ComponentType.Memory, "الرام")]
    [InlineData(ComponentType.Display, "الشاشة")]
    [InlineData(ComponentType.Gpu, "كارت الشاشة")]
    public void The_warning_uses_short_words(int type, string expected)
    {
        Assert.Equal(expected, PartChangeDetector.TypeText(type));

        // 🔴 وصفحة العتاد بتقول حاجة تانية — فالخريطتين مختلفتين
        //    فعلاً.
        Assert.NotEqual(ComponentType.Arabic(type), PartChangeDetector.TypeText(type));
    }

    [Fact]
    public void The_summary_is_clipped_to_the_column()
    {
        var diffs = Enumerable.Range(0, 400)
            .Select(i => new ComponentDiff
            {
                Type = i,
                Kind = ChangeKind.Removed,
            })
            .ToList();

        string text = PartChangeDetector.Describe(diffs);

        Assert.True(text.Length <= PartChangeDetector.MaxSummary);
    }
}
