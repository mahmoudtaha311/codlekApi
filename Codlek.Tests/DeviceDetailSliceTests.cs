using Codlek.Application.Features.Devices;
using Codlek.Application.Features.Devices.GetIdentifiers;
using Codlek.Application.Features.Devices.GetNotes;
using Codlek.Application.Features.Devices.GetTests;
using Codlek.Application.Features.Devices.LookupDevice;
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
        int pass = 0, int fail = 0, int error = 0, int notPresent = 0, int skip = 0) =>
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
            GeneralNote: note);
}
