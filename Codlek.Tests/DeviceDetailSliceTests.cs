using Codlek.Application.Contracts.Devices;
using Codlek.Application.Features.Devices;
using Codlek.Application.Features.Devices.AddNote;
using Codlek.Application.Features.Devices.GetIdentifiers;
using Codlek.Application.Features.Devices.GetLabel;
using Codlek.Application.Features.Devices.GetNotes;
using Codlek.Application.Features.Devices.GetTests;
using Codlek.Application.Features.Devices.GetTimeline;
using Codlek.Core.Devices;
using Codlek.Application.Features.Devices.LookupDevice;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// صفحة اللاب — المسح والمراسي والملاحظات والفحوص.
///
/// <para>🔴 <b>والقطاع ده اتكتب بعد ما مقارنة بين نقط القديم
/// والجديد لقت إنه <u>كله ناقص</u>.</b> قطاع الأجهزة في المشروع
/// الجديد خد القايمة والتصدير وخلاص — والتفاصيل عدّت، ومفيش فحص
/// كان بيبان منه ده: الفحوص بتقيس اللي مكتوب، مفيش حاجة بتقيس اللي
/// مكتوبش.</para>
/// </summary>
public class DeviceDetailSliceTests
{
    private sealed class FakeDeviceRepository : IDeviceRepository
    {
        public readonly List<Device> Devices = [];
        public readonly List<DeviceIdentifierRow> Identifiers = [];
        public readonly List<DeviceNote> Notes = [];
        public readonly List<DeviceTestRow> Tests = [];
        public readonly Dictionary<Guid, string> Racks = [];

        /// <summary>⚠️ بيحفظ اللي اتبعت عشان الفحص يقيس المدخل.</summary>
        public string? LastCode;
        public string? LastNormalized;

        public List<DeviceCodeHit> Hits = [];

        public Task<(IReadOnlyList<DeviceListRow> Rows, int TotalItems)> ListAsync(
            Guid t, DeviceListFilter f, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<DeviceListRow>, int)>(([], 0));

        public Task<IReadOnlyList<DeviceExportRow>> ExportAsync(
            Guid t, DeviceListFilter f, int cap, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceExportRow>>([]);

        public Task<Device?> FindByCodeAsync(
            Guid t, string code, CancellationToken ct = default) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.TenantId == t && d.PublicCode == code));

        public Task<IReadOnlyList<DeviceCodeHit>> ResolveCodeAsync(
            Guid t, string code, string normalized, CancellationToken ct = default)
        {
            LastCode = code;
            LastNormalized = normalized;
            return Task.FromResult<IReadOnlyList<DeviceCodeHit>>(Hits);
        }

        public Task<Device?> FindDetailAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.Id == id && d.TenantId == t));

        /// <summary>⚠️ أرقام الصفحة — المعالج بيعدّيها زي ما هي.</summary>
        public DeviceDetailFacts Facts =
            new(0, 0, 0, 0, null, "", "", null, "", "");

        public Task<DeviceDetailFacts> DetailFactsAsync(
            Guid t, Device d, CancellationToken ct = default) => Task.FromResult(Facts);

        /// <summary>⚠️ مصادر خط الزمن — الفحص بيزرعها ويقيس الدمج.</summary>
        public readonly List<TimelineReportRow> TimelineReports = [];
        public readonly List<TimelineNoteRow> TimelineNotes = [];
        public readonly List<TimelineRepairMomentRow> TimelineMoments = [];
        public readonly List<TimelineMovementRow> TimelineMovements = [];

        /// <summary>⚠️ بيحفظ أكبر `need` اتطلب — فحص التصفيح بيقيسه.</summary>
        public int LastNeed;

        public Task<DeviceTimelineCounts> TimelineCountsAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(new DeviceTimelineCounts(
                TimelineReports.Count,
                TimelineNotes.Count,
                TimelineMoments.Count,
                TimelineMovements.Count));

        public Task<IReadOnlyList<TimelineReportRow>> TimelineReportsAsync(
            Guid t, Guid id, int need, CancellationToken ct = default)
        {
            LastNeed = need;
            return Task.FromResult<IReadOnlyList<TimelineReportRow>>(
                TimelineReports.OrderByDescending(r => r.StartedAtUtc).Take(need).ToList());
        }

        public Task<IReadOnlyList<TimelineNoteRow>> TimelineNotesAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineNoteRow>>(
                TimelineNotes.OrderByDescending(n => n.CreatedAtUtc).Take(need).ToList());

        public Task<IReadOnlyList<TimelineRepairMomentRow>> TimelineRepairMomentsAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineRepairMomentRow>>(
                TimelineMoments.OrderByDescending(m => m.AtUtc).Take(need).ToList());

        public Task<IReadOnlyList<TimelineMovementRow>> TimelineMovementsAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineMovementRow>>(
                TimelineMovements.OrderByDescending(e => e.OccurredAtUtc).Take(need).ToList());

        public Task<IReadOnlyList<DeviceIdentifierRow>> IdentifiersAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceIdentifierRow>>(
                Identifiers.Where(i => i.TenantId == t && i.DeviceId == id)
                    .OrderByDescending(i => i.IsActive)
                    .ThenBy(i => i.Kind)
                    .ThenBy(i => i.Id)
                    .ToList());

        public Task<IReadOnlyList<DeviceNote>> NotesAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceNote>>(
                Notes.Where(n => n.TenantId == t && n.DeviceId == id)
                    .OrderByDescending(n => n.CreatedAtUtc)
                    .ThenByDescending(n => n.Id)
                    .ToList());

        /// <summary>
        /// ⚠️ <b>بيحاكي الحفظ:</b> بيدّي المعرّف وقت الإضافة — الرد
        /// بيرجّع المعرّف، والفحص بيقيس إنه مش صفر.
        /// </summary>
        public void AddNote(DeviceNote note)
        {
            note.Id = Notes.Count + 1;
            Notes.Add(note);
        }

        /// <summary>⚠️ نسخ الفحوص — الفحص بيزرعها بالمعرّف.</summary>
        public readonly Dictionary<Guid, ReportVersionFacts> Versions = [];

        /// <summary>⚠️ بيحفظ المعرّفات اللي اتطلبت — عشان نتأكد إنها قراية واحدة للصفحة.</summary>
        public readonly List<IReadOnlyCollection<Guid>> VersionCalls = [];

        public Task<IReadOnlyDictionary<Guid, ReportVersionFacts>> TestVersionsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            VersionCalls.Add(ids);

            return Task.FromResult<IReadOnlyDictionary<Guid, ReportVersionFacts>>(
                Versions.Where(v => ids.Contains(v.Key))
                    .ToDictionary(v => v.Key, v => v.Value));
        }

        public Task<(IReadOnlyList<DeviceTestRow> Rows, int TotalItems)> TestsAsync(
            Guid t, Guid id, int page, int size, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<DeviceTestRow>, int)>(
                (Tests.Skip((page - 1) * size).Take(size).ToList(), Tests.Count));

        public Task<IReadOnlyDictionary<Guid, string>> LocationNamesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<IReadOnlyDictionary<Guid, TechnicianLabel>> HolderNamesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, TechnicianLabel>>(
                new Dictionary<Guid, TechnicianLabel>());

        public Task<IReadOnlyDictionary<Guid, string>> RackCodesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(Racks);

        public Task<IReadOnlyDictionary<Guid, string>> RackLocationsAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }

    private static (FakeDeviceRepository Repo, FakeCurrentUser Me, Device Device) Build()
    {
        var repo = new FakeDeviceRepository();
        var me = new FakeCurrentUser(UserRole.Manager);

        var device = new Device
        {
            TenantId = me.TenantId,
            PublicCode = "LP-00018425",
            LastKnownManufacturer = "Lenovo",
            LastKnownModel = "20L5",
        };

        repo.Devices.Add(device);
        return (repo, me, device);
    }

    // =================================================================
    //  المسح
    // =================================================================

    /// <summary>
    /// 🔴 <b>المعالج بيبعت الكود <u>كبير الحروف</u> والقيمة الموحّدة
    /// بالتطبيع العربي.</b>
    ///
    /// <para>عمود <c>NormalizedValue</c> في مرساة <c>CompanyCode</c>
    /// بيتكتب بالمطبّع العربي، وهو <b>مابيكبّرش الحروف</b> — فلو
    /// المعالج بعت الكود زي ما المستخدم كتبه، المرساة التاريخية
    /// مابتتلاقاش.</para>
    /// </summary>
    [Fact]
    public async Task The_lookup_sends_an_uppercase_code_and_an_arabic_normalised_value()
    {
        var (repo, me, device) = Build();
        repo.Hits = [new DeviceCodeHit(device.Id, device.PublicCode, true)];

        await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("  lp-00018425  "), default);

        Assert.Equal("LP-00018425", repo.LastCode);
        Assert.Equal("LP-00018425", repo.LastNormalized);
    }

    [Fact]
    public async Task A_single_hit_comes_back_without_a_conflict()
    {
        var (repo, me, device) = Build();
        repo.Hits = [new DeviceCodeHit(device.Id, "LP-00018425", true)];

        var result = await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("LP-00018425"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(device.Id, result.Value.DeviceId);
        Assert.True(result.Value.IsCurrent);
        Assert.False(result.Value.Conflict);
        Assert.Single(result.Value.Matches);
    }

    /// <summary>
    /// 🔴 <b>استيكر اتنقل من لاب للاب.</b> بنرجّع الاتنين ونقول فيه
    /// تعارض، بدل ما نختار واحد في صمت — اللي بيمسح هو اللي يعرف
    /// أنهي لاب في إيده.
    /// </summary>
    [Fact]
    public async Task Two_devices_sharing_one_code_report_a_conflict()
    {
        var (repo, me, device) = Build();
        var other = Guid.NewGuid();

        repo.Hits =
        [
            new DeviceCodeHit(device.Id, "LP-00018425", true),
            new DeviceCodeHit(other, "LP-00099999", false),
        ];

        var result = await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("LP-00018425"), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Conflict);
        Assert.Equal(2, result.Value.Matches.Count);

        // ⚠️ والأول هو الحالي — الترتيب جاي من المستودع ومش بيتغيّر هنا.
        Assert.Equal(device.Id, result.Value.DeviceId);
    }

    /// <summary>
    /// ⚠️ <b>استيكر قديم بيوصّل للاب، بس بعلم إنه مش الكود
    /// الحالي.</b>
    /// </summary>
    [Fact]
    public async Task A_retired_code_resolves_but_is_flagged_as_not_current()
    {
        var (repo, me, device) = Build();
        repo.Hits = [new DeviceCodeHit(device.Id, "LP-00018425", false)];

        var result = await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("LP-00000001"), default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsCurrent);
    }

    /// <summary>
    /// ⚠️ <b>والكود اللي بيرجع هو اللي اتمسح</b> — مش كود الصف. اللي
    /// بيمسح لازم يشوف اللي مسحه.
    /// </summary>
    [Fact]
    public async Task The_scanned_code_is_echoed_back_not_the_row_code()
    {
        var (repo, me, device) = Build();
        repo.Hits = [new DeviceCodeHit(device.Id, "LP-00099999", false)];

        var result = await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("LP-00000001"), default);

        Assert.Equal("LP-00000001", result.Value.PublicCode);
        Assert.Equal("LP-00099999", Assert.Single(result.Value.Matches).PublicCode);
    }

    /// <summary>
    /// 🔴 <b>سببين مختلفين تحت نفس <c>404</c>.</b> «القيمة دي مش
    /// كود» مشكلة مختلفة تماماً عن «الكود مش مسجّل» — والأولى معناها
    /// إن الفني بيمسح QR بتاع حاجة تانية.
    /// </summary>
    [Fact]
    public async Task A_bad_shape_and_a_missing_code_give_different_error_codes()
    {
        var (repo, me, _) = Build();
        repo.Hits = [];

        var shape = await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("كلام فاضي"), default);

        var missing = await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("LP-00000001"), default);

        Assert.True(shape.IsFailure);
        Assert.True(missing.IsFailure);

        Assert.Equal(404, shape.Error.StatusCode);
        Assert.Equal(404, missing.Error.StatusCode);

        Assert.NotEqual(shape.Error.Code, missing.Error.Code);
        Assert.NotEqual(shape.Error.Description, missing.Error.Description);
    }

    /// <summary>
    /// ⚠️ <b>والشكل الغلط مابيوصلش القاعدة خالص</b> — الحارس قبل
    /// الاستعلام مش بعده.
    /// </summary>
    [Fact]
    public async Task A_bad_shape_never_reaches_the_database()
    {
        var (repo, me, _) = Build();

        await new LookupDeviceQueryHandler(repo, me)
            .Handle(new LookupDeviceQuery("SELECT * FROM Devices"), default);

        Assert.Null(repo.LastCode);
    }

    // =================================================================
    //  المراسي
    // =================================================================

    /// <summary>
    /// 🔴 <b>المرساة الملغية بتفضل بتتعرض.</b> هي اللي بتفسّر ليه
    /// بوردة اتغيّرت أو هارد اتبدّل.
    /// </summary>
    [Fact]
    public async Task Retired_identifiers_are_still_listed()
    {
        var (repo, me, device) = Build();

        repo.Identifiers.Add(NewIdentifier(device, DeviceIdentifierKind.DiskSerial, "OLD", false));
        repo.Identifiers.Add(NewIdentifier(device, DeviceIdentifierKind.DiskSerial, "NEW", true));

        var result = await new GetDeviceIdentifiersQueryHandler(repo, me)
            .Handle(new GetDeviceIdentifiersQuery(device.Id), default);

        Assert.Equal(2, result.Value.Count);

        // ⚠️ والنشطة الأول.
        Assert.True(result.Value[0].IsActive);
        Assert.Equal("NEW", result.Value[0].RawValue);
    }

    [Fact]
    public async Task An_identifier_kind_reads_in_arabic_next_to_its_identifier()
    {
        var (repo, me, device) = Build();

        repo.Identifiers.Add(
            NewIdentifier(device, DeviceIdentifierKind.BoardSerial, "PF0ABC", true));

        var result = await new GetDeviceIdentifiersQueryHandler(repo, me)
            .Handle(new GetDeviceIdentifiersQuery(device.Id), default);

        var row = Assert.Single(result.Value);

        // ⚠️ الإنجليزي معرّف للواجهة، والعربي للمستخدم.
        Assert.Equal("BoardSerial", row.Kind);
        Assert.Equal("سيريال البوردة", row.KindText);
    }

    /// <summary>
    /// 🔴 <b>و<c>NormalizedValue</c> مش في العقد خالص</b> — قيمة
    /// داخلية للمطابقة، وعرضها بيخلّي اللي بيقرا يفتكر إنها السيريال
    /// الحقيقي.
    /// </summary>
    [Fact]
    public void The_identifier_contract_has_no_normalised_value()
    {
        var names = typeof(Application.Contracts.Devices.DeviceIdentifierItem)
            .GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.DoesNotContain("normalizedvalue", names);
        Assert.DoesNotContain("normalized", names);
        Assert.Contains("rawvalue", names);
    }

    // =================================================================
    //  الحارس على وجود اللاب
    // =================================================================

    /// <summary>
    /// 🔴 <b>معرّف غلط بيرجّع <c>404</c>، مش قايمة فاضية بـ<c>200</c>.</b>
    ///
    /// <para>القايمة الفاضية بتخلّي اللي بيقرا مايعرفش لو اللاب مالوش
    /// مراسي ولا اللاب نفسه مش موجود — والفرق هو كل الفرق لما حد
    /// بيدوّر على لاب في إيده.</para>
    /// </summary>
    [Fact]
    public async Task Every_detail_endpoint_404s_on_an_unknown_device()
    {
        var (repo, me, _) = Build();
        var ghost = Guid.NewGuid();

        var identifiers = await new GetDeviceIdentifiersQueryHandler(repo, me)
            .Handle(new GetDeviceIdentifiersQuery(ghost), default);

        var notes = await new GetDeviceNotesQueryHandler(repo, me)
            .Handle(new GetDeviceNotesQuery(ghost), default);

        var tests = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(ghost, null, null), default);

        Assert.Equal(DeviceErrors.NotFound, identifiers.Error);
        Assert.Equal(DeviceErrors.NotFound, notes.Error);
        Assert.Equal(DeviceErrors.NotFound, tests.Error);
    }

    /// <summary>
    /// 🔴 <b>ولاب في شركة تانية = مش موجود.</b> معرّف منسوخ من شركة
    /// تانية عمره ما يفتح صفحتها.
    /// </summary>
    [Fact]
    public async Task A_device_in_another_workshop_is_not_found()
    {
        var (repo, me, _) = Build();

        var theirs = new Device { TenantId = Guid.NewGuid(), PublicCode = "LP-00099999" };
        repo.Devices.Add(theirs);

        var result = await new GetDeviceIdentifiersQueryHandler(repo, me)
            .Handle(new GetDeviceIdentifiersQuery(theirs.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(DeviceErrors.NotFound, result.Error);
    }

    // =================================================================
    //  الفحوص
    // =================================================================

    /// <summary>
    /// 🔴 <b>ملاحظة الفني بتطلع في العقد.</b> صاحب الشغل قال
    /// الملاحظات «مش بتوصل ولا بتسمع ع الموقع» — والقيمة كانت
    /// متخزّنة على الفحص من غير ما أي عقد يطلّعها.
    /// </summary>
    [Fact]
    public async Task The_technician_note_on_a_test_is_surfaced()
    {
        var (repo, me, device) = Build();

        repo.Tests.Add(NewTest(note: "الهارد بيطلّع صوت"));

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, null, null), default);

        Assert.Equal("الهارد بيطلّع صوت", Assert.Single(result.Value.Items).GeneralNote);
    }

    [Fact]
    public async Task The_rack_code_is_resolved_or_left_empty()
    {
        var (repo, me, device) = Build();
        var rackId = Guid.NewGuid();

        repo.Racks[rackId] = "RK-07";
        repo.Tests.Add(NewTest(rackId: rackId));
        repo.Tests.Add(NewTest(rackId: Guid.NewGuid()));
        repo.Tests.Add(NewTest(rackId: null));

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, null, null), default);

        var rows = result.Value.Items.ToList();

        Assert.Equal("RK-07", rows[0].RackCode);
        Assert.Equal("", rows[1].RackCode);
        Assert.Equal("", rows[2].RackCode);
    }

    /// <summary>
    /// ⚠️ والعدّادات بتعدّي زي ما هي — خمسة أرقام، مفيش دمج.
    /// </summary>
    [Fact]
    public async Task The_five_counters_pass_through_unmerged()
    {
        var (repo, me, device) = Build();

        repo.Tests.Add(NewTest(pass: 10, fail: 1, error: 2, notPresent: 3, skip: 4));

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, null, null), default);

        var counts = Assert.Single(result.Value.Items).Counts;

        Assert.Equal(10, counts.Pass);
        Assert.Equal(1, counts.Fail);
        Assert.Equal(2, counts.Error);
        Assert.Equal(3, counts.NotPresent);
        Assert.Equal(4, counts.Skip);
    }

    [Fact]
    public async Task The_test_list_is_paged_with_a_total()
    {
        var (repo, me, device) = Build();

        for (int i = 0; i < 30; i++) repo.Tests.Add(NewTest());

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, 2, 25), default);

        Assert.Equal(30, result.Value.TotalItems);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(5, result.Value.Items.Count);
    }

    /// <summary>
    /// 🔴 <b>والافتراضي ٢٥، مش ٤٠ بتاع باقي القوايم.</b>
    ///
    /// <para>قيمة صريحة في القديم مش سهو: تاب الفحوص بيتعرض في
    /// كارت جمب تابات تانية، و٤٠ صف فيه بيطوّل الصفحة لدرجة إن اللي
    /// بعده مابيبانش.</para>
    ///
    /// <para>⚠️ ونسخة أولى من المعالج استعملت افتراضي المشروع (٤٠).
    /// اللي لقطه ضرب HTTP حقيقي — الرد كان بيقول «الحجم ٤٠».</para>
    /// </summary>
    [Fact]
    public async Task The_test_list_defaults_to_twenty_five_a_page()
    {
        var (repo, me, device) = Build();

        for (int i = 0; i < 30; i++) repo.Tests.Add(NewTest());

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, null, null), default);

        Assert.Equal(25, result.Value.PageSize);
        Assert.Equal(25, result.Value.Items.Count);
        Assert.Equal(2, result.Value.TotalPages);

        // ⚠️ وده مش افتراضي المشروع — الفحص بيثبّت الفرق.
        Assert.NotEqual(Core.Paging.Paging.DefaultPageSize, result.Value.PageSize);
    }

    /// <summary>⚠️ واللي بيطلب حجم بياخده — الافتراضي مش سقف.</summary>
    [Fact]
    public async Task A_requested_test_page_size_is_honoured()
    {
        var (repo, me, device) = Build();

        for (int i = 0; i < 30; i++) repo.Tests.Add(NewTest());

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, 1, 10), default);

        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(10, result.Value.Items.Count);
    }


    // =================================================================
    //  خط الزمن — الدمج
    // =================================================================

    /// <summary>
    /// 🔴 <b>الصفحات مع بعض بتحتوي كل حدث <u>مرة واحدة</u> بالظبط.</b>
    ///
    /// <para>ودي الحاجة اللي بتكسر فعلاً: حدث بيظهر في صفحتين، أو
    /// حدث بيختفي خالص. التاريخ بيبان معقول في الحالتين — واللي
    /// بيبص مش بيعرف إن فيه حاجة ناقصة.</para>
    /// </summary>
    [Fact]
    public async Task Every_timeline_event_appears_on_exactly_one_page()
    {
        var (repo, me, device) = Build();

        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        /*
          ⚠️ أحداث من **تلات** مصادر مخلوطة في نفس المدى الزمني،
          عشان الدمج يتقاس مش ترتيب مصدر واحد.
        */
        for (int i = 0; i < 20; i++)
        {
            repo.TimelineReports.Add(new TimelineReportRow(
                Guid.NewGuid(), start.AddMinutes(i * 3), null, 0, "محمود", "T001",
                0, 0, 0, 0, 0));

            repo.TimelineNotes.Add(new TimelineNoteRow(
                i + 1, start.AddMinutes(i * 3 + 1), "كريم", "ملاحظة " + i));

            repo.TimelineMovements.Add(new TimelineMovementRow(
                i + 1, DeviceWorkflowEventType.LocationMoved,
                start.AddMinutes(i * 3 + 2), start.AddMinutes(i * 3 + 2),
                DeviceOperationalStage.Tested, DeviceOperationalStage.Tested,
                null, null, null, null, "", "User", "كريم"));
        }

        var handler = new GetDeviceTimelineQueryHandler(repo, me);

        var first = await handler.Handle(
            new GetDeviceTimelineQuery(device.Id, 1, null), default);

        int total = first.Value.TotalItems;
        int pages = first.Value.TotalPages;

        // ٦٠ صف + حدث الاكتشاف
        Assert.Equal(61, total);
        Assert.Equal(3, pages);

        var seen = new List<string>();

        for (int page = 1; page <= pages; page++)
        {
            var result = await handler.Handle(
                new GetDeviceTimelineQuery(device.Id, page, null), default);

            seen.AddRange(result.Value.Items.Select(
                i => i.Kind + "|" + i.AtUtc.Ticks + "|" + (i.NoteBody ?? "")));
        }

        Assert.Equal(total, seen.Count);
        Assert.Equal(total, seen.Distinct().Count());
    }

    /// <summary>🔴 <b>والترتيب نازل — الأحدث فوق.</b></summary>
    [Fact]
    public async Task The_timeline_runs_newest_first()
    {
        var (repo, me, device) = Build();
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 10; i++)
        {
            repo.TimelineNotes.Add(new TimelineNoteRow(
                i + 1, start.AddHours(i), "كريم", "ملاحظة " + i));
        }

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 1, null), default);

        var times = result.Value.Items.Select(i => i.AtUtc).ToList();

        Assert.Equal(times.OrderByDescending(t => t), times);
    }

    /// <summary>
    /// 🔴 <b>حدث الاكتشاف موجود دايماً — ولاب لسه ماتفحصش عنده حدث
    /// واحد.</b>
    ///
    /// <para>⚠️ وده مسقط من صف اللاب مش صف في جدول، فالإجمالي
    /// بيزوّد واحد بالإيد — ولو اتنسي، آخر صفحة بتبقى فيها حدث
    /// ومحدّش يوصلها.</para>
    /// </summary>
    [Fact]
    public async Task A_device_with_no_history_still_has_its_discovery_event()
    {
        var (repo, me, device) = Build();

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 1, null), default);

        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal(1, result.Value.TotalPages);

        var only = Assert.Single(result.Value.Items);

        Assert.Equal(nameof(DeviceTimelineEventType.DeviceDiscovered), only.Kind);
        Assert.Equal("تم اكتشاف الجهاز لأول مرة", only.Title);
    }

    /// <summary>
    /// 🔴 <b>والرد بيقول حجم الصفحة <u>الحقيقي</u>.</b>
    ///
    /// <para>القديم بياخد <c>pageSize</c> في العنوان، مابيستعملهوش،
    /// وبيرجّعه في الرد كأنه اتطبّق — يعني اللي بيطلب ٥٠ بياخد ٢٥
    /// والرد بيقوله «الحجم ٥٠». السلوك اتنقل زي ما هو والكدبة
    /// اتشالت.</para>
    /// </summary>
    [Fact]
    public async Task The_requested_page_size_is_ignored_and_the_response_says_so()
    {
        var (repo, me, device) = Build();
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 40; i++)
        {
            repo.TimelineNotes.Add(new TimelineNoteRow(
                i + 1, start.AddMinutes(i), "كريم", "ملاحظة"));
        }

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 1, 50), default);

        Assert.Equal(DeviceTimelineOrder.PageSize, result.Value.PageSize);
        Assert.Equal(DeviceTimelineOrder.PageSize, result.Value.Items.Count);
    }

    /// <summary>
    /// ⚠️ <b>وكل مصدر بيتطلب منه «الصفحة × ٢٥» صف</b> — مش كل
    /// التاريخ.
    /// </summary>
    [Fact]
    public async Task Each_source_is_asked_for_page_times_size_rows()
    {
        var (repo, me, device) = Build();

        for (int i = 0; i < 100; i++)
        {
            repo.TimelineNotes.Add(new TimelineNoteRow(
                i + 1, DateTime.UtcNow.AddMinutes(-i), "كريم", "ملاحظة"));
        }

        await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 3, null), default);

        Assert.Equal(75, repo.LastNeed);
    }

    /// <summary>
    /// 🔴 <b>لحظة الصيانة بتتعرض بأمرها، ومفيش حركة مكرّرة
    /// جنبها.</b>
    ///
    /// <para>الاستبعاد في الاستعلام، فالفحص ده بيقيس إن المعالج
    /// مابيضيفهمش من مصدر تاني. والاستعلام نفسه عليه فحص على قاعدة
    /// حقيقية.</para>
    /// </summary>
    [Fact]
    public async Task A_repair_moment_is_shown_without_a_duplicate_movement()
    {
        var (repo, me, device) = Build();
        var at = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        repo.TimelineMoments.Add(new TimelineRepairMomentRow(
            Guid.NewGuid(), at, RepairMoment.Started, "RP-00000001",
            "الكيبورد", "", "", 0, "User", "كريم", null, null));

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 1, null), default);

        var started = result.Value.Items.Single(
            i => i.Kind == nameof(DeviceTimelineEventType.RepairStarted));

        Assert.Equal("بدأت الصيانة", started.Title);
        Assert.Contains("أمر RP-00000001", started.Summary);

        Assert.DoesNotContain(result.Value.Items,
            i => i.Kind == nameof(DeviceTimelineEventType.DeviceMoved));
    }

    /// <summary>
    /// 🔴 <b>ومين المنفّذ بيختلف باختلاف اللحظة.</b> «اتفتح» ممكن
    /// يعمله مدير، و«بدأت» الفني المُسنَد، و«خلصت» اللي قفل — وساعات
    /// تلات ناس مختلفين.
    /// </summary>
    [Fact]
    public async Task Each_repair_moment_names_its_own_actor()
    {
        var (repo, me, device) = Build();
        var at = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var repair = Guid.NewGuid();

        repo.TimelineMoments.Add(new TimelineRepairMomentRow(
            repair, at, RepairMoment.Opened, "RP-1",
            "عطل", "", "", 0, "User", "المدير كريم", null, null));

        repo.TimelineMoments.Add(new TimelineRepairMomentRow(
            repair, at.AddHours(1), RepairMoment.Cancelled, "RP-1",
            "", "", "العميل سحب اللاب", 0, "User", "المدير كريم", null, null));

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 1, null), default);

        var opened = result.Value.Items.Single(
            i => i.Kind == nameof(DeviceTimelineEventType.RepairOpened));

        var cancelled = result.Value.Items.Single(
            i => i.Kind == nameof(DeviceTimelineEventType.RepairCancelled));

        Assert.Equal("المدير كريم", opened.ActorName);

        // 🔴 والإلغاء مالوش منفّذ — اسم مخمّن هنا كان هيبقى اتهام.
        Assert.Equal("", cancelled.ActorName);
        Assert.Contains("العميل سحب اللاب", cancelled.Summary);
    }

    /// <summary>
    /// ⚠️ <b>و<c>reportId</c> بترجع <c>null</c> دايماً</b> — خانة
    /// ميتة متنقولة زي ما هي، واللوحة مابتقراهاش.
    /// </summary>
    [Fact]
    public async Task The_timeline_report_id_is_always_null()
    {
        var (repo, me, device) = Build();

        repo.TimelineReports.Add(new TimelineReportRow(
            Guid.NewGuid(), DateTime.UtcNow, null, 0, "محمود", "T001", 0, 0, 0, 0, 0));

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(device.Id, 1, null), default);

        Assert.All(result.Value.Items, i => Assert.Null(i.ReportId));
    }

    [Fact]
    public async Task The_timeline_404s_on_an_unknown_device()
    {
        var (repo, me, _) = Build();

        var result = await new GetDeviceTimelineQueryHandler(repo, me)
            .Handle(new GetDeviceTimelineQuery(Guid.NewGuid(), 1, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(DeviceErrors.NotFound, result.Error);
    }

    // =================================================================
    //  نسخ البرنامج على كل فحص
    // =================================================================

    /// <summary>
    /// 🔴 <b>كل فحص شايل نسخته — والناقصة «غير متاح».</b>
    ///
    /// <para>القديم كان بيعرض النسختين في كل صف من تاب الفحوص، وده
    /// اللي بيجاوب «ليه الفحصين دول مختلفين». والفحص اللي مالوش صف
    /// (أو قيمته فاضية) بنفس كلمة صفحة الفحص.</para>
    /// </summary>
    [Fact]
    public async Task Each_test_carries_its_own_versions_or_unavailable()
    {
        var (repo, me, device) = Build();

        var full = NewTest();
        var blank = NewTest();
        var missing = NewTest();

        repo.Tests.AddRange([full, blank, missing]);
        repo.Versions[full.ReportId] = new ReportVersionFacts("2.4.1", "7");
        repo.Versions[blank.ReportId] = new ReportVersionFacts("  ", null);

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, null, null), default);

        var rows = result.Value.Items.ToList();

        Assert.Equal("2.4.1", rows[0].ApplicationVersion);
        Assert.Equal("7", rows[0].TestDefinitionVersion);

        Assert.Equal("غير متاح", rows[1].ApplicationVersion);
        Assert.Equal("غير متاح", rows[1].TestDefinitionVersion);

        Assert.Equal("غير متاح", rows[2].ApplicationVersion);
        Assert.Equal("غير متاح", rows[2].TestDefinitionVersion);
    }

    /// <summary>
    /// ⚠️ <b>قراية واحدة للصفحة، بمعرّفات الصفحة بس</b> — مش قراية
    /// لكل فحص، ومش لكل فحوص اللاب.
    /// </summary>
    [Fact]
    public async Task The_versions_are_read_once_for_the_page_rows_only()
    {
        var (repo, me, device) = Build();

        for (int i = 0; i < 30; i++) repo.Tests.Add(NewTest());

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, 2, 25), default);

        var call = Assert.Single(repo.VersionCalls);

        Assert.Equal(
            result.Value.Items.Select(i => i.ReportId).OrderBy(x => x),
            call.OrderBy(x => x));
        Assert.Equal(5, call.Count);
    }

    /// <summary>
    /// ⚠️ <b>«ماتعملتش» من العمود المخزّن على الفحص</b> — نفس رقم صفحة
    /// الفحص وقايمة الفحوص لنفس الفحص.
    /// </summary>
    [Fact]
    public async Task The_not_run_count_is_the_stored_report_column()
    {
        var (repo, me, device) = Build();

        repo.Tests.Add(NewTest(notRun: 3));

        var result = await new GetDeviceTestsQueryHandler(repo, me)
            .Handle(new GetDeviceTestsQuery(device.Id, null, null), default);

        Assert.Equal(3, Assert.Single(result.Value.Items).NotRunCount);
    }

    // =================================================================
    //  إضافة ملاحظة
    // =================================================================

    /// <summary>مستخدم باسم طويل — عشان قصّ الاسم على طول العمود.</summary>
    private sealed class LongNamedUser : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid TenantId { get; init; } = Guid.NewGuid();
        public string DisplayName { get; } = new string('م', 150);
        public string Code => "U002";
        public UserRole Role => UserRole.Manager;
        public bool IsAuthenticated => true;
    }

    /// <summary>
    /// 🔴 <b>لاب شركة تانية = <c>404</c> — حتى لو النص فاضي.</b>
    ///
    /// <para>القديم بيحمّل اللاب بالشركة <b>قبل</b> ما يبص على النص.
    /// لو الفراغ اتفحص الأول، الرد كان هيبقى <c>400</c> ويأكّد إن
    /// المعرّف ده موجود عند حد.</para>
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("ملاحظة سليمة")]
    public async Task A_note_on_another_workshops_device_is_not_found(string body)
    {
        var (repo, me, _) = Build();
        var uow = new FakeUnitOfWork();

        var theirs = new Device { TenantId = Guid.NewGuid(), PublicCode = "LP-00099999" };
        repo.Devices.Add(theirs);

        var result = await new AddDeviceNoteCommandHandler(repo, uow, me)
            .Handle(new AddDeviceNoteCommand(theirs.Id, body), default);

        Assert.Equal(DeviceErrors.NotFound, result.Error);
        Assert.Empty(repo.Notes);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task A_note_on_an_unknown_device_is_not_found()
    {
        var (repo, me, _) = Build();
        var uow = new FakeUnitOfWork();

        var result = await new AddDeviceNoteCommandHandler(repo, uow, me)
            .Handle(new AddDeviceNoteCommand(Guid.NewGuid(), "نص"), default);

        Assert.Equal(DeviceErrors.NotFound, result.Error);
        Assert.Equal(0, uow.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public async Task An_empty_note_is_rejected_with_the_legacy_message(string? body)
    {
        var (repo, me, device) = Build();
        var uow = new FakeUnitOfWork();

        var result = await new AddDeviceNoteCommandHandler(repo, uow, me)
            .Handle(new AddDeviceNoteCommand(device.Id, body), default);

        Assert.Equal(DeviceErrors.NoteEmpty, result.Error);
        Assert.Equal("اكتب الملاحظة الأول.", result.Error.Description);
        Assert.Equal(400, result.Error.StatusCode);
        Assert.Empty(repo.Notes);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task A_note_over_two_thousand_characters_is_rejected()
    {
        var (repo, me, device) = Build();
        var uow = new FakeUnitOfWork();

        var result = await new AddDeviceNoteCommandHandler(repo, uow, me)
            .Handle(new AddDeviceNoteCommand(device.Id, new string('ن', 2001)), default);

        Assert.Equal(DeviceErrors.NoteTooLong, result.Error);
        Assert.Equal("الملاحظة أطول من 2000 حرف.", result.Error.Description);
        Assert.Equal(400, result.Error.StatusCode);
        Assert.Empty(repo.Notes);
        Assert.Equal(0, uow.Saves);
    }

    /// <summary>
    /// ⚠️ <b>الطول بيتقاس بعد القصّ</b> — زي القديم: ٢٠٠٠ حرف ومعاهم
    /// مسافات في الطرفين بتعدّي.
    /// </summary>
    [Fact]
    public async Task The_length_is_measured_after_trimming()
    {
        var (repo, me, device) = Build();

        var result = await new AddDeviceNoteCommandHandler(repo, new FakeUnitOfWork(), me)
            .Handle(new AddDeviceNoteCommand(
                device.Id, "  " + new string('ن', 2000) + "   "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2000, Assert.Single(repo.Notes).Body.Length);
    }

    /// <summary>
    /// 🔴 <b>الكاتب والشركة من التوكن، والنص مقصوص، وحفظة واحدة.</b>
    /// والرد هو الملاحظة بنفس شكل القايمة.
    /// </summary>
    [Fact]
    public async Task The_note_is_saved_with_the_author_from_the_token()
    {
        var (repo, me, device) = Build();
        var uow = new FakeUnitOfWork();

        var result = await new AddDeviceNoteCommandHandler(repo, uow, me)
            .Handle(new AddDeviceNoteCommand(device.Id, "  الشاشة مشروخة من الركن  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, uow.Saves);

        var note = Assert.Single(repo.Notes);

        Assert.Equal(me.TenantId, note.TenantId);
        Assert.Equal(device.Id, note.DeviceId);
        Assert.Equal("الشاشة مشروخة من الركن", note.Body);
        Assert.Equal(me.Id, note.CreatedByUserId);
        Assert.Equal("كريم", note.CreatedByName);
        Assert.True((DateTime.UtcNow - note.CreatedAtUtc).Duration() < TimeSpan.FromMinutes(1));

        Assert.NotEqual(0, result.Value.Id);
        Assert.Equal(note.Id, result.Value.Id);
        Assert.Equal(note.Body, result.Value.Body);
        Assert.Equal("كريم", result.Value.CreatedByName);
        Assert.Equal(note.CreatedAtUtc, result.Value.CreatedAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>والمدموج بياخد ملاحظة</b> — صفحته بتفتح في القديم والفورم
    /// فيها.
    /// </summary>
    [Fact]
    public async Task A_merged_device_still_takes_a_note()
    {
        var (repo, me, device) = Build();
        device.Status = DeviceLifecycleStatus.Merged;

        var result = await new AddDeviceNoteCommandHandler(repo, new FakeUnitOfWork(), me)
            .Handle(new AddDeviceNoteCommand(device.Id, "اندمج في لاب تاني"), default);

        Assert.True(result.IsSuccess);
        Assert.Single(repo.Notes);
    }

    /// <summary>
    /// ⚠️ <b>اسم أطول من العمود بيتقصّ</b> — بدل ما الحفظ يقع والملاحظة
    /// تضيع.
    /// </summary>
    [Fact]
    public async Task A_long_author_name_is_clipped_to_the_column()
    {
        var repo = new FakeDeviceRepository();
        var me = new LongNamedUser();
        var device = new Device { TenantId = me.TenantId, PublicCode = "LP-00000002" };
        repo.Devices.Add(device);

        var result = await new AddDeviceNoteCommandHandler(repo, new FakeUnitOfWork(), me)
            .Handle(new AddDeviceNoteCommand(device.Id, "نص"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(120, Assert.Single(repo.Notes).CreatedByName.Length);
    }

    /// <summary>
    /// 🔴 <b>الجسم مافيهوش غير النص.</b> لو حد زوّد حقل كاتب أو شركة
    /// في العقد، العميل يقدر يكتب باسم غيره.
    /// </summary>
    [Fact]
    public void The_note_request_carries_only_the_body()
    {
        var properties = typeof(AddDeviceNoteRequest).GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(["Body"], properties);
    }

    // =================================================================
    //  ليبل الـQR
    // =================================================================

    /// <summary>رسّام بيسجّل اللي اتطلب منه.</summary>
    private sealed class RecordingRenderer : IDeviceLabelRenderer
    {
        public readonly List<string> Calls = [];

        public string Svg(string publicCode)
        {
            Calls.Add(publicCode);
            return "<svg>" + publicCode + "</svg>";
        }
    }

    /// <summary>
    /// 🔴 <b>اللي بيتشفّر هو الكود العام بالحرف</b> — مش المعرّف
    /// الداخلي ولا رابط.
    /// </summary>
    [Fact]
    public async Task The_label_encodes_exactly_the_public_code()
    {
        var (repo, me, device) = Build();
        var renderer = new RecordingRenderer();

        var result = await new GetDeviceLabelQueryHandler(repo, renderer, me)
            .Handle(new GetDeviceLabelQuery(device.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(["LP-00018425"], renderer.Calls);
        Assert.Equal("LP-00018425", result.Value.PublicCode);
        Assert.Equal("<svg>LP-00018425</svg>", result.Value.Svg);
    }

    /// <summary>
    /// 🔴 <b>مفيش كود = مفيش ليبل</b> — ولا رمز لسلسلة فاضية ولا
    /// للمعرّف الداخلي. والرسّام مابيتناداش أصلاً.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_device_without_a_public_code_has_no_label(string code)
    {
        var (repo, me, device) = Build();
        device.PublicCode = code;
        var renderer = new RecordingRenderer();

        var result = await new GetDeviceLabelQueryHandler(repo, renderer, me)
            .Handle(new GetDeviceLabelQuery(device.Id), default);

        Assert.Equal(DeviceErrors.NoPublicCode, result.Error);
        Assert.Equal("device.no_public_code", result.Error.Code);
        Assert.Equal(404, result.Error.StatusCode);
        Assert.Equal(
            "الجهاز لسه ماخدش كود عام، فمفيش ليبل دائم يتطبع. الكود بيتسجّل أول ما الراكة تزامن.",
            result.Error.Description);
        Assert.Empty(renderer.Calls);
    }

    [Fact]
    public async Task Another_workshops_label_is_not_found()
    {
        var (repo, me, _) = Build();
        var renderer = new RecordingRenderer();

        var theirs = new Device { TenantId = Guid.NewGuid(), PublicCode = "LP-00099999" };
        repo.Devices.Add(theirs);

        var result = await new GetDeviceLabelQueryHandler(repo, renderer, me)
            .Handle(new GetDeviceLabelQuery(theirs.Id), default);

        Assert.Equal(DeviceErrors.NotFound, result.Error);
        Assert.Empty(renderer.Calls);
    }

    // =================================================================
    //  بنّاؤون
    // =================================================================

    private static DeviceIdentifierRow NewIdentifier(
        Device device, DeviceIdentifierKind kind, string raw, bool active)
    {
        return new DeviceIdentifierRow
        {
            Id = Random.Shared.NextInt64(1, long.MaxValue),
            TenantId = device.TenantId,
            DeviceId = device.Id,
            Kind = kind,
            RawValue = raw,
            NormalizedValue = raw,
            Source = "لقطة عتاد",
            Confidence = DeviceIdentityConfidence.A,
            IsActive = active,
            FirstSeenAtUtc = DateTime.UtcNow.AddDays(-5),
            LastSeenAtUtc = DateTime.UtcNow,
        };
    }

    private static DeviceTestRow NewTest(
        Guid? rackId = null, string note = "",
        int pass = 0, int fail = 0, int error = 0, int notPresent = 0, int skip = 0,
        int notRun = 0) =>
        new(
            ReportId: Guid.NewGuid(),
            StartedAtUtc: DateTime.UtcNow.AddHours(-1),
            EndedAtUtc: DateTime.UtcNow,
            DurationMs: 120_000,
            TechnicianId: Guid.NewGuid(),
            TechnicianName: "محمود الفني",
            TechnicianCode: "T001",
            SourceRackId: rackId,
            PassCount: pass,
            FailCount: fail,
            ErrorCount: error,
            NotPresentCount: notPresent,
            SkipCount: skip,
            StepCount: 12,
            GeneralNote: note,
            NotRunCount: notRun);
}
