using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.SyncDevices;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>
/// استقبال الأجهزة من الراكة.
///
/// <para>🔴 <b>والراكة هي اللي بتولّد المعرّف.</b> الفني ماسك اللاب
/// على بنش من غير نت، فالجهاز لازم يتعمل هناك دلوقتي. السيرفر بيعمل
/// <c>upsert</c> بالمعرّف ده — لو ولّد واحد جديد، كل مزامنة كانت
/// هتخلّق <b>نسخة تانية من نفس اللاب</b>.</para>
///
/// <para>🔴 <b>وترتيب الخطوات هنا هو الصح نفسه:</b> الاسم المستعار ←
/// التعرّف على الهوية ← فحص تكرار الكود ← الكتابة. كل قلب في الترتيب
/// ده كان عطل حقيقي في الإنتاج.</para>
/// </summary>
public class DeviceSyncTests
{
    private sealed class FakeDeviceSync : IDeviceSyncRepository
    {
        public readonly List<Device> Devices = [];
        public readonly List<DeviceAlias> Aliases = [];
        public readonly List<DeviceWorkflowEvent> Events = [];
        public readonly List<ImportContainer> Containers = [];

        /// <summary>(نوع، قيمة) → أجهزة.</summary>
        public readonly Dictionary<(DeviceIdentifierKind, string), List<Guid>> Anchors = [];

        /// <summary>أجهزة بتشارك مرساة — بالقيمة المطبَّعة.</summary>
        public readonly Dictionary<string, List<Guid>> Twins = [];

        /// <summary>⚠️ اللي الفحص بيقوله «اتغيّر» — عشان Updated/Unchanged.</summary>
        public bool Touched;

        public int AnchorQueries;
        public int CodeClashQueries;

        /// <summary>⚠️ ترتيب النداءات — الفحص بيقيس إن الترتيب صح.</summary>
        public readonly List<string> Calls = [];

        public Task<IReadOnlyList<Guid>> MatchAnchorAsync(
            Guid tenantId, DeviceIdentifierKind kind, string normalizedValue,
            Guid? exceptDeviceId, int take, CancellationToken ct = default)
        {
            AnchorQueries++;
            Calls.Add("anchor");

            return Task.FromResult<IReadOnlyList<Guid>>(
                Anchors.TryGetValue((kind, normalizedValue), out var list)
                    ? [.. list.Take(take)]
                    : []);
        }

        public Task<DeviceAlias?> FindAliasAsync(
            Guid tenantId, Guid aliasDeviceId, CancellationToken ct = default)
        {
            Calls.Add("alias");

            return Task.FromResult(
                Aliases.FirstOrDefault(a => a.AliasDeviceId == aliasDeviceId));
        }

        public Task<Device?> FindWithIdentifiersAsync(
            Guid tenantId, Guid deviceId, CancellationToken ct = default) =>
            Task.FromResult(Devices.FirstOrDefault(d => d.Id == deviceId));

        public Task<Guid?> CodeClashAsync(
            Guid tenantId, string publicCode, Guid exceptDeviceId,
            CancellationToken ct = default)
        {
            CodeClashQueries++;
            Calls.Add("clash");

            var clash = Devices.FirstOrDefault(
                d => d.PublicCode == publicCode && d.Id != exceptDeviceId);

            return Task.FromResult(clash?.Id);
        }

        public void Add(Device device)
        {
            Calls.Add("add-device");
            Devices.Add(device);
        }

        public void AddAlias(DeviceAlias alias)
        {
            Calls.Add("add-alias");
            Aliases.Add(alias);
        }

        public void AddWorkflowEvent(DeviceWorkflowEvent workflowEvent) =>
            Events.Add(workflowEvent);

        public Task<ImportContainer?> FindContainerAsync(
            Guid tenantId, string normalizedCode, CancellationToken ct = default) =>
            Task.FromResult(
                Containers.FirstOrDefault(c => c.NormalizedCode == normalizedCode));

        public void AddContainer(ImportContainer container) => Containers.Add(container);

        public Task<IReadOnlyList<Guid>> TwinsByAnchorsAsync(
            Guid tenantId, Guid exceptDeviceId, IReadOnlyCollection<string> normalizedValues,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(
                [.. normalizedValues
                    .SelectMany(v => Twins.TryGetValue(v, out var ids) ? ids : [])
                    .Where(id => id != exceptDeviceId)
                    .Distinct()]);

        public Task<IReadOnlyList<Device>> ActiveDevicesAsync(
            Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Device>>(
                [.. Devices.Where(d => ids.Contains(d.Id)
                                    && d.Status == DeviceLifecycleStatus.Active)]);

        public bool WasTouched(Device device) => Touched;
    }

    private sealed record Harness(
        FakeDeviceSync Repo, DeviceSyncApplier Applier, Guid Tenant, Guid RackId);

    private static Harness Build()
    {
        var repo = new FakeDeviceSync();

        return new Harness(
            repo,
            new DeviceSyncApplier(repo, NullLogger<DeviceSyncApplier>.Instance),
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static DeviceSyncPayload Payload(
        Guid? id = null,
        string code = "LP-00000042",
        string? uuid = null,
        string? bios = null,
        string container = "",
        int status = 0,
        string manufacturer = "Dell Inc.",
        string model = "Latitude 5400") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            PublicCode = code,
            Status = status,
            Confidence = 1,
            IdentityBasis = "UUID + BIOS",
            LastKnownManufacturer = manufacturer,
            LastKnownModel = model,
            ContainerCode = container,
            FirstSeenAtUtc = new DateTime(2026, 10, 1, 8, 0, 0),
            LastSeenAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
            FirstSeenByTechnicianCode = "100200",
            Identifiers = BuildAnchors(uuid, bios),
        };

    private static List<DeviceIdentifierPayload> BuildAnchors(string? uuid, string? bios)
    {
        var list = new List<DeviceIdentifierPayload>();

        if (uuid is not null)
        {
            list.Add(new DeviceIdentifierPayload
            {
                Kind = (int)DeviceIdentifierKind.SystemUuid,
                RawValue = uuid,
                NormalizedValue = uuid.ToUpperInvariant(),
                Source = "SMBIOS",
                Confidence = 1,
                FirstSeenAtUtc = new DateTime(2026, 10, 1, 8, 0, 0),
                LastSeenAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
            });
        }

        if (bios is not null)
        {
            list.Add(new DeviceIdentifierPayload
            {
                Kind = (int)DeviceIdentifierKind.BiosSerial,
                RawValue = bios,
                NormalizedValue = bios.ToUpperInvariant(),
                Source = "SMBIOS",
                Confidence = 1,
                FirstSeenAtUtc = new DateTime(2026, 10, 1, 8, 0, 0),
                LastSeenAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
            });
        }

        return list;
    }

    private static Device Existing(
        Harness h, Guid id, string code = "LP-00000001",
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active)
    {
        var device = new Device
        {
            Id = id,
            TenantId = h.Tenant,
            PublicCode = code,
            Status = status,
            LastKnownManufacturer = "Dell Inc.",
            LastKnownModel = "Latitude 5400",
            FirstSeenAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSeenAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        h.Repo.Devices.Add(device);
        return device;
    }

    private static Task<DeviceApplyResult> Apply(Harness h, DeviceSyncPayload dto) =>
        h.Applier.ApplyAsync(h.Tenant, h.RackId, dto, default);

    // =================================================================
    //  الأساسيات
    // =================================================================

    [Fact]
    public async Task A_device_with_no_id_is_refused()
    {
        var h = Build();

        var result = await Apply(h, Payload(id: Guid.Empty));

        Assert.Equal(DeviceApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(DeviceSyncApplier.MissingDeviceId, result.ErrorCode);
        Assert.Empty(h.Repo.Devices);
    }

    [Fact]
    public async Task A_fresh_device_is_added_with_the_rack_that_saw_it_first()
    {
        var h = Build();
        var dto = Payload();

        var result = await Apply(h, dto);

        Assert.Equal(DeviceApplyOutcome.Added, result.Outcome);
        Assert.Equal(dto.Id, result.CanonicalDeviceId);

        var row = Assert.Single(h.Repo.Devices);

        Assert.Equal(dto.Id, row.Id);
        Assert.Equal(h.Tenant, row.TenantId);
        Assert.Equal(h.RackId, row.FirstSeenByRackId);
        Assert.Equal("100200", row.FirstSeenByTechnicianCode);
        Assert.Equal("LP-00000042", row.PublicCode);
    }

    /// <summary>
    /// 🔴 <b>والمعرّف من الراكة — السيرفر مابيولّدش واحد جديد.</b>
    /// لو ولّد، كل مزامنة كانت هتخلّق نسخة تانية من نفس اللاب.
    /// </summary>
    [Fact]
    public async Task The_same_id_twice_updates_one_row()
    {
        var h = Build();
        var dto = Payload();

        await Apply(h, dto);

        h.Repo.Touched = true;

        var again = await Apply(h, dto);

        Assert.Equal(DeviceApplyOutcome.Updated, again.Outcome);
        Assert.Single(h.Repo.Devices);
    }

    /// <summary>
    /// 🔴 <b>وجهاز ماتغيّرش فيه حاجة بيرجع «زي ما هو».</b>
    ///
    /// <para>الراكة بتعدّ الرقم ده، ولو قلنا «اتحدّث» على جهاز
    /// ماتغيّرش، العدّاد اللي المدير بيقيس بيه النشاط <b>بيبقى
    /// وهم</b>.</para>
    /// </summary>
    [Fact]
    public async Task An_untouched_device_comes_back_unchanged()
    {
        var h = Build();
        var dto = Payload();

        await Apply(h, dto);

        h.Repo.Touched = false;

        Assert.Equal(DeviceApplyOutcome.Unchanged, (await Apply(h, dto)).Outcome);
    }

    // =================================================================
    //  ترتيب الخطوات — وده الصح نفسه
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاسم المستعار بيتترجم <u>قبل</u> أي حاجة.</b>
    ///
    /// <para>راكة اتعرّفنا على جهازها قبل كده بتفضل تبعت بمعرّفها
    /// المحلي <b>للأبد</b> — لازم يتترجم كل مرة، من غير ما نطلب من
    /// الراكة تغيّر حاجة.</para>
    /// </summary>
    [Fact]
    public async Task An_alias_is_translated_before_anything_else()
    {
        var h = Build();

        var canonical = Guid.NewGuid();
        var local = Guid.NewGuid();

        Existing(h, canonical, code: "LP-00000001");

        h.Repo.Aliases.Add(new DeviceAlias
        {
            TenantId = h.Tenant,
            AliasDeviceId = local,
            CanonicalDeviceId = canonical,
        });

        h.Repo.Touched = true;

        var result = await Apply(h, Payload(id: local, code: "LP-00000001"));

        Assert.Equal(DeviceApplyOutcome.Updated, result.Outcome);
        Assert.Equal(canonical, result.CanonicalDeviceId);

        // ⚠️ ومفيش صف جديد.
        Assert.Single(h.Repo.Devices);

        // 🔴 والاسم المستعار اتسأل عنه **قبل** المراسي.
        Assert.Equal("alias", h.Repo.Calls[0]);
    }

    /// <summary>
    /// 🔴 <b>والتعرّف على الهوية قبل إنشاء أي جهاز جديد.</b>
    ///
    /// <para>العطل اللي اتكشف في الإنتاج: راكة ماشافتش اللاب بتعمل
    /// جهاز جديد بكود جديد، والسيرفر كان بيقبله زي ما هو <b>من غير ما
    /// يقارن ولا مرساة</b>. اتقاس: نفس اللاب بقى
    /// <c>LP-00000501</c> و<c>LP-00004001</c>.</para>
    /// </summary>
    [Fact]
    public async Task A_known_laptop_arriving_with_a_new_id_becomes_an_alias()
    {
        var h = Build();

        var canonical = Guid.NewGuid();
        var newLocal = Guid.NewGuid();

        Existing(h, canonical, code: "LP-00000042");

        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] = [canonical];
        h.Repo.Touched = true;

        var result = await Apply(h, Payload(id: newLocal, bios: "abc12345", code: "LP-00000042"));

        // 🔴 مااتعملش صف جديد.
        Assert.Single(h.Repo.Devices);
        Assert.Equal(canonical, result.CanonicalDeviceId);
        Assert.NotEqual(DeviceApplyOutcome.Added, result.Outcome);

        // ⚠️ والربط اتسجّل بالسبب.
        var alias = Assert.Single(h.Repo.Aliases);

        Assert.Equal(newLocal, alias.AliasDeviceId);
        Assert.Equal(canonical, alias.CanonicalDeviceId);
        Assert.Equal(h.RackId, alias.SourceRackId);
        Assert.NotEmpty(alias.Reason);
    }

    /// <summary>
    /// 🔴 <b>والهوية الملتبسة = رفض، <u>ومفيش جهاز تالت</u>.</b>
    ///
    /// <para>لما مرساة قوية تشاور على جهاز ومرساة تانية تشاور على
    /// غيره، الإنشاء بيخفي التعارض بدل ما يحله.</para>
    /// </summary>
    [Fact]
    public async Task An_ambiguous_identity_creates_nothing()
    {
        var h = Build();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Existing(h, a, code: "LP-00000001");
        Existing(h, b, code: "LP-00000002");

        h.Repo.Anchors[(DeviceIdentifierKind.SystemUuid, "4C4C4544-0051-3010-8051-B8C04F435931")] = [a];
        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] = [b];

        var result = await Apply(h, Payload(
            uuid: "4c4c4544-0051-3010-8051-b8c04f435931", bios: "abc12345"));

        Assert.Equal(DeviceApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(DeviceSyncApplier.AmbiguousIdentity, result.ErrorCode);

        // 🔴 ولا صف جديد ولا ربط.
        Assert.Equal(2, h.Repo.Devices.Count);
        Assert.Empty(h.Repo.Aliases);
    }

    /// <summary>
    /// 🔴 <b>والرفض معناه «مفيش أثر خالص» — والربط بيتأجّل عشان
    /// كده.</b>
    ///
    /// <para>الإضافة بتفضل قايمة في السياق حتى لو رجّعنا رفض بعديها،
    /// فأول حفظ على صف تاني في نفس الدفعة بيكتبها.</para>
    /// </summary>
    [Fact]
    public async Task A_rejection_after_identity_leaves_no_alias_behind()
    {
        var h = Build();

        var canonical = Guid.NewGuid();
        var other = Guid.NewGuid();
        var newLocal = Guid.NewGuid();

        Existing(h, canonical, code: "LP-00000001");

        // ⚠️ وجهاز تالت شايل الكود اللي جايّ — فالرفض بيحصل **بعد**
        //    التعرّف على الهوية.
        Existing(h, other, code: "LP-00000042");

        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] = [canonical];

        var result = await Apply(h, Payload(
            id: newLocal, bios: "abc12345", code: "LP-00000042"));

        Assert.Equal(DeviceApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(DeviceSyncApplier.DuplicateDeviceCode, result.ErrorCode);

        // 🔴 والربط **مااتضافش** — رغم إن التعرّف نجح.
        Assert.Empty(h.Repo.Aliases);
    }

    /// <summary>
    /// 🔴 <b>وفحص تكرار الكود بعد التعرّف — مش قبله.</b>
    ///
    /// <para>ده كان فوق، والترتيب اتكسر لما كود المخزن بقى هو الهوية:
    /// الراكتين بيقرأوا <b>نفس الكود</b> من نفس الاستيكر، فالفحص وهو
    /// فوق كان بيرفض <b>رفض نهائي</b> قبل ما التعرّف يلحق يقول إن ده
    /// نفس اللاب. النتيجة: <b>تاني راكة تفحص أي لاب مابتقدرش ترفعه
    /// أبداً</b>.</para>
    /// </summary>
    [Fact]
    public async Task The_same_sticker_from_a_second_rack_is_not_a_clash()
    {
        var h = Build();

        var canonical = Guid.NewGuid();
        var secondRackId = Guid.NewGuid();

        // نفس اللاب، نفس الكود المطبوع.
        Existing(h, canonical, code: "LP-00000042");

        h.Repo.Anchors[(DeviceIdentifierKind.BiosSerial, "ABC12345")] = [canonical];
        h.Repo.Touched = true;

        var result = await Apply(h, Payload(
            id: secondRackId, bios: "abc12345", code: "LP-00000042"));

        // 🔴 اتقبل — مش «الكود متكرر».
        Assert.NotEqual(DeviceApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(canonical, result.CanonicalDeviceId);

        // ⚠️ والترتيب: المراسي قبل فحص الكود.
        int anchorAt = h.Repo.Calls.IndexOf("anchor");
        int clashAt = h.Repo.Calls.IndexOf("clash");

        Assert.True(anchorAt >= 0 && clashAt >= 0);
        Assert.True(anchorAt < clashAt, "فحص الكود سبق التعرّف على الهوية.");
    }

    /// <summary>
    /// ⚠️ <b>والكود على هاردوير <u>تاني فعلاً</u> لسه مرفوض.</b>
    /// استيكر واحد على لابين — وده لسه لازم إنسان يشوفه.
    /// </summary>
    [Fact]
    public async Task One_sticker_on_two_different_laptops_is_refused()
    {
        var h = Build();

        var other = Guid.NewGuid();

        Existing(h, other, code: "LP-00000042");

        // مفيش مرساة بتربط الجايّ بالموجود.
        var result = await Apply(h, Payload(bios: "zzz99999", code: "LP-00000042"));

        Assert.Equal(DeviceApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(DeviceSyncApplier.DuplicateDeviceCode, result.ErrorCode);

        // ⚠️ ومش قابل للإعادة — إعادة نفس الحمولة مش هتغيّر النتيجة.
        Assert.False(result.Retryable);
    }

    // =================================================================
    //  الكود
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والكود الفاضي مابيمسحش كود موجود.</b> راكة نسختها أقدم
    /// مالهاش حق تشيل هوية جهاز.
    /// </summary>
    [Fact]
    public async Task An_empty_code_never_erases_the_stored_one()
    {
        var h = Build();
        var id = Guid.NewGuid();

        Existing(h, id, code: "LP-00000001");

        h.Repo.Touched = false;

        await Apply(h, Payload(id: id, code: ""));

        Assert.Equal("LP-00000001", h.Repo.Devices[0].PublicCode);
    }

    /// <summary>
    /// 🔴 <b>والكود اللي اتغيّر بعد التعرّف بيتسجّل كحركة — مش
    /// بيترفض.</b>
    ///
    /// <para>الحالتين شكلهم واحد من هنا: اللاب اتعاد ترقيمه واستيكر
    /// جديد اتلزق، أو الفني كتب رقم غلط. والأولى لازم تعدّي والتانية
    /// لازم حد يشوفها — فبناخد الكود الجديد <b>ونسجّل الحركة</b>،
    /// ومحدّش بيخسر شغل في الحالتين.</para>
    /// </summary>
    [Fact]
    public async Task A_renamed_laptop_takes_the_new_code_and_logs_it()
    {
        var h = Build();

        var canonical = Guid.NewGuid();
        var local = Guid.NewGuid();

        Existing(h, canonical, code: "LP-00000001");

        h.Repo.Aliases.Add(new DeviceAlias
        {
            TenantId = h.Tenant,
            AliasDeviceId = local,
            CanonicalDeviceId = canonical,
        });

        h.Repo.Touched = true;

        await Apply(h, Payload(id: local, code: "LP-00009999"));

        // 🔴 الكود الجديد اتاخد.
        Assert.Equal("LP-00009999", h.Repo.Devices[0].PublicCode);

        // ⚠️ والحركة اتسجّلت بالكودين.
        var ev = Assert.Single(h.Repo.Events);

        Assert.Equal(DeviceWorkflowEventType.CodeChanged, ev.EventType);
        Assert.Contains("LP-00000001", ev.Reason);
        Assert.Contains("LP-00009999", ev.Reason);
        Assert.Equal("مزامنة الراكة", ev.ActorName);
    }

    /// <summary>
    /// ⚠️ <b>وجهاز وصل بمعرّفه هو وكوده اتغيّر = مفيش حركة.</b>
    /// الراكة بتعدّل كودها بنفسها، وده شغل عادي.
    /// </summary>
    [Fact]
    public async Task A_code_change_without_identity_resolution_is_not_logged()
    {
        var h = Build();
        var id = Guid.NewGuid();

        Existing(h, id, code: "LP-00000001");

        h.Repo.Touched = true;

        await Apply(h, Payload(id: id, code: "LP-00009999"));

        Assert.Equal("LP-00009999", h.Repo.Devices[0].PublicCode);
        Assert.Empty(h.Repo.Events);
    }

    /// <summary>
    /// 🔴 <b>والكود بيتسجّل كمرساة — مش عمود بس.</b>
    ///
    /// <para>ومن غير التاريخ ده، أول ما الكود يتغيّر القديم
    /// <b>بيختفي من البحث</b> — واللي ماسك استيكر قديم مالوش أي طريقة
    /// يلاقي اللاب.</para>
    /// </summary>
    [Fact]
    public async Task The_code_is_also_stored_as_an_anchor()
    {
        var h = Build();

        await Apply(h, Payload(code: "LP-00000042"));

        var anchor = Assert.Single(
            h.Repo.Devices[0].Identifiers,
            i => i.Kind == DeviceIdentifierKind.CompanyCode);

        Assert.Equal("LP-00000042", anchor.RawValue);
        Assert.True(anchor.IsActive);
        Assert.Equal(DeviceIdentityConfidence.A, anchor.Confidence);
    }

    /// <summary>
    /// 🔴 <b>والكود القديم بيتعلّم مش نشط — <u>ومابيتمسحش</u>.</b>
    /// والبحث بيفضل يشوفه.
    /// </summary>
    [Fact]
    public async Task An_old_code_stays_as_history_and_stays_searchable()
    {
        var h = Build();
        var id = Guid.NewGuid();

        Existing(h, id, code: "LP-00000001");

        h.Repo.Touched = true;

        await Apply(h, Payload(id: id, code: "LP-00000001"));
        await Apply(h, Payload(id: id, code: "LP-00009999"));

        var row = h.Repo.Devices[0];

        var codes = row.Identifiers
            .Where(i => i.Kind == DeviceIdentifierKind.CompanyCode)
            .ToList();

        Assert.Equal(2, codes.Count);

        Assert.True(codes.Single(c => c.RawValue == "LP-00009999").IsActive);

        var old = codes.Single(c => c.RawValue == "LP-00000001");

        Assert.False(old.IsActive);
        Assert.NotNull(old.SupersededAtUtc);

        // 🔴 والاتنين في نص البحث.
        Assert.Contains("LP-00000001", row.SearchText);
        Assert.Contains("LP-00009999", row.SearchText);
    }

    // =================================================================
    //  الحالة
    // =================================================================

    /// <summary>
    /// 🔴 <b>والحالة مش بترجع لـ«شغّال» من الراكة.</b>
    ///
    /// <para>لو المدير علّم الجهاز «مشكوك إنه مكرر» أو «متقاعد»،
    /// راكة بتزامن حمولة أقدم ماينفعش تلغي القرار ده.</para>
    /// </summary>
    [Theory]
    [InlineData(DeviceLifecycleStatus.DuplicateSuspected)]
    [InlineData(DeviceLifecycleStatus.Retired)]
    public async Task A_managers_decision_survives_a_sync(DeviceLifecycleStatus decided)
    {
        var h = Build();
        var id = Guid.NewGuid();

        Existing(h, id, status: decided);

        h.Repo.Touched = true;

        await Apply(h, Payload(id: id, status: (int)DeviceLifecycleStatus.Active));

        Assert.Equal(decided, h.Repo.Devices[0].Status);
    }

    /// <summary>⚠️ والجهاز الشغّال بياخد الحالة اللي جايّة.</summary>
    [Fact]
    public async Task An_active_device_accepts_the_incoming_status()
    {
        var h = Build();
        var id = Guid.NewGuid();

        Existing(h, id, status: DeviceLifecycleStatus.Active);

        h.Repo.Touched = true;

        await Apply(h, Payload(id: id, status: (int)DeviceLifecycleStatus.Retired));

        Assert.Equal(DeviceLifecycleStatus.Retired, h.Repo.Devices[0].Status);
    }

    // =================================================================
    //  «آخر ظهور» و«أول ظهور»
    // =================================================================

    /// <summary>
    /// ⚠️ <b>«آخر ظهور» بيتقدّم بس، و«أول ظهور» بيرجع لورا بس.</b>
    ///
    /// <para>الراكات بترفع بترتيب مش مضمون، فحمولة أقدم ماينفعش
    /// تقصّر عمر الجهاز.</para>
    /// </summary>
    [Fact]
    public async Task Last_seen_only_moves_forward_and_first_seen_only_backward()
    {
        var h = Build();
        var id = Guid.NewGuid();

        var device = Existing(h, id);

        device.FirstSeenAtUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        device.LastSeenAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);

        h.Repo.Touched = true;

        var dto = Payload(id: id);

        // حمولة أقدم في الاتنين.
        dto.FirstSeenAtUtc = new DateTime(2026, 9, 1, 0, 0, 0);
        dto.LastSeenAtUtc = new DateTime(2026, 9, 20, 0, 0, 0);

        await Apply(h, dto);

        // 🔴 «آخر ظهور» ماتأخّرش.
        Assert.Equal(
            new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
            h.Repo.Devices[0].LastSeenAtUtc);

        // ⚠️ و«أول ظهور» رجع لورا — الجهاز أقدم من اللي كنا فاكرين.
        Assert.Equal(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            h.Repo.Devices[0].FirstSeenAtUtc);
    }

    // =================================================================
    //  الحاوية
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والحاوية بتتعمل لو مش موجودة.</b> الفني بيكتب رمز جديد
    /// على راكة أوفلاين؛ رفض الرمز لأنه مش في الليستة كان هيوقف
    /// الفحص.
    /// </summary>
    [Fact]
    public async Task An_unknown_container_code_creates_the_container()
    {
        var h = Build();

        await Apply(h, Payload(container: "شحنة يناير"));

        var container = Assert.Single(h.Repo.Containers);

        Assert.Equal("شحنة يناير", container.Code);
        Assert.Equal("مزامنة الراكة", container.CreatedByName);
        Assert.Equal(container.Id, h.Repo.Devices[0].ContainerId);
    }

    /// <summary>
    /// 🔴 <b>والحاوية مابتتكتبش فوق ربط موجود.</b>
    ///
    /// <para>صاحب الشغل قال الحاوية «مش بتتغير». ومن غير القاعدة دي،
    /// راكة بتعيد إرسال حمولة قديمة كانت هتنقل اللاب لحاوية غلط
    /// <b>في صمت</b>.</para>
    /// </summary>
    [Fact]
    public async Task A_linked_container_is_never_replaced()
    {
        var h = Build();
        var id = Guid.NewGuid();

        var device = Existing(h, id);
        var first = Guid.NewGuid();

        device.ContainerId = first;

        h.Repo.Touched = true;

        await Apply(h, Payload(id: id, container: "حاوية تانية"));

        Assert.Equal(first, h.Repo.Devices[0].ContainerId);
        Assert.Empty(h.Repo.Containers);
    }

    /// <summary>
    /// ⚠️ <b>والرمز الفاضي معناه «ماتعملش حاجة» مش «امسح».</b>
    /// الراكات القديمة بتبعته فاضي في كل مزامنة.
    /// </summary>
    [Fact]
    public async Task An_empty_container_code_does_nothing()
    {
        var h = Build();

        await Apply(h, Payload(container: ""));

        Assert.Null(h.Repo.Devices[0].ContainerId);
        Assert.Empty(h.Repo.Containers);
    }

    /// <summary>
    /// ⚠️ <b>ونفس الحاوية بخمس طرق كتابة = حاوية واحدة.</b>
    /// </summary>
    [Theory]
    [InlineData("C-1")]
    [InlineData("c 1")]
    [InlineData("C_1")]
    [InlineData("  c-1  ")]
    public async Task The_same_container_written_five_ways_is_one_container(string written)
    {
        var h = Build();

        h.Repo.Containers.Add(new ImportContainer
        {
            TenantId = h.Tenant,
            Code = "C-1",
            NormalizedCode = "c1",
        });

        await Apply(h, Payload(container: written));

        // مااتعملتش حاوية تانية.
        Assert.Single(h.Repo.Containers);
        Assert.NotNull(h.Repo.Devices[0].ContainerId);
    }

    // =================================================================
    //  المراسي
    // =================================================================

    [Fact]
    public async Task An_incoming_anchor_is_stored()
    {
        var h = Build();

        await Apply(h, Payload(bios: "abc12345"));

        var anchor = Assert.Single(
            h.Repo.Devices[0].Identifiers,
            i => i.Kind == DeviceIdentifierKind.BiosSerial);

        Assert.Equal("ABC12345", anchor.NormalizedValue);
        Assert.True(anchor.IsActive);
    }

    /// <summary>
    /// 🔴 <b>وإلغاء التنشيط بيوصل من الراكة، بس الإرجاع لأ.</b>
    /// مرساة اتلغت بقرار مراجعة ماتترجّعش بمزامنة.
    /// </summary>
    [Fact]
    public async Task A_retired_anchor_is_never_revived_by_a_sync()
    {
        var h = Build();
        var id = Guid.NewGuid();

        var device = Existing(h, id);

        device.Identifiers.Add(new DeviceIdentifierRow
        {
            TenantId = h.Tenant,
            DeviceId = id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "abc12345",
            NormalizedValue = "ABC12345",
            IsActive = false,
            SupersededReason = "قرار مراجعة",
        });

        h.Repo.Touched = true;

        // الراكة بتبعتها **نشطة**.
        await Apply(h, Payload(id: id, bios: "abc12345"));

        var anchor = h.Repo.Devices[0].Identifiers
            .Single(i => i.Kind == DeviceIdentifierKind.BiosSerial);

        Assert.False(anchor.IsActive);
        Assert.Equal("قرار مراجعة", anchor.SupersededReason);
    }

    /// <summary>⚠️ والإلغاء من الراكة بيوصل.</summary>
    [Fact]
    public async Task A_deactivation_from_the_rack_is_applied()
    {
        var h = Build();
        var id = Guid.NewGuid();

        var device = Existing(h, id);

        device.Identifiers.Add(new DeviceIdentifierRow
        {
            TenantId = h.Tenant,
            DeviceId = id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "abc12345",
            NormalizedValue = "ABC12345",
            IsActive = true,
        });

        h.Repo.Touched = true;

        var dto = Payload(id: id, bios: "abc12345");

        dto.Identifiers[0].IsActive = false;
        dto.Identifiers[0].SupersededReason = "البوردة اتغيّرت";

        await Apply(h, dto);

        var anchor = h.Repo.Devices[0].Identifiers
            .Single(i => i.Kind == DeviceIdentifierKind.BiosSerial);

        Assert.False(anchor.IsActive);
        Assert.Equal("البوردة اتغيّرت", anchor.SupersededReason);
        Assert.NotNull(anchor.SupersededAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>والمرساة الفاضية بتتجاهل</b> — صف من غير قيمة مطبَّعة
    /// مالوش معنى.
    /// </summary>
    [Fact]
    public async Task An_anchor_with_no_normalised_value_is_dropped()
    {
        var h = Build();

        var dto = Payload(bios: "abc12345");
        dto.Identifiers[0].NormalizedValue = "   ";

        await Apply(h, dto);

        Assert.DoesNotContain(
            h.Repo.Devices[0].Identifiers,
            i => i.Kind == DeviceIdentifierKind.BiosSerial);
    }

    // =================================================================
    //  كشف التكرار — تعليم مش دمج
    // =================================================================

    /// <summary>
    /// 🔴 <b>وجهازين بنفس المرساة بيتعلّموا — <u>مبيتدمجوش</u>.</b>
    ///
    /// <para>الدمج التلقائي بياخد قرار مالوش رجعة على أساس إشارة
    /// <b>غامضة</b>: نفس المرساة ممكن تبقى نفس اللاب، وممكن تبقى
    /// بوردة اتنقلت، وممكن تبقى شركة بتكرّر سيريالاتها. التلاتة
    /// شكلهم واحد. النظام بيقول «دول شكلهم واحد» <b>والمدير
    /// بيقرّر</b>.</para>
    /// </summary>
    [Fact]
    public async Task A_shared_anchor_flags_every_side_and_merges_nothing()
    {
        var h = Build();

        var twinA = Guid.NewGuid();
        var twinB = Guid.NewGuid();

        Existing(h, twinA, code: "LP-00000001");
        Existing(h, twinB, code: "LP-00000002");

        h.Repo.Twins["ABC12345"] = [twinA, twinB];

        var result = await Apply(h, Payload(bios: "abc12345", code: "LP-00000099"));

        // ⚠️ الجهاز الجايّ اتعلّم.
        var incoming = h.Repo.Devices.Single(d => d.PublicCode == "LP-00000099");

        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected, incoming.Status);

        // 🔴 **وكل الأطراف اتعلّموا** — مش أول واحد.
        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected,
            h.Repo.Devices.Single(d => d.Id == twinA).Status);

        Assert.Equal(DeviceLifecycleStatus.DuplicateSuspected,
            h.Repo.Devices.Single(d => d.Id == twinB).Status);

        // ⚠️ ومفيش دمج.
        Assert.Null(incoming.MergedIntoDeviceId);
        Assert.NotEqual(DeviceApplyOutcome.Rejected, result.Outcome);
    }

    /// <summary>
    /// ⚠️ <b>والجهاز اللي المدير علّمه «متقاعد» مابيترجعش
    /// «مشكوك»</b> — نفس قاعدة الحالة.
    /// </summary>
    [Fact]
    public async Task A_retired_twin_keeps_its_status()
    {
        var h = Build();

        var twin = Guid.NewGuid();

        Existing(h, twin, code: "LP-00000001", status: DeviceLifecycleStatus.Retired);

        h.Repo.Twins["ABC12345"] = [twin];

        await Apply(h, Payload(bios: "abc12345", code: "LP-00000099"));

        Assert.Equal(
            DeviceLifecycleStatus.Retired, h.Repo.Devices.Single(d => d.Id == twin).Status);
    }

    [Fact]
    public async Task A_device_with_no_twin_stays_active()
    {
        var h = Build();

        await Apply(h, Payload(bios: "abc12345"));

        Assert.Equal(DeviceLifecycleStatus.Active, h.Repo.Devices[0].Status);
    }
}
