using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// استعلامات الاستقبال — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>والملف ده موجود لسبب مقاس.</b> فحوص القطاع بتستعمل
/// مستودع مزيّف، والمزيّف بينفّذ الفلاتر بنفسه — يعني <b>غياب</b>
/// ترشيح الشركة في استعلام الفنيين (وهو مقصود) أو <b>وجوده</b> في
/// استعلام المراسي (وهو الحاجز) مش بيتقاسوا غير هنا.</para>
///
/// <para>⚠️ وكل فحص هنا بيستعمل شركة لوحده: القاعدة مشتركة بين
/// الفحوص، وبعض الاستعلامات <b>عابرة للشركات عن قصد</b>، فالعزل
/// بالشركة اللي بيحمي باقي الملفات مابيحميهاش.</para>
/// </summary>
public class ReportIngestRepositoryTests(ReportIngestDbFixture fixture)
    : IClassFixture<ReportIngestDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Technician NewTech(
        AppDbContext db, Guid tenantId, string code, string username)
    {
        var tech = new Technician
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "فني " + code,
            Username = username,
            NormalizedUsername = username,
            PasswordHash = "hash",
            Salt = "salt",
        };

        db.Technicians.Add(tech);
        return tech;
    }

    private static Device NewDevice(AppDbContext db, Guid tenantId, string code)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownManufacturer = "Dell Inc.",
            LastKnownModel = "Latitude 5400",
        };

        db.Devices.Add(device);
        return device;
    }

    private static Report NewReport(
        AppDbContext db, Guid tenantId, Guid? deviceId, DateTime startedAtUtc,
        bool deleted = false, string? commercial = null, string? source = null,
        string? machineType = null, bool partial = false)
    {
        var report = new Report
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            StartedAtUtc = startedAtUtc,
            IsDeleted = deleted,
            CommercialModelName = commercial,
            CommercialModelSource = source,
            MachineType = machineType,
            SnapshotIsPartial = partial,
        };

        db.Reports.Add(report);
        return report;
    }

    // =================================================================
    //  الفحوص الموجودة + أولادها
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الأولاد لازم ييجوا مع الصف</b> — الاستقبال بيشيلهم
    /// ويكتبهم من تاني، ومن غيرهم بيشيل مجموعة فاضية والصفوف القديمة
    /// بتفضل.
    /// </summary>
    [Fact]
    public async Task An_existing_report_arrives_with_its_children()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var report = NewReport(db, tenant, null, DateTime.UtcNow);

        report.Steps.Add(new ReportStep { StepId = "battery", Title = "البطارية" });
        report.Parts.Add(new ReportPart { Name = "شاشة" });
        report.Edits.Add(new ReportEdit { ByName = "كريم", AtUtc = DateTime.UtcNow });

        report.SnapshotComponents.Add(new ReportSnapshotComponent
        {
            TenantId = tenant, Type = 5, ManufacturerSerial = "DISK-1",
        });

        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        var rows = await new ReportIngestRepository(fresh)
            .ExistingAsync(tenant, [report.Id]);

        var row = rows[report.Id];

        Assert.Single(row.Steps);
        Assert.Single(row.Parts);
        Assert.Single(row.Edits);
        Assert.Single(row.SnapshotComponents);
    }

    /// <summary>
    /// 🔴 <b>والبحث مربوط بالشركة.</b> معرّف الفحص بيتولّد على
    /// الراكة، فنظرياً ممكن يتصادم بين شركتين — ومن غير الترشيح، راكة
    /// بتقدر تدهس فحص شركة تانية.
    /// </summary>
    [Fact]
    public async Task A_report_in_another_workshop_is_not_found()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var report = NewReport(db, theirs, null, DateTime.UtcNow);

        await db.SaveChangesAsync();

        var rows = await new ReportIngestRepository(db).ExistingAsync(mine, [report.Id]);

        Assert.Empty(rows);
    }

    // =================================================================
    //  هوية الفني — عابرة للشركات عن قصد
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاستعلام ده <u>مش</u> مربوط بشركة — وده مقصود.</b>
    ///
    /// <para>من غير كده «فني شركة تانية» و«فني مش موجود» بيبقوا نفس
    /// الحالة، والأولى لازم <b>ترفض</b> (محاولة كتابة في شركة تانية)
    /// والتانية لازم <b>تعدّي</b> (راكة قديمة بتبعت معرّف حسابها
    /// المحلي). الترشيح كان هيحوّل الاتنين لتجاهل صامت.</para>
    /// </summary>
    [Fact]
    public async Task Technician_identities_come_back_across_workshops()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var ours = NewTech(db, mine, "I10001", "ing1");
        var intruder = NewTech(db, theirs, "I10002", "ing2");

        await db.SaveChangesAsync();

        var rows = await new ReportIngestRepository(db)
            .TechnicianIdentitiesAsync([ours.Id, intruder.Id]);

        Assert.Equal(2, rows.Count);

        // 🔴 والشركة راجعة معاهم — هي اللي بيتقرّر بيها الرفض.
        Assert.Equal(theirs, rows.Single(r => r.Id == intruder.Id).TenantId);
        Assert.Equal("I10002", rows.Single(r => r.Id == intruder.Id).Code);
    }

    [Fact]
    public async Task An_empty_identity_list_asks_nothing()
    {
        using var db = fixture.Create();

        Assert.Empty(await new ReportIngestRepository(db).TechnicianIdentitiesAsync([]));
    }

    // =================================================================
    //  المستعار → الكانوني
    // =================================================================

    /// <summary>
    /// 🔴 <b>من غير الترجمة دي، الفحص بيروح لجهاز مكرر بدل
    /// الكانوني.</b>
    /// </summary>
    [Fact]
    public async Task An_alias_resolves_to_the_canonical_device()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var canonical = NewDevice(db, tenant, "LP-10000001");
        var duplicate = NewDevice(db, tenant, "LP-10000002");

        await db.SaveChangesAsync();

        db.DeviceAliases.Add(new DeviceAlias
        {
            TenantId = tenant,
            AliasDeviceId = duplicate.Id,
            CanonicalDeviceId = canonical.Id,
        });

        await db.SaveChangesAsync();

        var repo = new ReportIngestRepository(db);

        Assert.Equal(canonical.Id, await repo.CanonicalForAliasAsync(tenant, duplicate.Id));

        // ⚠️ وجهاز مش مستعار بيرجّع `null` مش `Guid.Empty` — عشان
        //    المنادي مايقارنش بقيمة سحرية.
        Assert.Null(await repo.CanonicalForAliasAsync(tenant, canonical.Id));
    }

    /// <summary>⚠️ والمستعار مربوط بشركته.</summary>
    [Fact]
    public async Task An_alias_from_another_workshop_is_not_followed()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var canonical = NewDevice(db, theirs, "LP-10100001");
        var duplicate = NewDevice(db, theirs, "LP-10100002");

        await db.SaveChangesAsync();

        db.DeviceAliases.Add(new DeviceAlias
        {
            TenantId = theirs,
            AliasDeviceId = duplicate.Id,
            CanonicalDeviceId = canonical.Id,
        });

        await db.SaveChangesAsync();

        Assert.Null(await new ReportIngestRepository(db)
            .CanonicalForAliasAsync(mine, duplicate.Id));
    }

    // =================================================================
    //  المراسي
    // =================================================================

    /// <summary>
    /// 🔴 <b>والتقييد بالشركة هنا هو الحاجز.</b> سيريال بيوس بيتكرر
    /// بين ورشتين (نفس اللاب اتباع ورجع) — ومن غير الترشيح، فحص ورشة
    /// بيتربط بجهاز ورشة تانية.
    /// </summary>
    [Fact]
    public async Task An_anchor_only_matches_inside_the_workshop()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var ours = NewDevice(db, mine, "LP-10200001");
        var hers = NewDevice(db, theirs, "LP-10200002");

        await db.SaveChangesAsync();

        foreach (var (tenant, device) in new[] { (mine, ours), (theirs, hers) })
        {
            db.DeviceIdentifiers.Add(new DeviceIdentifierRow
            {
                TenantId = tenant,
                DeviceId = device.Id,
                Kind = DeviceIdentifierKind.BiosSerial,
                RawValue = "shared-serial",
                NormalizedValue = "SHARED-SERIAL",
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();

        var found = await new ReportIngestRepository(db)
            .DevicesByIdentifierAsync(mine, DeviceIdentifierKind.BiosSerial, "SHARED-SERIAL");

        Assert.Equal(ours.Id, Assert.Single(found));
    }

    /// <summary>
    /// ⚠️ <b>والمرساة المتقاعدة مابتطابقش.</b> مرساة اتسحبت
    /// (<c>IsActive = false</c>) تاريخ، مش هوية حالية — ومطابقتها
    /// بتربط فحص جديد بجهاز اتغيّرت لوحته.
    /// </summary>
    [Fact]
    public async Task A_retired_anchor_never_matches()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-10300001");

        await db.SaveChangesAsync();

        db.DeviceIdentifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant,
            DeviceId = device.Id,
            Kind = DeviceIdentifierKind.BoardSerial,
            RawValue = "old-board",
            NormalizedValue = "OLD-BOARD",
            IsActive = false,
            SupersededReason = "اللوحة اتغيّرت",
        });

        await db.SaveChangesAsync();

        Assert.Empty(await new ReportIngestRepository(db)
            .DevicesByIdentifierAsync(tenant, DeviceIdentifierKind.BoardSerial, "OLD-BOARD"));
    }

    /// <summary>
    /// ⚠️ <b>والنوع جزء من المفتاح.</b> سيريال هارد وسيريال بيوس
    /// بنفس القيمة حاجتين مختلفتين.
    /// </summary>
    [Fact]
    public async Task The_kind_is_part_of_the_key()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-10400001");

        await db.SaveChangesAsync();

        db.DeviceIdentifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant,
            DeviceId = device.Id,
            Kind = DeviceIdentifierKind.DiskSerial,
            RawValue = "same-value",
            NormalizedValue = "SAME-VALUE",
            IsActive = true,
        });

        await db.SaveChangesAsync();

        var repo = new ReportIngestRepository(db);

        Assert.Single(await repo.DevicesByIdentifierAsync(
            tenant, DeviceIdentifierKind.DiskSerial, "SAME-VALUE"));

        Assert.Empty(await repo.DevicesByIdentifierAsync(
            tenant, DeviceIdentifierKind.BiosSerial, "SAME-VALUE"));
    }

    /// <summary>
    /// 🔴 <b>واتنين كفاية — إحنا بنسأل «واحد ولا أكتر؟».</b>
    /// والعدّ الكامل رحلة زيادة على مرساة ممكن تكون على مية جهاز.
    /// </summary>
    [Fact]
    public async Task A_colliding_anchor_stops_at_two()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        for (int i = 0; i < 5; i++)
        {
            var device = NewDevice(db, tenant, $"LP-1050000{i}");

            await db.SaveChangesAsync();

            db.DeviceIdentifiers.Add(new DeviceIdentifierRow
            {
                TenantId = tenant,
                DeviceId = device.Id,
                Kind = DeviceIdentifierKind.BiosSerial,
                RawValue = "collides",
                NormalizedValue = "COLLIDES",
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();

        var found = await new ReportIngestRepository(db)
            .DevicesByIdentifierAsync(tenant, DeviceIdentifierKind.BiosSerial, "COLLIDES");

        Assert.Equal(2, found.Count);
    }

    // =================================================================
    //  دليل الاسم التجاري
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الأحدث الأول، والممسوح والفاضي مستبعدين في
    /// الاستعلام.</b>
    /// </summary>
    [Fact]
    public async Task The_model_evidence_is_newest_first_and_excludes_deleted()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-10600001");

        await db.SaveChangesAsync();

        var day = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        NewReport(db, tenant, device.Id, day, commercial: "قديم", source: "Model");
        NewReport(db, tenant, device.Id, day.AddDays(3), commercial: "أحدث", source: "SKU");

        // ❌ ممسوح.
        NewReport(db, tenant, device.Id, day.AddDays(5),
            deleted: true, commercial: "ممسوح", source: "SKU");

        // ❌ من غير اسم تجاري.
        NewReport(db, tenant, device.Id, day.AddDays(6));

        // ❌ باسم فاضي.
        NewReport(db, tenant, device.Id, day.AddDays(7), commercial: "", source: "SKU");

        await db.SaveChangesAsync();

        var rows = await new ReportIngestRepository(db).ModelEvidenceAsync(device.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal("أحدث", rows[0].CommercialModelName);
        Assert.Equal("قديم", rows[1].CommercialModelName);
    }

    // =================================================================
    //  مؤشّرات الفحوص
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الأحدث الأول — والمعالج بياخد أول اتنين.</b> ترتيب
    /// تنازلي غلط هنا معناه مقارنة بـ<b>أول</b> فحصين بدل آخر
    /// اتنين، والقطعة اللي اتغيّرت بإذن من ٦ شهور بتبقى تحذير
    /// دايم.
    /// </summary>
    [Fact]
    public async Task The_cursors_are_newest_first_and_exclude_deleted()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-10700001");

        await db.SaveChangesAsync();

        var day = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var oldest = NewReport(db, tenant, device.Id, day);
        var middle = NewReport(db, tenant, device.Id, day.AddDays(2));
        var newest = NewReport(db, tenant, device.Id, day.AddDays(4));

        NewReport(db, tenant, device.Id, day.AddDays(9), deleted: true);

        // ❌ من غير جهاز.
        NewReport(db, tenant, deviceId: null, startedAtUtc: day.AddDays(10));

        await db.SaveChangesAsync();

        var rows = await new ReportIngestRepository(db)
            .ReportCursorsAsync(tenant, [device.Id]);

        Assert.Equal(
            [newest.Id, middle.Id, oldest.Id], rows.Select(r => r.ReportId));
    }

    /// <summary>⚠️ والعلم بيرجع معاها — هو اللي بيمنع الاتهام.</summary>
    [Fact]
    public async Task The_partial_flag_rides_along()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-10800001");

        await db.SaveChangesAsync();

        NewReport(db, tenant, device.Id, DateTime.UtcNow, partial: true);

        await db.SaveChangesAsync();

        var row = Assert.Single(await new ReportIngestRepository(db)
            .ReportCursorsAsync(tenant, [device.Id]));

        Assert.True(row.SnapshotIsPartial);
    }

    // =================================================================
    //  لمسة المحطة
    // =================================================================

    /// <summary>
    /// 🔴 <b>وزيادة العدّاد في جملة واحدة مش قراية-ثم-كتابة.</b>
    ///
    /// <para>الراكة بتعيد إرسال نفس الدفعة لو الرد ضاع في الشبكة،
    /// فطلبين متوازيين على نفس الصف بياكلوا زيادة من بعض — والعدّاد
    /// بيتعرض للمالك في قايمة المحطات.</para>
    /// </summary>
    [Fact]
    public async Task The_counter_increments_on_the_stored_value()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var rack = new Rack
        {
            TenantId = tenant,
            RackCode = "RACK-900",
            Name = "محطة الاستقبال",
            ReportsReceived = 7,
        };

        db.Racks.Add(rack);

        await db.SaveChangesAsync();

        var repo = new RackRepository(db);

        await repo.TouchAsync(rack.Id, 3);
        await repo.TouchAsync(rack.Id, 2);

        using var fresh = fixture.Create();
        var stored = await fresh.Racks.SingleAsync(r => r.Id == rack.Id);

        Assert.Equal(12, stored.ReportsReceived);
        Assert.NotNull(stored.LastSeenAtUtc);
    }

    /// <summary>⚠️ ومحطة تانية مابتتلمسش.</summary>
    [Fact]
    public async Task Touching_one_rack_leaves_the_others_alone()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var mine = new Rack { TenantId = tenant, RackCode = "RACK-901", Name = "أ" };
        var other = new Rack { TenantId = tenant, RackCode = "RACK-902", Name = "ب" };

        db.Racks.AddRange(mine, other);

        await db.SaveChangesAsync();

        await new RackRepository(db).TouchAsync(mine.Id, 5);

        using var fresh = fixture.Create();

        Assert.Equal(0, (await fresh.Racks.SingleAsync(r => r.Id == other.Id)).ReportsReceived);
    }

    // =================================================================
    //  رحلة كاملة
    // =================================================================

    /// <summary>
    /// 🔴 <b>والرحلة الكاملة: الفحص وأولاده بيوصلوا القاعدة
    /// فعلاً.</b>
    ///
    /// <para>الفحوص اللي فوق بتقيس كل استعلام لوحده؛ الفحص ده بيقيس
    /// إن الصف اللي المعالج بناه <b>بيتخزّن</b> — ودي الحتة اللي
    /// فيها طول العمود والفهرس الفريد بيقولوا رأيهم.</para>
    /// </summary>
    [Fact]
    public async Task A_written_report_and_its_children_actually_persist()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "LP-10900001");

        await db.SaveChangesAsync();

        var dto = new Application.Contracts.Sync.LaptopReportPayload
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 10, 4, 9, 0, 0),
            DeviceId = device.Id,
            DeviceCode = "LP-10900001",
            TechnicianName = "أحمد فني",
            TechnicianCode = "100200",
            GeneralNote = "الشاشة فيها خط رفيع",
            PartsUsed = ["شاشة"],
            Steps =
            [
                new Application.Contracts.Sync.StepResultPayload
                {
                    Id = "battery", Title = "البطارية", Status = 1,
                },
            ],
            Specs = new Application.Contracts.Sync.DeviceSpecsPayload
            {
                Manufacturer = "Dell Inc.",
                Model = "Latitude 5400",
                SerialNumber = "SER-12345",
                TotalRamBytes = 8589934592,
                InternalDisks =
                [
                    new Application.Contracts.Sync.DiskInfoPayload
                    {
                        SizeBytes = 512_000_000_000, MediaType = "SSD",
                    },
                ],
            },
            Snapshot = new Application.Contracts.Sync.HardwareSnapshotPayload
            {
                CapturedAtUtc = new DateTime(2026, 10, 4, 8, 55, 0),
                CollectorVersion = "1.4.0",
                Components =
                [
                    new Application.Contracts.Sync.SnapshotComponentPayload
                    {
                        Type = 5, ManufacturerSerial = "DISK-1",
                        IdentityConfidence = 1, IsPresent = true,
                    },
                ],
            },
        };

        var row = new Report { Id = dto.Id, TenantId = tenant };

        Application.Features.Rack.IngestReports.ReportRowWriter.Apply(
            row, dto, "{}", null,
            new Application.Features.Rack.IngestReports.DeviceLink(device.Id, false),
            tenant, null);

        new ReportIngestRepository(db).Add(row);

        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        var stored = await fresh.Reports
            .Include(r => r.Steps)
            .Include(r => r.Parts)
            .Include(r => r.SnapshotComponents)
            .AsSplitQuery()
            .SingleAsync(r => r.Id == dto.Id);

        Assert.Equal(device.Id, stored.DeviceId);
        Assert.Equal("8GB", stored.RamText);
        Assert.Equal("512 GB SSD", stored.StorageText);
        Assert.Single(stored.Steps);
        Assert.Single(stored.Parts);
        Assert.Single(stored.SnapshotComponents);

        // ⚠️ وعمود البحث بيتخزّن مطبَّع.
        Assert.Contains("LP-10900001", stored.SearchText);
        Assert.Contains("100200", stored.SearchText);
    }

    // =================================================================
    //  سلسلة الدمج — على قاعدة حقيقية
    // =================================================================

    /// <summary>
    /// 🔴 <b>اسم مستعار على شاهد قبر لازم يوصل للجهاز الحيّ.</b>
    ///
    /// <para>ودي الحالة اللي كانت بتضيّع شغل: «أ ← ب» اتعمل، وبعدين
    /// ب اتدمج في ج. الدمج <b>مابيعيدش توجيه</b> الاسم المستعار
    /// القديم، فأ لسه بيشاور على ب — وب بقى شاهد قبر
    /// (<c>Merged</c>).</para>
    ///
    /// <para>⚠️ والقديم في مسار الاستقبال كان بيقرا الاسم المستعار
    /// <b>خطوة واحدة</b> ويرجّع ب — جهاز ميّت.</para>
    /// </summary>
    [Fact]
    public async Task An_alias_onto_a_merged_device_reaches_the_living_one()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var merged = NewDevice(db, tenant, "LP-11000001");
        var canonical = NewDevice(db, tenant, "LP-11000002");

        await db.SaveChangesAsync();

        // ب اتدمج في ج — والصف بيفضل كشاهد قبر.
        merged.Status = DeviceLifecycleStatus.Merged;
        merged.MergedIntoDeviceId = canonical.Id;

        // وأ (معرّف راكة محلي) بيشاور على ب.
        var local = Guid.NewGuid();

        db.DeviceAliases.Add(new DeviceAlias
        {
            TenantId = tenant,
            AliasDeviceId = local,
            CanonicalDeviceId = merged.Id,
        });

        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        var resolver = new Codlek.Infrastructure.Data.DeviceReference(fresh, Microsoft.Extensions.Logging.Abstractions.NullLogger<Codlek.Infrastructure.Data.DeviceReference>.Instance);

        Assert.Equal(canonical.Id, await resolver.ResolveAsync(tenant, local));

        // ⚠️ وشاهد القبر نفسه بيوصل للحيّ كمان.
        Assert.Equal(canonical.Id, await resolver.ResolveAsync(tenant, merged.Id));
    }

    /// <summary>⚠️ والجهاز الحيّ بيرجّع نفسه، والمجهول مابيظهرش.</summary>
    [Fact]
    public async Task A_live_device_resolves_to_itself_and_an_unknown_one_vanishes()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-11100001");

        await db.SaveChangesAsync();

        var unknown = Guid.NewGuid();

        var resolver = new Codlek.Infrastructure.Data.DeviceReference(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<Codlek.Infrastructure.Data.DeviceReference>.Instance);

        var map = await resolver.ResolveManyAsync(tenant, [device.Id, unknown]);

        Assert.Equal(device.Id, map[device.Id]);
        Assert.False(map.ContainsKey(unknown));
    }

    /// <summary>
    /// 🔴 <b>والترجمة مربوطة بالشركة</b> — جهاز ورشة تانية بيبقى
    /// مجهول، مش بيتربط.
    /// </summary>
    [Fact]
    public async Task A_device_in_another_workshop_is_unknown_not_linked()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var hers = NewDevice(db, theirs, "LP-11200001");

        await db.SaveChangesAsync();

        var resolver = new Codlek.Infrastructure.Data.DeviceReference(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<Codlek.Infrastructure.Data.DeviceReference>.Instance);

        Assert.Null(await resolver.ResolveAsync(mine, hers.Id));
    }

    /// <summary>
    /// ⚠️ <b>والاسم المستعار بتاع ورشة تانية مابيتبعش</b> — بس
    /// الحزام اللي بيمنع ده مش اللي ممكن تتوقعه.
    ///
    /// <para>🔴 <b>ترشيح الشركة على جدول الأسامي المستعارة <u>زيادة
    /// مقسومة</u>:</b> شيلناه بتحوير والفحص عدّى. السبب إن السلسلة
    /// بتنتهي دايماً عند <b>صف جهاز في الشركة دي</b> — واستعلام
    /// الحالة مربوط بالشركة، فالاسم المسرّب بيوصّل لمعرّف مالوش صف
    /// عندنا.</para>
    ///
    /// <para>⚠️ والتسريب محتاج الترشيح يتشال <b>و</b> نفس الـGuid
    /// يبقى جهاز في الورشتين — وده عملياً مش بيحصل. سايبين الحزام
    /// بردو، والفحص ده بيثبّت <b>النتيجة</b> مش الحزام.</para>
    /// </summary>
    [Fact]
    public async Task An_alias_in_another_workshop_is_unknown()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var hers = NewDevice(db, theirs, "LP-11300001");

        await db.SaveChangesAsync();

        var local = Guid.NewGuid();

        db.DeviceAliases.Add(new DeviceAlias
        {
            TenantId = theirs,
            AliasDeviceId = local,
            CanonicalDeviceId = hers.Id,
        });

        await db.SaveChangesAsync();

        var resolver = new Codlek.Infrastructure.Data.DeviceReference(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<Codlek.Infrastructure.Data.DeviceReference>.Instance);

        // 🔴 ورشتي مابتشوفهوش.
        Assert.Null(await resolver.ResolveAsync(mine, local));

        // ⚠️ وحراسة: ورشتهم بتشوفه — فالفحص بيقيس الترشيح مش الغياب.
        Assert.Equal(hers.Id, await resolver.ResolveAsync(theirs, local));
    }

    /// <summary>
    /// ⚠️ <b>و<c>Merged</c> من غير معرّف هدف مش شاهد قبر</b> —
    /// بيانات ناقصة، فالصف نفسه هو الإجابة بدل ما نلف على فاضي.
    ///
    /// <para>🔴 وتحوير خلّى المستودع يحط معرّف مخترع مكان الفاضي
    /// <b>ونجا</b>، لأن الفحص اللي كان بيغطّي الحالة دي بيستعمل
    /// المزيّف — والمزيّف بيبني الحالة بإيده. فالقياس لازم يعدّي من
    /// القاعدة.</para>
    /// </summary>
    [Fact]
    public async Task Merged_with_no_target_resolves_to_itself_from_the_database()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-11400001");

        await db.SaveChangesAsync();

        device.Status = DeviceLifecycleStatus.Merged;
        device.MergedIntoDeviceId = null;

        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        var resolver = new Codlek.Infrastructure.Data.DeviceReference(fresh, Microsoft.Extensions.Logging.Abstractions.NullLogger<Codlek.Infrastructure.Data.DeviceReference>.Instance);

        Assert.Equal(device.Id, await resolver.ResolveAsync(tenant, device.Id));
    }

    /// <summary>
    /// ⚠️ <b>و<c>Guid.Empty</c> كهدف دمج مش هدف كمان</b> — نفس
    /// المعنى بشكل تاني.
    /// </summary>
    [Fact]
    public async Task Merged_into_an_empty_guid_resolves_to_itself()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var device = NewDevice(db, tenant, "LP-11500001");

        await db.SaveChangesAsync();

        device.Status = DeviceLifecycleStatus.Merged;
        device.MergedIntoDeviceId = Guid.Empty;

        await db.SaveChangesAsync();

        using var fresh = fixture.Create();

        var resolver = new Codlek.Infrastructure.Data.DeviceReference(fresh, Microsoft.Extensions.Logging.Abstractions.NullLogger<Codlek.Infrastructure.Data.DeviceReference>.Instance);

        Assert.Equal(device.Id, await resolver.ResolveAsync(tenant, device.Id));
    }
}
