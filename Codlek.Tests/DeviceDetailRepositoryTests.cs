using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Tests;

/// <summary>
/// استعلامات صفحة اللاب — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>وكل فحص هنا بيقيس حاجة المستودع المزيّف بيخبّيها.</b>
/// المزيّف بينفّذ الفلاتر بنفسه في C#، فنطاق غلط في SQL بيعدّي من
/// تحته. والقواعد اللي في الاستعلامات دي بالذات:</para>
///
/// <list type="bullet">
///   <item>صفحة اللاب <b>بتفتح للمدموج</b> — مفيش فلتر حالة.</item>
///   <item>عدّاد المراسي بيعدّ <b>الكل</b>، وعدّاد الفحوص بيعدّ
///   <b>غير الممسوح</b> — نطاقين مختلفين في نفس الدالة.</item>
///   <item>عدد مراحل الفحص استعلام فرعي <b>جوّه الإسقاط</b>؛ لو
///   اتنقل للمعالج بيبقى صفر على كل صف.</item>
///   <item>حركات الصيانة التلاتة مستبعدة من خط الزمن.</item>
/// </list>
/// </summary>
public class DeviceDetailRepositoryTests(DeviceDetailDbFixture fixture)
    : IClassFixture<DeviceDetailDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Device NewDevice(
        AppDbContext db, Guid tenantId, string code,
        DeviceLifecycleStatus status = DeviceLifecycleStatus.Active)
    {
        var device = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownManufacturer = "Lenovo",
            LastKnownModel = "20L5",
            SearchText = ArabicText.Combine(code, "Lenovo", "20L5"),
            Status = status,
            FirstSeenAtUtc = DateTime.UtcNow.AddDays(-30),
            LastSeenAtUtc = DateTime.UtcNow,
        };

        db.Devices.Add(device);
        return device;
    }

    private static Report NewReport(
        AppDbContext db, Guid tenantId, Guid deviceId, DateTime startedAtUtc,
        bool deleted = false, string technicianCode = "T001",
        string technicianName = "محمود", DateTime? snapshotAtUtc = null,
        int steps = 0)
    {
        var report = new Report
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            DeviceCode = "RAW",
            StartedAtUtc = startedAtUtc,
            ReceivedAtUtc = startedAtUtc,
            TechnicianCode = technicianCode,
            TechnicianName = technicianName,
            IsDeleted = deleted,
            SnapshotCapturedAtUtc = snapshotAtUtc,
        };

        for (int i = 0; i < steps; i++)
        {
            report.Steps.Add(new ReportStep
            {
                StepId = "step" + i,
                Title = "خطوة " + i,
            });
        }

        db.Reports.Add(report);
        return report;
    }

    // =================================================================
    //  صفحة اللاب بتفتح للمدموج
    // =================================================================

    /// <summary>
    /// 🔴 <b>مفيش فلتر حالة على البحث بمعرّف.</b>
    ///
    /// <para>صفحة اللاب هي صفحة <b>تاريخه</b>: اللي اندمج في غيره
    /// لازم يفتح ويقول إنه اندمج. ولو الاستعلام استعمل النسخة اللي
    /// بتشيل المدموجين، كل لاب مدموج كانت صفحته بترجّع <c>404</c>
    /// على صف القايمة لسه مشاورة عليه.</para>
    ///
    /// <para>⚠️ <b>والفحص ده مستحيل يتعمل بمستودع مزيّف:</b> المزيّف
    /// بيرجّع الصف على أي حال، فالفلتر في SQL هو اللي بيتقاس.</para>
    /// </summary>
    [Theory]
    [InlineData(DeviceLifecycleStatus.Active)]
    [InlineData(DeviceLifecycleStatus.Merged)]
    [InlineData(DeviceLifecycleStatus.Retired)]
    [InlineData(DeviceLifecycleStatus.DuplicateSuspected)]
    public async Task The_detail_opens_whatever_the_status_is(DeviceLifecycleStatus status)
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-" + (int)status, status);

        await db.SaveChangesAsync();

        var found = await new DeviceRepository(db).FindDetailAsync(tenant, device.Id);

        Assert.NotNull(found);
        Assert.Equal(status, found.Status);
    }

    [Fact]
    public async Task A_device_in_another_workshop_is_not_found_by_id()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);
        var device = NewDevice(db, theirs, "DV-OTHER");

        await db.SaveChangesAsync();

        Assert.Null(await new DeviceRepository(db).FindDetailAsync(mine, device.Id));
    }

    // =================================================================
    //  العدّادات — نطاقات مختلفة
    // =================================================================

    /// <summary>
    /// 🔴 <b>تلات نطاقات في دالة واحدة، وكل واحد لازم يبقى صح
    /// لوحده.</b>
    ///
    /// <para>الفحوص <b>غير الممسوحة</b>؛ المراسي <b>كلها</b> والملغية
    /// معاها؛ واللقطات هي الفحوص اللي معاها لقطة. ولو واحد منهم أخد
    /// نطاق غيره، الرقم بيخالف عدد الصفوف اللي النقطة التانية
    /// بترجّعها — والمدير بيشوف «٣ مراسي» وبيفتح التاب يلاقي
    /// اتنين.</para>
    /// </summary>
    [Fact]
    public async Task Each_counter_uses_its_own_scope()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-COUNT");

        var now = DateTime.UtcNow;

        // فحصين حيّين وواحد ممسوح — والممسوح مش في العدّ.
        NewReport(db, tenant, device.Id, now.AddHours(-3));
        NewReport(db, tenant, device.Id, now.AddHours(-2), snapshotAtUtc: now.AddHours(-2));
        NewReport(db, tenant, device.Id, now.AddHours(-1), deleted: true);

        // تلات مراسي، واحدة ملغية — والملغية **في** العدّ.
        for (int i = 0; i < 3; i++)
        {
            db.DeviceIdentifiers.Add(new DeviceIdentifierRow
            {
                TenantId = tenant,
                DeviceId = device.Id,
                Kind = DeviceIdentifierKind.DiskSerial,
                RawValue = "SN" + i,
                NormalizedValue = "SN" + i,
                Source = "لقطة",
                Confidence = DeviceIdentityConfidence.A,
                IsActive = i != 0,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
            });
        }

        db.DeviceNotes.Add(new DeviceNote
        {
            TenantId = tenant,
            DeviceId = device.Id,
            Body = "ملاحظة",
            CreatedByName = "كريم",
        });

        await db.SaveChangesAsync();

        var facts = await new DeviceRepository(db).DetailFactsAsync(tenant, device);

        // 🔴 الممسوح مستبعد.
        Assert.Equal(2, facts.ReportCount);

        // 🔴 والملغية داخلة.
        Assert.Equal(3, facts.IdentifierCount);

        // ⚠️ فحص واحد هو اللي معاه لقطة.
        Assert.Equal(1, facts.SnapshotCount);

        Assert.Equal(1, facts.NoteCount);
    }

    /// <summary>
    /// 🔴 <b>وعدّاد المراسي بيطابق عدد الصفوف اللي النقطة
    /// بترجّعها.</b>
    ///
    /// <para>ده الفحص اللي بيمنع النطاقين يختلفوا: الرقم على الكارت
    /// والصفوف في التاب لازم يبقوا نفس الحاجة.</para>
    /// </summary>
    [Fact]
    public async Task The_identifier_counter_matches_the_identifier_list()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-MATCH");
        var now = DateTime.UtcNow;

        foreach (bool active in new[] { true, false, false })
        {
            db.DeviceIdentifiers.Add(new DeviceIdentifierRow
            {
                TenantId = tenant,
                DeviceId = device.Id,
                Kind = DeviceIdentifierKind.BoardSerial,
                RawValue = Guid.NewGuid().ToString("N")[..8],
                NormalizedValue = Guid.NewGuid().ToString("N")[..8],
                Source = "لقطة",
                Confidence = DeviceIdentityConfidence.A,
                IsActive = active,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
            });
        }

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var facts = await repo.DetailFactsAsync(tenant, device);
        var rows = await repo.IdentifiersAsync(tenant, device.Id);

        Assert.Equal(rows.Count, facts.IdentifierCount);
        Assert.Equal(3, rows.Count);

        // ⚠️ والنشطة الأول.
        Assert.True(rows[0].IsActive);
    }

    /// <summary>
    /// 🔴 <b>اسم أول فني من <u>أقدم</u> فحص فيه اسم.</b>
    ///
    /// <para>الخانة بتقول «مين اكتشف اللاب» — سؤال تاريخي. والاسم
    /// متكرر على كل فحص، فلو اتصحّح يوم ما، «أحدث فحص» بيكتب على
    /// واقعة قديمة اسم ماكانش موجود وقتها.</para>
    /// </summary>
    [Fact]
    public async Task The_first_seen_technician_name_comes_from_the_oldest_report()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-FIRST");
        device.FirstSeenByTechnicianCode = "T001";

        var now = DateTime.UtcNow;

        NewReport(db, tenant, device.Id, now.AddDays(-10), technicianName: "الاسم القديم");
        NewReport(db, tenant, device.Id, now.AddDays(-1), technicianName: "الاسم الجديد");

        await db.SaveChangesAsync();

        var facts = await new DeviceRepository(db).DetailFactsAsync(tenant, device);

        Assert.Equal("الاسم القديم", facts.FirstSeenTechnicianName);

        // ⚠️ وآخر فني من أحدث فحص — سؤال تاني خالص.
        Assert.Equal("الاسم الجديد", facts.LatestTechnicianName);
    }

    /// <summary>
    /// ⚠️ <b>والفحص اللي اسمه فاضي بيتخطّى.</b> فيه فحوص قديمة
    /// اسمها مش مكتوب، وأخدها بيرجّع فراغ وكأن الاسم مش موجود خالص.
    /// </summary>
    [Fact]
    public async Task A_report_with_a_blank_name_is_skipped()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-BLANK");
        device.FirstSeenByTechnicianCode = "T001";

        var now = DateTime.UtcNow;

        NewReport(db, tenant, device.Id, now.AddDays(-10), technicianName: "");
        NewReport(db, tenant, device.Id, now.AddDays(-5), technicianName: "محمود");

        await db.SaveChangesAsync();

        var facts = await new DeviceRepository(db).DetailFactsAsync(tenant, device);

        Assert.Equal("محمود", facts.FirstSeenTechnicianName);
    }

    /// <summary>
    /// ⚠️ ولاب مالوش كود فني أول بيرجّع فراغ من غير أي استعلام.
    /// </summary>
    [Fact]
    public async Task A_device_with_no_first_seen_code_gets_an_empty_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-NOCODE");

        NewReport(db, tenant, device.Id, DateTime.UtcNow, technicianName: "محمود");

        await db.SaveChangesAsync();

        var facts = await new DeviceRepository(db).DetailFactsAsync(tenant, device);

        Assert.Equal("", facts.FirstSeenTechnicianName);
    }

    // =================================================================
    //  الفحوص — عدد المراحل
    // =================================================================

    /// <summary>
    /// 🔴 <b>وده الفحص اللي مستحيل يتعمل في الذاكرة.</b>
    ///
    /// <para><c>StepCount</c> استعلام فرعي <b>جوّه إسقاط EF</b>.
    /// والاستعلام <c>AsNoTracking</c> ومن غير <c>Include</c> — فلو
    /// العدّ اتنقل للمعالج، بيبقى <b>صفر على كل صف</b> والصفحة بتقول
    /// إن كل الفحوص مالهاش مراحل.</para>
    /// </summary>
    [Fact]
    public async Task The_step_count_comes_from_a_subquery_inside_the_projection()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-STEPS");

        NewReport(db, tenant, device.Id, DateTime.UtcNow, steps: 4);

        await db.SaveChangesAsync();

        var (rows, total) = await new DeviceRepository(db)
            .TestsAsync(tenant, device.Id, page: 1, pageSize: 25);

        Assert.Equal(1, total);
        Assert.Equal(4, Assert.Single(rows).StepCount);
    }

    /// <summary>
    /// 🔴 <b>والترتيب ثابت لو الفحوص بنفس التوقيت بالحرف.</b>
    ///
    /// <para>دفعة فحوص اترفعت من نفس المزامنة بتاخد نفس وقت البداية
    /// — ومن غير فاصل التعادل، الصف بيظهر في صفحتين أو بيختفي.</para>
    /// </summary>
    [Fact]
    public async Task Tests_with_the_same_timestamp_come_back_in_a_stable_order()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-TIE");

        var same = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 6; i++) NewReport(db, tenant, device.Id, same);

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var (first, _) = await repo.TestsAsync(tenant, device.Id, 1, 3);
        var (again, _) = await repo.TestsAsync(tenant, device.Id, 1, 3);
        var (second, _) = await repo.TestsAsync(tenant, device.Id, 2, 3);

        Assert.Equal(
            first.Select(r => r.ReportId), again.Select(r => r.ReportId));

        // ⚠️ ومفيش صف بيظهر في الصفحتين.
        Assert.Empty(first.Select(r => r.ReportId)
            .Intersect(second.Select(r => r.ReportId)));
    }

    [Fact]
    public async Task A_deleted_report_is_not_in_the_test_list()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-DEL");
        var now = DateTime.UtcNow;

        NewReport(db, tenant, device.Id, now.AddHours(-1));
        NewReport(db, tenant, device.Id, now, deleted: true);

        await db.SaveChangesAsync();

        var (rows, total) = await new DeviceRepository(db)
            .TestsAsync(tenant, device.Id, 1, 25);

        Assert.Equal(1, total);
        Assert.Single(rows);
    }

    // =================================================================
    //  خط الزمن
    // =================================================================

    /// <summary>
    /// 🔴 <b>حركات الصيانة التلاتة مستبعدة من خط الزمن.</b>
    ///
    /// <para>أمر الصيانة نفسه معروض بمعلومات أكتر، وعرضهم كمان معناه
    /// كل صيانة مكتوبة <b>مرتين في نفس اللحظة</b>. والاستبعاد في
    /// الاستعلام، فالفحص لازم يبقى على قاعدة حقيقية.</para>
    /// </summary>
    [Fact]
    public async Task The_three_repair_movements_are_excluded_from_the_timeline()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-MOVE");
        var now = DateTime.UtcNow;

        DeviceWorkflowEventType[] all =
        [
            DeviceWorkflowEventType.StageChanged,
            DeviceWorkflowEventType.CustodyHandoff,
            DeviceWorkflowEventType.LocationMoved,
            DeviceWorkflowEventType.SentToRepair,
            DeviceWorkflowEventType.RepairStarted,
            DeviceWorkflowEventType.RepairCompleted,
            DeviceWorkflowEventType.DispatchedToSales,
        ];

        foreach (var type in all)
        {
            db.DeviceWorkflowEvents.Add(new DeviceWorkflowEvent
            {
                TenantId = tenant,
                DeviceId = device.Id,
                EventType = type,
                OccurredAtUtc = now,
                RecordedAtUtc = now,
                ActorType = "User",
                ActorName = "كريم",
            });
        }

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var rows = await repo.TimelineMovementsAsync(tenant, device.Id, 100);
        var kinds = rows.Select(r => r.EventType).ToHashSet();

        Assert.Equal(4, rows.Count);

        Assert.DoesNotContain(DeviceWorkflowEventType.SentToRepair, kinds);
        Assert.DoesNotContain(DeviceWorkflowEventType.RepairStarted, kinds);
        Assert.DoesNotContain(DeviceWorkflowEventType.RepairCompleted, kinds);

        Assert.Contains(DeviceWorkflowEventType.DispatchedToSales, kinds);

        // 🔴 والعدّاد على **نفس** الاستعلام.
        var counts = await repo.TimelineCountsAsync(tenant, device.Id);

        Assert.Equal(rows.Count, counts.Movements);
    }

    /// <summary>
    /// 🔴 <b>أمر صيانة واحد بيدّي أكتر من لحظة على خط الزمن.</b>
    ///
    /// <para>الصف واحد بأربع أعمدة وقت، وخط الزمن بيعرض كل واحد
    /// فيهم كسطر مستقل في مكانه الزمني.</para>
    ///
    /// <para>⚠️ <b>والإنهاء بيملا وقت البداية لو كانت فاضية</b>،
    /// فأمر اتقفل من غير بداية حقيقية بيدّي «بدأت» و«خلصت» في نفس
    /// اللحظة — وده اللي الصف بيقوله فعلاً.</para>
    /// </summary>
    [Fact]
    public async Task One_repair_row_yields_several_timeline_moments()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-REP");
        var now = DateTime.UtcNow;

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-00000001",
            FaultSummary = "الكيبورد",
            Status = RepairStatus.Completed,
            OpenedAtUtc = now.AddHours(-5),
            StartedAtUtc = now.AddHours(-3),
            CompletedAtUtc = now.AddHours(-1),
            UpdatedAtUtc = now.AddHours(-1),
        });

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        var moments = await repo.TimelineRepairMomentsAsync(tenant, device.Id, 100);
        var kinds = moments.Select(m => m.Moment).ToList();

        // اتفتح + بدأ + خلص = تلاتة. والملغي والمتعذّر مش منهم.
        Assert.Equal(3, moments.Count);

        Assert.Contains(RepairMoment.Opened, kinds);
        Assert.Contains(RepairMoment.Started, kinds);
        Assert.Contains(RepairMoment.Completed, kinds);
        Assert.DoesNotContain(RepairMoment.Unable, kinds);
        Assert.DoesNotContain(RepairMoment.Cancelled, kinds);

        // ⚠️ وكود الأمر على كل لحظة — العرض محتاجه.
        Assert.All(moments, m => Assert.Equal("RP-00000001", m.PublicCode));

        var counts = await repo.TimelineCountsAsync(tenant, device.Id);

        Assert.Equal(3, counts.RepairMoments);
    }

    /// <summary>
    /// 🔴 <b>و«تعذّر» و«تمت» مابيطلعوش مع بعض.</b> الاتنين بيقفلوا
    /// نفس العمود، فالحالة هي اللي بتفرّق.
    /// </summary>
    [Fact]
    public async Task A_repair_that_could_not_be_fixed_reports_unable_not_completed()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-UNABLE");
        var now = DateTime.UtcNow;

        db.RepairWorkItems.Add(new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-00000002",
            Status = RepairStatus.UnableToRepair,
            OutcomeReason = "البوردة مش موجودة",
            OpenedAtUtc = now.AddHours(-5),
            CompletedAtUtc = now.AddHours(-1),
            UpdatedAtUtc = now.AddHours(-1),
        });

        await db.SaveChangesAsync();

        var moments = await new DeviceRepository(db)
            .TimelineRepairMomentsAsync(tenant, device.Id, 100);

        var kinds = moments.Select(m => m.Moment).ToList();

        Assert.Contains(RepairMoment.Unable, kinds);
        Assert.DoesNotContain(RepairMoment.Completed, kinds);
    }

    /// <summary>
    /// ⚠️ <b>و«اتلغى» وقته آخر تعديل على الصف.</b> الإلغاء
    /// مابيسجّلش ختم وقت مخصّص، و«ملغي» حالة نهائية — فآخر لمسة على
    /// الصف هي الإلغاء نفسه. ده أقرب دليل متخزّن، مش وقت مخترع.
    ///
    /// <para>🔴 <b>والفحص بيقرا الوقت من الصف بعد الحفظ، مش بيحدّده
    /// قبله.</b> نسخة أولى حطّت <c>UpdatedAtUtc</c> بالإيد ووقعت:
    /// السياق بيختم العمود ده تلقائياً على كل إضافة وكل تعديل
    /// (<c>StampRepairCursor</c>)، فالقيمة المكتوبة من الفحص
    /// بتتدهس.</para>
    ///
    /// <para>⚠️ والختم ده مقصوص للمللي ثانية عن قصد — المؤشّر بيخرج
    /// على السلك وبيرجع في <c>?since=</c>، فدقة أعلى من اللي على
    /// السلك كانت بتخلّي الصف يرجع في كل سحبة للأبد.</para>
    /// </summary>
    [Fact]
    public async Task A_cancelled_repair_is_timed_by_its_last_update()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-CANCEL");

        var repair = new RepairWorkItem
        {
            TenantId = tenant,
            DeviceId = device.Id,
            PublicCode = "RP-00000003",
            Status = RepairStatus.Cancelled,
            OutcomeReason = "العميل سحب اللاب",
            OpenedAtUtc = DateTime.UtcNow.AddHours(-5),
        };

        db.RepairWorkItems.Add(repair);
        await db.SaveChangesAsync();

        // ⚠️ القيمة الفعلية بعد الختم — مش اللي الفحص اختاره.
        using var fresh = fixture.Create();

        var stamped = await fresh.RepairWorkItems
            .AsNoTracking()
            .Where(w => w.Id == repair.Id)
            .Select(w => w.UpdatedAtUtc)
            .SingleAsync();

        var moments = await new DeviceRepository(db)
            .TimelineRepairMomentsAsync(tenant, device.Id, 100);

        var cancelled = moments.Single(m => m.Moment == RepairMoment.Cancelled);

        Assert.Equal(stamped, cancelled.AtUtc);

        // 🔴 وحاجز: الوقت ده مش وقت الفتح — وإلا الفحص بيعدّي على
        //    أي عمود وقت.
        Assert.NotEqual(repair.OpenedAtUtc, cancelled.AtUtc);

        // ⚠️ والسبب بيتعرض — هو اللي بيشرح الإلغاء.
        Assert.Equal("العميل سحب اللاب", cancelled.OutcomeReason);
    }

    /// <summary>
    /// 🔴 <b>وأعداد خط الزمن من <u>نفس</u> الاستعلامات اللي الصفوف
    /// بتتقرا منها.</b>
    ///
    /// <para>العدّ هو اللي بيحسب عدد الصفحات — فرقم ناقص معناه صفحة
    /// أخيرة فيها أحداث ومحدّش يقدر يوصلها.</para>
    /// </summary>
    [Fact]
    public async Task The_timeline_counts_agree_with_the_rows()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-COUNTS");
        var now = DateTime.UtcNow;

        NewReport(db, tenant, device.Id, now.AddHours(-4));
        NewReport(db, tenant, device.Id, now.AddHours(-3), deleted: true);

        db.DeviceNotes.Add(new DeviceNote
        {
            TenantId = tenant, DeviceId = device.Id,
            Body = "ملاحظة", CreatedByName = "كريم",
        });

        db.DeviceWorkflowEvents.Add(new DeviceWorkflowEvent
        {
            TenantId = tenant, DeviceId = device.Id,
            EventType = DeviceWorkflowEventType.LocationMoved,
            OccurredAtUtc = now, RecordedAtUtc = now,
            ActorType = "User", ActorName = "كريم",
        });

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);
        var counts = await repo.TimelineCountsAsync(tenant, device.Id);

        Assert.Equal((await repo.TimelineReportsAsync(tenant, device.Id, 100)).Count,
            counts.Reports);

        Assert.Equal((await repo.TimelineNotesAsync(tenant, device.Id, 100)).Count,
            counts.Notes);

        Assert.Equal((await repo.TimelineMovementsAsync(tenant, device.Id, 100)).Count,
            counts.Movements);

        // ⚠️ والممسوح مستبعد من خط الزمن كمان.
        Assert.Equal(1, counts.Reports);

        // ⚠️ والإجمالي بيزوّد واحد لحدث الاكتشاف.
        Assert.Equal(4, counts.Total);
    }

    /// <summary>
    /// 🔴 <b>الفحص اللي بيقفل باب الفاصل فعلاً.</b>
    ///
    /// <para>فحوص الترتيب فوق <b>مش</b> بتلقط شيل الفاصل — صفوف
    /// قليلة بتطلع بنفس الخطة. والفحص ده بيقرا جملة <c>ORDER BY</c>
    /// المولّدة ويتأكد إن فيها عمود <b>فريد</b>، ومن <b>آخر</b>
    /// <c>ORDER BY</c> في النص مش أولها (الاستعلامات الفرعية ليها
    /// ترتيبها).</para>
    /// </summary>
    [Fact]
    public async Task The_detail_listings_end_with_a_unique_column()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add,
                    [Microsoft.EntityFrameworkCore.DbLoggerCategory.Database.Command.Name],
                    Microsoft.Extensions.Logging.LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "DV-SQL");

        NewReport(db, tenant, device.Id, DateTime.UtcNow);

        db.DeviceNotes.Add(new DeviceNote
        {
            TenantId = tenant, DeviceId = device.Id,
            Body = "ملاحظة", CreatedByName = "كريم",
        });

        db.DeviceIdentifiers.Add(new DeviceIdentifierRow
        {
            TenantId = tenant, DeviceId = device.Id,
            Kind = DeviceIdentifierKind.BiosSerial,
            RawValue = "SN", NormalizedValue = "SN", Source = "لقطة",
            Confidence = DeviceIdentityConfidence.A, IsActive = true,
            FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        var repo = new DeviceRepository(db);

        sql.Clear();

        await repo.TestsAsync(tenant, device.Id, 1, 25);
        await repo.IdentifiersAsync(tenant, device.Id);
        await repo.NotesAsync(tenant, device.Id);
        await repo.TimelineNotesAsync(tenant, device.Id, 25);
        await repo.TimelineMovementsAsync(tenant, device.Id, 25);

        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .Select(line => line[line.LastIndexOf("ORDER BY", StringComparison.Ordinal)..])
            .ToList();

        Assert.Equal(5, ordered.Count);

        Assert.All(ordered, clause =>
            Assert.Contains("[Id]", clause, StringComparison.Ordinal));
    }
}
