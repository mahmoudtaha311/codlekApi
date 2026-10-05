using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Features.Reports;
using Codlek.Application.Features.Reports.DeleteReport;
using Codlek.Application.Features.Reports.GetReportEdits;
using Codlek.Application.Features.Reports.RestoreReport;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using Codlek.Infrastructure.Data;
using Codlek.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codlek.Tests;

/// <summary>قاعدة فحوص مسح الفحص واسترجاعه وتعديلاته.</summary>
public sealed class ReportAdminDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_report_admin_test";
}

/// <summary>
/// مسح الفحص واسترجاعه وتعديلاته — <b>على قاعدة حقيقية</b>.
///
/// <para>🔴 <b>أهم حاجة هنا:</b> الممسوح بيخرج من العدّ والمسترجع
/// بيرجع له — ودي مش حاجة المعالج بيعملها، دي فلاتر <c>!IsDeleted</c>
/// في كل قراية عدّ. فالفحص لازم يعدّي على القرايات الحقيقية.</para>
///
/// <para>⚠️ وكل خطوة بـ<c>DbContext</c> جديد — الكيان اللي في الذاكرة
/// ممكن يبان متغيّر وهو ماتحفظش.</para>
/// </summary>
public class ReportAdminDbTests(ReportAdminDbFixture fixture)
    : IClassFixture<ReportAdminDbFixture>
{
    private sealed class Me(Guid tenantId, UserRole role) : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid TenantId => tenantId;
        public string DisplayName => "صاحب الورشة";
        public string Code => "O001";
        public UserRole Role => role;
        public bool IsAuthenticated => true;
    }

    private async Task<Guid> NewTenantAsync()
    {
        await using var db = fixture.Create();
        var tenant = new Tenant { Name = "ورشة " + Guid.NewGuid().ToString("N")[..6] };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task<Report> NewReportAsync(
        Guid tenantId, string deviceCode = "LAP-0042", string rawJson = "",
        bool deleted = false)
    {
        await using var db = fixture.Create();

        // ⚠️ تاريخ قديم ثابت — مايدخلش في فترة أي فحص تاني على نفس القاعدة.
        var at = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        var row = new Report
        {
            TenantId = tenantId,
            DeviceCode = deviceCode,
            SearchText = ArabicText.Combine(deviceCode, "HP", "840"),
            StartedAtUtc = at,
            ReceivedAtUtc = at,
            TechnicianCode = "T001",
            TechnicianName = "أحمد الفني",
            Manufacturer = "HP",
            Model = "840",
            RawJson = rawJson,
            IsDeleted = deleted,
            DeletedReason = deleted ? "اتفحص بالغلط" : "",
            DeletedByName = deleted ? "مدير المخزن" : "",
            DeletedAtUtc = deleted ? at.AddHours(1) : null,
        };

        db.Reports.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static DeleteReportCommandHandler Delete(AppDbContext db, ICurrentUser me) =>
        new(new ReportRepository(db), new AuditTrail(db, me), new UnitOfWork(db), me);

    private static RestoreReportCommandHandler Restore(AppDbContext db, ICurrentUser me) =>
        new(new ReportRepository(db), new AuditTrail(db, me), new UnitOfWork(db), me);

    private async Task<int> CountedAsync(Guid tenantId)
    {
        await using var db = fixture.Create();
        var (kpis, _) = await new AnalyticsRepository(db)
            .KpisAsync(tenantId, null, AnalyticsPeriod.Everything(), includeStations: false);
        return kpis.Reports;
    }

    private async Task<int> ListedAsync(Guid tenantId)
    {
        await using var db = fixture.Create();
        var (_, total) = await new ReportRepository(db).ListAsync(
            tenantId,
            new ReportListFilter
            {
                Page = 1,
                PageSize = 50,
            });
        return total;
    }

    // =================================================================
    //  المسح والاسترجاع من أولهم لآخرهم
    // =================================================================

    /// <summary>
    /// 🔴 <b>الممسوح بيخرج من العدّ والقايمة، والمسترجع بيرجع — وصفحته
    /// بتفتح في الحالتين.</b>
    /// </summary>
    [Fact]
    public async Task Delete_drops_the_report_out_of_the_counts_and_restore_brings_it_back()
    {
        var tenant = await NewTenantAsync();
        var row = await NewReportAsync(tenant);
        await NewReportAsync(tenant, deviceCode: "LAP-0043");

        Assert.Equal(2, await CountedAsync(tenant));
        Assert.Equal(2, await ListedAsync(tenant));

        await using (var db = fixture.Create())
        {
            var result = await Delete(db, new Me(tenant, UserRole.Manager))
                .Handle(new DeleteReportCommand(row.Id, "الفني اتسرّع"), default);

            Assert.True(result.IsSuccess);
        }

        Assert.Equal(1, await CountedAsync(tenant));
        Assert.Equal(1, await ListedAsync(tenant));

        await using (var db = fixture.Create())
        {
            var analytics = new AnalyticsRepository(db);
            Assert.Equal(1, await analytics.DeletedCountAsync(tenant, AnalyticsPeriod.Everything()));

            // ⚠️ الصفحة بتفتح — ومعاها السبب.
            var page = await new ReportRepository(db).FindDetailAsync(tenant, row.Id);
            Assert.NotNull(page);
            Assert.True(page.IsDeleted);
            Assert.Equal("الفني اتسرّع", page.DeletedReason);
            Assert.Equal("صاحب الورشة", page.DeletedByName);
            Assert.NotNull(page.DeletedAtUtc);
        }

        await using (var db = fixture.Create())
        {
            var result = await Restore(db, new Me(tenant, UserRole.Owner))
                .Handle(new RestoreReportCommand(row.Id, "اتمسح بالغلط"), default);

            Assert.True(result.IsSuccess);
        }

        Assert.Equal(2, await CountedAsync(tenant));
        Assert.Equal(2, await ListedAsync(tenant));

        await using (var db = fixture.Create())
        {
            var saved = await db.Reports.AsNoTracking().SingleAsync(r => r.Id == row.Id);

            Assert.False(saved.IsDeleted);
            Assert.Equal("اتمسح بالغلط", saved.RestoredReason);
            Assert.Equal("صاحب الورشة", saved.RestoredByName);
            Assert.NotNull(saved.RestoredAtUtc);

            // 🔴 بيانات المسح فضلت.
            Assert.Equal("الفني اتسرّع", saved.DeletedReason);
            Assert.NotNull(saved.DeletedAtUtc);
        }
    }

    /// <summary>
    /// ⚠️ <b>سطر السجل بيتحفظ في نفس الحفظة — وبشركة اللي عمل.</b>
    /// </summary>
    [Fact]
    public async Task The_audit_lines_land_with_the_change()
    {
        var tenant = await NewTenantAsync();
        var row = await NewReportAsync(tenant, deviceCode: "LAP-0777");

        await using (var db = fixture.Create())
            await Delete(db, new Me(tenant, UserRole.FloorManager))
                .Handle(new DeleteReportCommand(row.Id, "اتفحص مرتين"), default);

        await using (var db = fixture.Create())
            await Restore(db, new Me(tenant, UserRole.Owner))
                .Handle(new RestoreReportCommand(row.Id, null), default);

        await using var read = fixture.Create();

        var lines = await read.AuditEvents.AsNoTracking()
            .Where(a => a.EntityId == row.Id)
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(
            [AuditActions.ReportDeleted, AuditActions.ReportRestored],
            lines.Select(a => a.Action));

        Assert.All(lines, a =>
        {
            Assert.Equal(tenant, a.TenantId);
            Assert.Equal("Report", a.EntityType);
            Assert.Equal("LAP-0777", a.EntityCode);
        });

        Assert.Equal("فحص LAP-0777 اتمسح — اتفحص مرتين", lines[0].Summary);
        Assert.Equal("فحص LAP-0777 رجع للحساب", lines[1].Summary);
    }

    /// <summary>
    /// 🔴 <b>الرفض مابيحفظش ومابيسجّلش.</b>
    /// </summary>
    [Fact]
    public async Task A_refused_delete_writes_nothing()
    {
        var tenant = await NewTenantAsync();
        var row = await NewReportAsync(tenant);

        await using (var db = fixture.Create())
        {
            var result = await Delete(db, new Me(tenant, UserRole.Manager))
                .Handle(new DeleteReportCommand(row.Id, "قصير"), default);

            Assert.Equal(ReportErrors.DeleteReasonRequired, result.Error);
        }

        await using var read = fixture.Create();

        Assert.False((await read.Reports.AsNoTracking().SingleAsync(r => r.Id == row.Id)).IsDeleted);
        Assert.False(await read.AuditEvents.AnyAsync(a => a.EntityId == row.Id));
    }

    /// <summary>
    /// 🔴 <b>فحص شركة تانية مابيتلمسش — <c>404</c> والصف زي ما هو.</b>
    /// </summary>
    [Fact]
    public async Task Another_tenants_report_cannot_be_deleted_or_restored()
    {
        var mine = await NewTenantAsync();
        var theirs = await NewTenantAsync();
        var live = await NewReportAsync(theirs);
        var gone = await NewReportAsync(theirs, deviceCode: "LAP-0099", deleted: true);

        await using (var db = fixture.Create())
        {
            var deleted = await Delete(db, new Me(mine, UserRole.Owner))
                .Handle(new DeleteReportCommand(live.Id, "سبب طويل كفاية"), default);

            var restored = await Restore(db, new Me(mine, UserRole.Owner))
                .Handle(new RestoreReportCommand(gone.Id, null), default);

            Assert.Equal(ReportErrors.NotFound, deleted.Error);
            Assert.Equal(ReportErrors.NotFound, restored.Error);
        }

        await using var read = fixture.Create();

        Assert.False((await read.Reports.AsNoTracking().SingleAsync(r => r.Id == live.Id)).IsDeleted);
        Assert.True((await read.Reports.AsNoTracking().SingleAsync(r => r.Id == gone.Id)).IsDeleted);
        Assert.False(await read.AuditEvents.AnyAsync(a => a.EntityId == live.Id || a.EntityId == gone.Id));
    }

    // =================================================================
    //  القرايات
    // =================================================================

    [Fact]
    public async Task The_update_read_is_tenant_scoped_and_sees_deleted_rows()
    {
        var mine = await NewTenantAsync();
        var theirs = await NewTenantAsync();
        var gone = await NewReportAsync(mine, deleted: true);
        var foreign = await NewReportAsync(theirs);

        await using var db = fixture.Create();
        var repo = new ReportRepository(db);

        var found = await repo.FindForUpdateAsync(mine, gone.Id);
        Assert.NotNull(found);
        Assert.True(found.IsDeleted);

        // ⚠️ متتبَّع — المعالج بيعدّل عليه ويحفظ.
        Assert.Equal(EntityState.Unchanged, db.Entry(found).State);

        Assert.Null(await repo.FindForUpdateAsync(mine, foreign.Id));

        Assert.True(await repo.ExistsAsync(mine, gone.Id));
        Assert.False(await repo.ExistsAsync(mine, foreign.Id));
        Assert.False(await repo.ExistsAsync(mine, Guid.NewGuid()));
    }

    /// <summary>
    /// 🔴 <b>جدول التعديلات مالوش عمود شركة — والشرط بيعدّي على الفحص
    /// جوّه SQL.</b>
    /// </summary>
    [Fact]
    public async Task Edits_are_read_newest_first_and_only_through_my_tenant()
    {
        var mine = await NewTenantAsync();
        var theirs = await NewTenantAsync();
        var row = await NewReportAsync(mine);
        var other = await NewReportAsync(mine, deviceCode: "LAP-0050");
        var foreign = await NewReportAsync(theirs);
        var t0 = new DateTime(2025, 1, 11, 9, 0, 0, DateTimeKind.Utc);

        await using (var db = fixture.Create())
        {
            db.Edits.AddRange(
                new ReportEdit
                {
                    ReportId = row.Id, AtUtc = t0, ByName = "أحمد",
                    Field = "الشاشة", OldValue = "A", NewValue = "B", Reason = "غلطة",
                },
                new ReportEdit
                {
                    ReportId = row.Id, AtUtc = t0.AddHours(2), ByName = "سامي",
                    Field = "البطارية", OldValue = "80", NewValue = "60", Reason = "اتقاست",
                },
                new ReportEdit { ReportId = other.Id, AtUtc = t0, Field = "فحص تاني" },
                new ReportEdit { ReportId = foreign.Id, AtUtc = t0, Field = "سرّي" });

            await db.SaveChangesAsync();
        }

        await using var read = fixture.Create();
        var repo = new ReportRepository(read);

        var edits = await repo.EditsAsync(mine, row.Id);

        Assert.Equal(["البطارية", "الشاشة"], edits.Select(e => e.Field));
        Assert.Equal("60", edits[0].NewValue);
        Assert.Equal("سامي", edits[0].ByName);

        // 🔴 معرّف فحص شركة تانية مع شركتي = ولا صف.
        Assert.Empty(await repo.EditsAsync(mine, foreign.Id));

        // ⚠️ ومن ناحية المعالج: ٤٠٤ مش قايمة فاضية.
        var handler = new GetReportEditsQueryHandler(repo, new Me(mine, UserRole.Manager));
        var result = await handler.Handle(new GetReportEditsQuery(foreign.Id), default);
        Assert.Equal(ReportErrors.NotFound, result.Error);
    }

    /// <summary>
    /// ⚠️ <b>«مين سلّم» من الحمولة الخام — نفس مفاتيح القديم بالحرف</b>
    /// (<c>CompletedByTechnicianName</c> و<c>CompletedByTechnicianCode</c>).
    /// </summary>
    [Fact]
    public async Task Completed_by_is_read_out_of_the_raw_payload()
    {
        var tenant = await NewTenantAsync();

        var handedOver = await NewReportAsync(tenant, rawJson: """
            {"ApplicationVersion":"2.4.1","CompletedByTechnicianName":"سامي","CompletedByTechnicianCode":"T007"}
            """);
        var older = await NewReportAsync(tenant, rawJson: """{"ApplicationVersion":"1.0"}""");
        var broken = await NewReportAsync(tenant, rawJson: "{ not json");

        await using var db = fixture.Create();
        var repo = new ReportRepository(db);

        var facts = await repo.VersionsAsync(tenant, handedOver.Id);
        Assert.NotNull(facts);
        Assert.Equal("سامي", facts.CompletedByName);
        Assert.Equal("T007", facts.CompletedByCode);
        Assert.Equal("2.4.1", facts.ApplicationVersion);

        var none = await repo.VersionsAsync(tenant, older.Id);
        Assert.NotNull(none);
        Assert.Null(none.CompletedByName);
        Assert.Null(none.CompletedByCode);

        // 🔴 ISJSON — صف بايظ مابيوقّعش الصفحة.
        var bad = await repo.VersionsAsync(tenant, broken.Id);
        Assert.NotNull(bad);
        Assert.Null(bad.CompletedByName);
    }

    // =================================================================
    //  المسح من الموقع لازم يعيش بعد إعادة إرسال الراكة
    // =================================================================

    private static IngestReportsCommandHandler Ingest(AppDbContext db) =>
        new(
            new ReportIngestRepository(db),
            new DeviceReference(db, NullLogger<DeviceReference>.Instance),
            new UnitOfWork(db),
            NullLogger<IngestReportsCommandHandler>.Instance);

    private async Task<IngestResult> SendAsync(Guid tenantId, LaptopReportPayload dto)
    {
        await using var db = fixture.Create();

        var result = await Ingest(db).Handle(
            new IngestReportsCommand(tenantId, Guid.NewGuid(), [dto]), default);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    /// <summary>حمولة راكة لفحص مش ممسوح — بتاريخ قديم ثابت.</summary>
    private static LaptopReportPayload RackReport() => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = new DateTime(2025, 1, 10, 9, 0, 0),
        DeviceCode = "LAP-0500",
        TechnicianName = "أحمد الفني",
        GeneralNote = "أول نسخة",
        Specs = new DeviceSpecsPayload { Manufacturer = "HP", Model = "840" },
    };

    /// <summary>
    /// 🔴 <b>المدير مسح الفحص من الموقع، والفني عدّل ملاحظة على الراكة
    /// وهي بعتته تاني — الفحص لازم يفضل ممسوح بسبب المدير.</b>
    ///
    /// <para>الراكة لسه شايلة الفحص «مش ممسوح» ومن غير أي وقت مسح.
    /// القديم كان بينسخ ده فوق الصف، فالفحص كان بيرجع للعدّ في صمت
    /// وسبب المدير بيتمسح.</para>
    ///
    /// <para>⚠️ <b>وباقي الفحص بيتحدّث عادي</b> — الملاحظة والتعديلات
    /// اللي جاية من الراكة بتنزل.</para>
    /// </summary>
    [Fact]
    public async Task A_website_delete_survives_a_changed_resend_from_the_rack()
    {
        var tenant = await NewTenantAsync();
        var dto = RackReport();

        Assert.Equal(1, (await SendAsync(tenant, dto)).Added);

        await using (var db = fixture.Create())
        {
            var deleted = await Delete(db, new Me(tenant, UserRole.Manager))
                .Handle(new DeleteReportCommand(dto.Id, "الفحص اتعمل على لاب غلط"), default);

            Assert.True(deleted.IsSuccess);
        }

        // ⚠️ نفس الحمولة بالظبط ← مابتتلمسش خالص (المسح أصلاً فاضل).
        Assert.Equal(1, (await SendAsync(tenant, dto)).Unchanged);

        dto.GeneralNote = "الفني عدّل الملاحظة";
        dto.Edits =
        [
            new EditEntryPayload
            {
                AtUtc = new DateTime(2025, 1, 11, 9, 0, 0),
                ByName = "أحمد الفني",
                Field = "GeneralNote",
                OldValue = "أول نسخة",
                NewValue = "الفني عدّل الملاحظة",
                Reason = "تصحيح",
            },
        ];

        Assert.Equal(1, (await SendAsync(tenant, dto)).Updated);

        await using var read = fixture.Create();
        var saved = await read.Reports.AsNoTracking().SingleAsync(r => r.Id == dto.Id);

        Assert.True(saved.IsDeleted);
        Assert.Equal("الفحص اتعمل على لاب غلط", saved.DeletedReason);
        Assert.Equal("صاحب الورشة", saved.DeletedByName);
        Assert.NotNull(saved.DeletedAtUtc);

        // باقي الفحص اتحدّث.
        Assert.Equal("الفني عدّل الملاحظة", saved.GeneralNote);

        var edit = Assert.Single(
            await read.Edits.AsNoTracking().Where(e => e.ReportId == dto.Id).ToListAsync());

        Assert.Equal("GeneralNote", edit.Field);
        Assert.Equal("الفني عدّل الملاحظة", edit.NewValue);

        Assert.Equal(0, await CountedAsync(tenant));
    }

    /// <summary>
    /// ⚠️ <b>والراكة اللي قرارها أحدث بتكسب.</b> المالك رجّع الفحص من
    /// الموقع، وبعدها الفني مسحه على الراكة — المسح ده أحدث فبينزل
    /// بحالته وسببه كاملين.
    /// </summary>
    [Fact]
    public async Task A_later_delete_on_the_rack_still_wins()
    {
        var tenant = await NewTenantAsync();
        var dto = RackReport();

        await SendAsync(tenant, dto);

        await using (var db = fixture.Create())
        {
            Assert.True((await Delete(db, new Me(tenant, UserRole.Manager))
                .Handle(new DeleteReportCommand(dto.Id, "اتفحص مرتين"), default)).IsSuccess);
        }

        await using (var db = fixture.Create())
        {
            Assert.True((await Restore(db, new Me(tenant, UserRole.Owner))
                .Handle(new RestoreReportCommand(dto.Id, "اتمسح بالغلط"), default)).IsSuccess);
        }

        var rackDeletedAt = DateTime.UtcNow.AddHours(1);

        dto.IsDeleted = true;
        dto.DeletedReason = "الفني مسحه على الراكة";
        dto.DeletedByName = "أحمد الفني";
        dto.DeletedAtUtc = rackDeletedAt;

        Assert.Equal(1, (await SendAsync(tenant, dto)).Updated);

        await using var read = fixture.Create();
        var saved = await read.Reports.AsNoTracking().SingleAsync(r => r.Id == dto.Id);

        Assert.True(saved.IsDeleted);
        Assert.Equal("الفني مسحه على الراكة", saved.DeletedReason);
        Assert.Equal("أحمد الفني", saved.DeletedByName);

        // 🔴 القرار كله من الراكة — حتى بيانات الاسترجاع (الراكة مابعتتهاش).
        Assert.Equal("", saved.RestoredReason);
        Assert.Null(saved.RestoredAtUtc);
    }
}
