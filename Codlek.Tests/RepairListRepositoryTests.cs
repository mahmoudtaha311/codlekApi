using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Tests;

/// <summary>
/// قايمة الصيانة — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>الفحوص دي لازم تبقى على قاعدة، مش على مستودع
/// بديل.</b> كل القواعد اللي بتتكسر هنا عايشة <b>جوّه
/// الاستعلام</b>: فاصل التعادل في الترتيب، المدى نصف المفتوح،
/// العدّ قبل التصفيح، والترشيح بالشركة. مستودع بديل بيعدّي عليهم
/// كلهم.</para>
/// </summary>
public class RepairListRepositoryTests(RepairListDbFixture fixture)
    : IClassFixture<RepairListDbFixture>
{
    private static Guid NewTenant(AppDbContext db)
    {
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        return tenant.Id;
    }

    private static Device NewDevice(AppDbContext db, Guid tenantId, string code)
    {
        var row = new Device
        {
            TenantId = tenantId,
            PublicCode = code,
            LastKnownManufacturer = "HP",
            LastKnownModel = "6470b",
            LastSeenAtUtc = DateTime.UtcNow,
        };
        db.Devices.Add(row);
        return row;
    }

    private static RepairWorkItem NewItem(
        AppDbContext db, Guid tenantId, Guid deviceId, string code,
        DateTime openedAtUtc,
        RepairStatus status = RepairStatus.New,
        RepairApproval approval = RepairApproval.Pending,
        Guid? technicianId = null,
        string faultSummary = "الشاشة بايظة")
    {
        var row = new RepairWorkItem
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            PublicCode = code,
            OpenedAtUtc = openedAtUtc,
            Status = status,
            Approval = approval,
            AssignedTechnicianId = technicianId,
            FaultSummary = faultSummary,
            OpenedByName = "كريم",
            OpenedByActorType = "User",
            SearchText = ArabicText.Combine(code, faultSummary),
        };
        db.RepairWorkItems.Add(row);
        return row;
    }

    // =================================================================
    //  الترتيب والتصفيح
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفحص الأهم في الملف.</b>
    ///
    /// <para>عشرة أوامر بـ<b>نفس</b> <c>OpenedAtUtc</c> بالحرف — زي
    /// دفعة اتفتحت من فحص واحد. نقرا صفحتين ونتأكد إن العشرة كلهم
    /// ظهروا <b>مرة واحدة</b> لكل واحد.</para>
    ///
    /// <para>⚠️ من غير <c>ThenBy(Id)</c>، SQL Server حر يرتّب
    /// المتعادلين بأي شكل في كل استعلام — فصف بيظهر في الصفحتين وصف
    /// تاني بيختفي خالص. والفحص ده بيقع فعلاً لما نشيل الفاصل.</para>
    /// </summary>
    [Fact]
    public async Task Paging_never_duplicates_or_drops_rows_that_share_an_open_time()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-TIE");

        // ⚠️ نفس اللحظة بالحرف — ده اللي بيحصل في دفعة واحدة.
        var sameMoment = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 10; i++)
            NewItem(db, tenant, device.Id, $"RP-TIE{i:D2}", sameMoment);

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var first = await repo.ListAsync(tenant, new RepairListFilter { Page = 1, PageSize = 5 });
        var second = await repo.ListAsync(tenant, new RepairListFilter { Page = 2, PageSize = 5 });

        var seen = first.Rows.Concat(second.Rows).Select(r => r.PublicCode).ToList();

        Assert.Equal(10, first.TotalItems);
        Assert.Equal(10, seen.Count);
        Assert.Equal(10, seen.Distinct().Count());
    }

    /// <summary>
    /// 🔴 <b>وده الفحص اللي بيقفل الباب فعلاً.</b>
    ///
    /// <para>الفحص اللي فوق <b>مابيلقطش</b> شيل <c>ThenBy(Id)</c> —
    /// جرّبناه: شلنا الفاصل والعشرة فضلوا طالعين صح. السبب إن SQL
    /// Server بيستعمل نفس الخطة في الاستعلامين لما الصفوف قليلة،
    /// فالترتيب بيطلع ثابت <b>بالعرض</b> مش بالعقد.</para>
    ///
    /// <para>⚠️ فالفحص ده بيقرا جملة <c>ORDER BY</c> المولّدة
    /// ويتأكد إن فيها عمود <b>فريد</b>. ده الحاجة الوحيدة اللي
    /// بتفضل صح مهما كان حجم البيانات أو الخطة.</para>
    /// </summary>
    [Fact]
    public async Task The_paged_query_orders_by_a_unique_column()
    {
        var sql = new List<string>();

        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
                .Options);

        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-SQL");

        NewItem(db, tenant, device.Id, "RP-SQL",
            new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        await repo.ListAsync(tenant, new RepairListFilter());
        await repo.ListAsync(tenant, new RepairListFilter { Oldest = true });
        await repo.ListForDeviceAsync(tenant, device.Id);

        var ordered = sql
            .Where(line => line.Contains("ORDER BY", StringComparison.Ordinal))
            .Select(line => line[line.IndexOf("ORDER BY", StringComparison.Ordinal)..])
            .ToList();

        // ⚠️ تلات استعلامات بترتيب: الأحدث، الأقدم، وتبويب الجهاز.
        Assert.Equal(3, ordered.Count);

        Assert.All(ordered, clause =>
            Assert.Contains("[Id]", clause, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Newest_first_by_default_and_oldest_first_when_asked()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-SORT");

        var day = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        NewItem(db, tenant, device.Id, "RP-OLD", day);
        NewItem(db, tenant, device.Id, "RP-NEW", day.AddHours(5));

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var newest = await repo.ListAsync(tenant, new RepairListFilter());
        var oldest = await repo.ListAsync(tenant, new RepairListFilter { Oldest = true });

        Assert.Equal("RP-NEW", newest.Rows[0].PublicCode);
        Assert.Equal("RP-OLD", oldest.Rows[0].PublicCode);
    }

    /// <summary>
    /// 🔴 العدّ لازم يبقى قبل التصفيح — غير كده «٥ من ٥» بتظهر على
    /// كل صفحة وأزرار التنقّل بتختفي.
    /// </summary>
    [Fact]
    public async Task Total_counts_the_filtered_set_not_the_page()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-TOTAL");

        var day = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 7; i++)
            NewItem(db, tenant, device.Id, $"RP-TOT{i}", day.AddMinutes(i));

        await db.SaveChangesAsync();

        var (rows, total) = await new RepairRepository(db)
            .ListAsync(tenant, new RepairListFilter { Page = 1, PageSize = 3 });

        Assert.Equal(3, rows.Count);
        Assert.Equal(7, total);
    }

    // =================================================================
    //  الفلاتر
    // =================================================================

    /// <summary>
    /// 🔴 <b>المدى نصف مفتوح.</b>
    ///
    /// <para>أمر اتفتح ٣ مارس ١١ بالليل لازم يدخل في «من ١ لـ٣
    /// مارس». ولو الحد الأعلى كان <c>&lt;=</c> على بداية اليوم، كل
    /// أوامر اليوم الأخير كانت تختفي من التقرير.</para>
    /// </summary>
    [Fact]
    public async Task Upper_bound_is_exclusive_so_the_last_day_is_fully_included()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-RANGE");

        NewItem(db, tenant, device.Id, "RP-BEFORE",
            new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc));

        // ⚠️ آخر لحظة في اليوم الأخير — ده اللي بيقع.
        NewItem(db, tenant, device.Id, "RP-LATE",
            new DateTime(2026, 3, 3, 23, 30, 0, DateTimeKind.Utc));

        /*
          🔴 **الصف ده هو اللي بيفرّق بين <c>&lt;</c>
          و<c>&lt;=</c>.**

          هو على الحد **بالظبط** — بداية اليوم اللي بعد المدى. ومن
          غيره الفحص بيعدّي على المسخ: جرّبنا نحوّل الشرط
          لـ<c>&lt;=</c> والفحص فضل أخضر، لأن مفيش ولا صف على
          الحد.
        */
        NewItem(db, tenant, device.Id, "RP-ON-BOUND",
            new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc));

        NewItem(db, tenant, device.Id, "RP-AFTER",
            new DateTime(2026, 3, 5, 1, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var (rows, _) = await new RepairRepository(db).ListAsync(tenant, new RepairListFilter
        {
            FromUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),

            // بداية اليوم اللي بعده — مش نهاية اليوم الأخير.
            ToUtc = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc),
        });

        Assert.Equal(["RP-LATE"], rows.Select(r => r.PublicCode));
    }

    [Fact]
    public async Task Status_and_approval_and_technician_filter_independently()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-FILTER");

        var tech = new Technician
        {
            TenantId = tenant,
            DisplayName = "محمود",
            Code = "T900",
            Username = "mahmoud900",
            NormalizedUsername = "mahmoud900",
            PasswordHash = "x",
            Salt = "y",
            IsActive = true,
            CanRepair = true,
        };
        db.Technicians.Add(tech);

        var day = new DateTime(2026, 3, 4, 9, 0, 0, DateTimeKind.Utc);

        NewItem(db, tenant, device.Id, "RP-F1", day,
            status: RepairStatus.InProgress, approval: RepairApproval.Approved,
            technicianId: tech.Id);

        NewItem(db, tenant, device.Id, "RP-F2", day.AddMinutes(1),
            status: RepairStatus.New, approval: RepairApproval.Pending);

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var byStatus = await repo.ListAsync(tenant,
            new RepairListFilter { Status = RepairStatus.InProgress });

        var byApproval = await repo.ListAsync(tenant,
            new RepairListFilter { Approval = RepairApproval.Pending });

        var byTechnician = await repo.ListAsync(tenant,
            new RepairListFilter { TechnicianId = tech.Id });

        Assert.Equal(["RP-F1"], byStatus.Rows.Select(r => r.PublicCode));
        Assert.Equal(["RP-F2"], byApproval.Rows.Select(r => r.PublicCode));
        Assert.Equal(["RP-F1"], byTechnician.Rows.Select(r => r.PublicCode));
    }

    /// <summary>
    /// ⚠️ البحث بيلاقي بالكود المضبوط <b>وب</b>نص البحث — والمدير
    /// بيلزّق كود كامل في الخانة.
    /// </summary>
    [Fact]
    public async Task Search_matches_the_exact_code_and_the_normalized_text()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-SEARCH");

        var day = new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc);

        NewItem(db, tenant, device.Id, "RP-00000042", day, faultSummary: "الكيبورد");
        NewItem(db, tenant, device.Id, "RP-00000099", day.AddMinutes(1),
            faultSummary: "البطارية بتفضى");

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var byCode = await repo.ListAsync(tenant, new RepairListFilter
        {
            ExactCode = "RP-00000042",
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("RP-00000042")),
        });

        // ⚠️ «البطاريه» بالهاء — التوحيد هو اللي بيخلّيها تلاقي «البطارية».
        var byText = await repo.ListAsync(tenant, new RepairListFilter
        {
            ExactCode = "البطاريه",
            SearchPattern = SearchPattern.Contains(ArabicText.Normalize("البطاريه")),
        });

        Assert.Equal(["RP-00000042"], byCode.Rows.Select(r => r.PublicCode));
        Assert.Equal(["RP-00000099"], byText.Rows.Select(r => r.PublicCode));
    }

    // =================================================================
    //  عدّاد المحاسب والترشيح بالشركة
    // =================================================================

    /// <summary>
    /// 🔴 <b>العدّاد برّه الفلاتر وبرّه التصفيح.</b>
    ///
    /// <para>الفلتر هنا على «تمت الصيانة» — يعني الصفوف المعروضة
    /// مافيهاش ولا أمر مستني موافقة. والعدّاد لازم يفضل بيقول
    /// ٣.</para>
    /// </summary>
    [Fact]
    public async Task Awaiting_approval_is_counted_outside_every_filter()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-AWAIT");

        var day = new DateTime(2026, 3, 7, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 3; i++)
            NewItem(db, tenant, device.Id, $"RP-AW{i}", day.AddMinutes(i));

        NewItem(db, tenant, device.Id, "RP-DONE", day.AddHours(1),
            status: RepairStatus.Completed, approval: RepairApproval.Approved);

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var (rows, _) = await repo.ListAsync(tenant,
            new RepairListFilter { Status = RepairStatus.Completed });

        int awaiting = await repo.CountAwaitingApprovalAsync(tenant);

        Assert.Equal(["RP-DONE"], rows.Select(r => r.PublicCode));
        Assert.Equal(3, awaiting);
    }

    /// <summary>
    /// 🔴 الترشيح بالشركة — أول مكان نداء ينساه بيفتح بيانات ورشة
    /// تانية.
    /// </summary>
    [Fact]
    public async Task Another_tenant_rows_are_invisible_everywhere()
    {
        using var db = fixture.Create();
        var mine = NewTenant(db);
        var theirs = NewTenant(db);

        var myDevice = NewDevice(db, mine, "D-MINE");
        var theirDevice = NewDevice(db, theirs, "D-THEIRS");

        var day = new DateTime(2026, 3, 8, 9, 0, 0, DateTimeKind.Utc);

        NewItem(db, mine, myDevice.Id, "RP-MINE", day);
        NewItem(db, theirs, theirDevice.Id, "RP-THEIRS", day.AddMinutes(1));

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var (rows, total) = await repo.ListAsync(mine, new RepairListFilter());
        int awaiting = await repo.CountAwaitingApprovalAsync(mine);
        var forTheirDevice = await repo.ListForDeviceAsync(mine, theirDevice.Id);

        Assert.Equal(["RP-MINE"], rows.Select(r => r.PublicCode));
        Assert.Equal(1, total);
        Assert.Equal(1, awaiting);

        // ⚠️ جهاز شركة تانية بيرجّع قايمة فاضية — مش أوامرهم.
        Assert.Empty(forTheirDevice);
        Assert.False(await repo.DeviceExistsAsync(mine, theirDevice.Id));
    }

    // =================================================================
    //  أعمدة الصف
    // =================================================================

    /// <summary>
    /// 🔴 العدد من SQL — صفحة ٢٠٠ أمر مالهاش لازمة تسحب كل عطل وكل
    /// قطعة عشان نعدّهم.
    /// </summary>
    [Fact]
    public async Task Row_carries_issue_and_part_counts_and_the_location_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        var location = new Location { TenantId = tenant, Name = "الدور التاني", Code = "L2" };
        db.Locations.Add(location);

        var device = NewDevice(db, tenant, "D-COUNTS");
        device.CurrentLocationId = location.Id;
        device.CommercialModelName = "ProBook 6470b";

        var item = NewItem(db, tenant, device.Id, "RP-COUNTS",
            new DateTime(2026, 3, 9, 9, 0, 0, DateTimeKind.Utc));

        db.RepairWorkItemIssues.Add(new RepairWorkItemIssue
        {
            TenantId = tenant, WorkItemId = item.Id,
            IssueCode = "SCREEN", IssueTitleSnapshot = "شاشة", Category = "display",
        });

        db.RepairParts.Add(new RepairPart
        {
            TenantId = tenant, WorkItemId = item.Id, Name = "شاشة", Quantity = 1,
        });

        db.RepairParts.Add(new RepairPart
        {
            TenantId = tenant, WorkItemId = item.Id, Name = "كابل", Quantity = 2,
        });

        await db.SaveChangesAsync();

        var (rows, _) = await new RepairRepository(db).ListAsync(tenant, new RepairListFilter());

        var row = Assert.Single(rows);

        Assert.Equal(1, row.IssueCount);
        Assert.Equal(2, row.PartCount);
        Assert.Equal("الدور التاني", row.LocationName);
        Assert.Equal("ProBook 6470b", row.CommercialModelName);
        Assert.Equal("D-COUNTS", row.DeviceCode);
    }

    /// <summary>
    /// ⚠️ اللاب اللي مش في مكان مسجّل لازم يرجّع اسم فاضي، مش يرمي.
    /// </summary>
    [Fact]
    public async Task A_device_with_no_location_yields_an_empty_name()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-NOLOC");

        NewItem(db, tenant, device.Id, "RP-NOLOC",
            new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        var (rows, _) = await new RepairRepository(db).ListAsync(tenant, new RepairListFilter());

        var row = Assert.Single(rows);

        /*
          🔴 <b>الفحص ده لقط باج حقيقي.</b>

          الوصلة الخارجية بترجّع <c>null</c>، والعمود في العقد
          <c>string</c> غير قابل للعدم — فالـ<c>null</c> كان بيتحشر
          جوّاه ويوصل للداش بورد. و<c>Assert.Equal("")</c> هي اللي
          فرّقت، لأن <c>Assert.Empty</c> كانت هتعدّي على
          <c>null</c>... لأ، كانت هترمي؛ بس
          <c>string.IsNullOrEmpty</c> كانت هتعدّي.
        */
        Assert.Equal("", row.LocationName);

        // ⚠️ ونفس الحكاية في اسم الفني: أمر من غير فني.
        Assert.Equal("", row.AssignedTechnicianName);
    }

    [Fact]
    public async Task Device_tab_lists_every_order_newest_first_without_paging()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-TAB");
        var other = NewDevice(db, tenant, "D-OTHER");

        var day = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);

        NewItem(db, tenant, device.Id, "RP-TAB1", day);
        NewItem(db, tenant, device.Id, "RP-TAB2", day.AddHours(2));
        NewItem(db, tenant, other.Id, "RP-OTHER", day.AddHours(3));

        await db.SaveChangesAsync();

        var rows = await new RepairRepository(db).ListForDeviceAsync(tenant, device.Id);

        Assert.Equal(["RP-TAB2", "RP-TAB1"], rows.Select(r => r.PublicCode));
    }

    // =================================================================
    //  التفاصيل
    // =================================================================

    [Fact]
    public async Task Detail_loads_issues_and_parts_and_the_workflow_trail()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);
        var device = NewDevice(db, tenant, "D-DETAIL");

        var item = NewItem(db, tenant, device.Id, "RP-DETAIL",
            new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc));

        db.RepairWorkItemIssues.Add(new RepairWorkItemIssue
        {
            TenantId = tenant, WorkItemId = item.Id,
            IssueCode = "BATT", IssueTitleSnapshot = "بطارية", Category = "power",
        });

        db.RepairParts.Add(new RepairPart
        {
            TenantId = tenant, WorkItemId = item.Id, Name = "بطارية", Quantity = 1,
        });

        // ⚠️ الحركة المتأخرة بتتسجّل بعدين وبتبقى `Id` أكبر، بس
        // حدوثها أقدم — والترتيب لازم يبقى بالحدوث.
        db.DeviceWorkflowEvents.Add(new DeviceWorkflowEvent
        {
            TenantId = tenant, DeviceId = device.Id, RepairWorkItemId = item.Id,
            EventType = DeviceWorkflowEventType.RepairStarted,
            ActorName = "محمود", Reason = "بدأ",
            OccurredAtUtc = new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc),
        });

        db.DeviceWorkflowEvents.Add(new DeviceWorkflowEvent
        {
            TenantId = tenant, DeviceId = device.Id, RepairWorkItemId = item.Id,
            EventType = DeviceWorkflowEventType.SentToRepair,
            ActorName = "كريم", Reason = "تحويل",
            OccurredAtUtc = new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc),
        });

        await db.SaveChangesAsync();

        var repo = new RepairRepository(db);

        var detail = await repo.FindDetailAsync(tenant, item.Id);
        var trail = await repo.ListWorkflowAsync(tenant, item.Id);

        Assert.NotNull(detail);
        Assert.Single(detail.Issues);
        Assert.Single(detail.Parts);

        Assert.Equal(
            [DeviceWorkflowEventType.SentToRepair, DeviceWorkflowEventType.RepairStarted],
            trail.Select(e => e.EventType));
    }

    [Fact]
    public async Task Device_facts_are_null_when_the_device_is_gone()
    {
        using var db = fixture.Create();
        var tenant = NewTenant(db);

        Assert.Null(await new RepairRepository(db).DeviceFactsAsync(tenant, Guid.NewGuid()));
    }
}
