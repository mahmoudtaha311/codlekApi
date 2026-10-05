using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Features.Reports;
using Codlek.Application.Features.Reports.DeleteReport;
using Codlek.Application.Features.Reports.GetReportDetail;
using Codlek.Application.Features.Reports.GetReportEdits;
using Codlek.Application.Features.Reports.RestoreReport;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// مسح الفحص واسترجاعه وتعديلاته بعد التسليم — <b>القواعد</b>.
///
/// <para>🔴 <b>النصوص والترتيب من صفحة الفحص في القديم بالحرف</b>
/// (<c>Pages/Reports/Details.cshtml.cs:49-99</c>): الصف بيتقرا الأول
/// (٤٠٤)، وبعدين الصلاحية، وبعدين السبب.</para>
///
/// <para>⚠️ والقراية الحقيقية (الشركة جوّه SQL، والعدّ بعد المسح) في
/// <c>ReportAdminDbTests</c>.</para>
/// </summary>
public class ReportAdminSliceTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private const string MyName = "كريم المدير";

    private sealed class FakeUser(UserRole role) : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid TenantId => Tenant;
        public string DisplayName => MyName;
        public string Code => "M001";
        public UserRole Role => role;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeReports : IReportRepository
    {
        public readonly List<Report> Rows = [];
        public readonly List<ReportEdit> Edits = [];
        public ReportVersionFacts? Versions;

        public Task<Report?> FindForUpdateAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Id == id && r.TenantId == t));

        public Task<bool> ExistsAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.Any(r => r.Id == id && r.TenantId == t));

        public Task<IReadOnlyList<ReportEdit>> EditsAsync(
            Guid t, Guid reportId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReportEdit>>(Edits
                .Where(e => e.ReportId == reportId
                            && Rows.Any(r => r.Id == reportId && r.TenantId == t))
                .OrderByDescending(e => e.AtUtc)
                .ToList());

        public Task<Report?> FindDetailAsync(Guid t, Guid id, CancellationToken ct = default) =>
            FindForUpdateAsync(t, id, ct);

        public Task<int> SnapshotComponentCountAsync(
            Guid t, Guid reportId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<ReportVersionFacts?> VersionsAsync(
            Guid t, Guid reportId, CancellationToken ct = default) =>
            Task.FromResult(Versions);

        public Task<IReadOnlyDictionary<Guid, RackLabel>> RackLabelsAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, RackLabel>>(
                new Dictionary<Guid, RackLabel>());

        public Task<IReadOnlyDictionary<Guid, string>> DeviceCodesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<(IReadOnlyList<Report> Rows, int TotalItems)> ListAsync(
            Guid t, ReportListFilter f, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Report>> ExportAsync(
            Guid t, ReportListFilter f, int cap, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAudit : IAuditTrail
    {
        public readonly List<(string Action, string EntityType, Guid? EntityId, string Code, string Summary)> Entries = [];

        public void Record(
            string action, string entityType, Guid? entityId,
            string entityCode, string summary) =>
            Entries.Add((action, entityType, entityId, entityCode, summary));

        public void RecordForRack(
            RackAuditActor actor, string action, string entityType, Guid? entityId,
            string entityCode, string summary, string dataJson = "") =>
            throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves;

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.FromResult(1);
        }

        public async Task<bool> TrySaveChangesAsync(CancellationToken ct = default)
        {
            await SaveChangesAsync(ct);
            return true;
        }

        public Task<T> InTransactionAsync<T>(
            Func<CancellationToken, Task<T>> work, CancellationToken ct = default) => work(ct);
    }

    private sealed record Harness(
        FakeReports Reports,
        FakeAudit Audit,
        FakeUnitOfWork UnitOfWork,
        DeleteReportCommandHandler Delete,
        RestoreReportCommandHandler Restore,
        GetReportEditsQueryHandler Edits,
        GetReportDetailQueryHandler Detail);

    private static Harness Build(UserRole role)
    {
        var reports = new FakeReports();
        var audit = new FakeAudit();
        var unitOfWork = new FakeUnitOfWork();
        var me = new FakeUser(role);

        return new Harness(
            reports, audit, unitOfWork,
            new DeleteReportCommandHandler(reports, audit, unitOfWork, me),
            new RestoreReportCommandHandler(reports, audit, unitOfWork, me),
            new GetReportEditsQueryHandler(reports, me),
            new GetReportDetailQueryHandler(reports, me));
    }

    private static Report Seed(Harness h, Guid? tenant = null, bool deleted = false)
    {
        var row = new Report
        {
            TenantId = tenant ?? Tenant,
            DeviceCode = "LAP-0042",
            TechnicianCode = "T001",
            TechnicianName = "أحمد الفني",
            StartedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            ReceivedAtUtc = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
            IsDeleted = deleted,
        };

        if (deleted)
        {
            row.DeletedReason = "اتفحص بالغلط";
            row.DeletedByName = "مدير المخزن";
            row.DeletedAtUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        }

        h.Reports.Rows.Add(row);
        return row;
    }

    private static void AssertNothingWritten(Harness h)
    {
        Assert.Equal(0, h.UnitOfWork.Saves);
        Assert.Empty(h.Audit.Entries);
    }

    // =================================================================
    //  المسح
    // =================================================================

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.FloorManager)]
    [InlineData(UserRole.Owner)]
    public async Task A_manager_deletes_with_a_reason(UserRole role)
    {
        var h = Build(role);
        var row = Seed(h);
        var before = DateTime.UtcNow;

        var result = await h.Delete.Handle(
            new DeleteReportCommand(row.Id, "  الفني اتسرّع والجهاز اتفحص تاني  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ReportActionResponse(row.Id, true, "الفحص اتمسح واتسجّل في المراجعة"),
            result.Value);

        Assert.True(row.IsDeleted);

        // ⚠️ السبب متقلّم — زي القديم.
        Assert.Equal("الفني اتسرّع والجهاز اتفحص تاني", row.DeletedReason);
        Assert.Equal(MyName, row.DeletedByName);
        Assert.NotNull(row.DeletedAtUtc);
        Assert.True(row.DeletedAtUtc >= before);

        Assert.Equal(1, h.UnitOfWork.Saves);
    }

    /// <summary>
    /// ⚠️ <b>سطر السجل زيادة عن القديم</b> — بنفس الحفظة، ومعاه
    /// السبب عشان المالك يقراه من السجل.
    /// </summary>
    [Fact]
    public async Task The_delete_leaves_an_audit_line_with_the_reason()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);

        await h.Delete.Handle(new DeleteReportCommand(row.Id, "اتفحص مرتين"), default);

        var entry = Assert.Single(h.Audit.Entries);
        Assert.Equal(AuditActions.ReportDeleted, entry.Action);
        Assert.Equal("Report", entry.EntityType);
        Assert.Equal(row.Id, entry.EntityId);
        Assert.Equal("LAP-0042", entry.Code);
        Assert.Equal("فحص LAP-0042 اتمسح — اتفحص مرتين", entry.Summary);
    }

    /// <summary>
    /// 🔴 <b>الفني والمحاسب مالهمش يمسحوا.</b> السياسة على المسار
    /// بتمنعهم قبل كده — والحارس ده بنص القديم.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Technician)]
    [InlineData(UserRole.Accountant)]
    public async Task Below_manager_cannot_delete(UserRole role)
    {
        var h = Build(role);
        var row = Seed(h);

        var result = await h.Delete.Handle(
            new DeleteReportCommand(row.Id, "سبب طويل كفاية"), default);

        Assert.Equal(ReportErrors.DeleteForbidden, result.Error);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.Equal("المسح من صلاحية مدير المخزن وفوق", result.Error.Description);
        Assert.False(row.IsDeleted);
        AssertNothingWritten(h);
    }

    /// <summary>
    /// 🔴 <b>فحص شركة تانية = <c>404</c>، مش <c>403</c></b> — حتى
    /// للمالك.
    /// </summary>
    [Fact]
    public async Task Another_tenants_report_is_not_found()
    {
        var h = Build(UserRole.Owner);
        var theirs = Seed(h, tenant: Other);

        var result = await h.Delete.Handle(
            new DeleteReportCommand(theirs.Id, "سبب طويل كفاية"), default);

        Assert.Equal(ReportErrors.NotFound, result.Error);
        Assert.Equal(404, result.Error.StatusCode);
        Assert.False(theirs.IsDeleted);
        AssertNothingWritten(h);
    }

    /// <summary>
    /// ⚠️ <b>المعرّف المش موجود بياخد <c>404</c> حتى لو السبب
    /// فاضي</b> — القديم بيقرا الصف قبل ما يبص على السبب.
    /// </summary>
    [Fact]
    public async Task A_missing_report_is_404_before_the_reason_is_looked_at()
    {
        var h = Build(UserRole.Manager);

        var result = await h.Delete.Handle(new DeleteReportCommand(Guid.NewGuid(), null), default);

        Assert.Equal(ReportErrors.NotFound, result.Error);
        AssertNothingWritten(h);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("        ")]
    [InlineData("ابدأ")]
    [InlineData("   ابدأ   ")]
    public async Task The_reason_needs_five_letters_after_trimming(string? reason)
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);

        var result = await h.Delete.Handle(new DeleteReportCommand(row.Id, reason), default);

        Assert.Equal(ReportErrors.DeleteReasonRequired, result.Error);
        Assert.Equal(400, result.Error.StatusCode);
        Assert.Equal("لازم تكتب سبب المسح (٥ حروف على الأقل)", result.Error.Description);
        Assert.False(row.IsDeleted);
        AssertNothingWritten(h);
    }

    [Fact]
    public async Task Exactly_five_letters_is_enough()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);

        var result = await h.Delete.Handle(new DeleteReportCommand(row.Id, " مكرّر "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("مكرّر", row.DeletedReason);
    }

    /// <summary>
    /// ⚠️ <b>جديد — القديم كان بيقع بخطأ عام.</b> العمود ٤٠٠ حرف، والقص
    /// في صمت بيضيّع الدليل اللي المالك بيراجعه.
    /// </summary>
    [Fact]
    public async Task A_reason_past_the_column_is_refused_not_clipped()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);

        var tooLong = await h.Delete.Handle(
            new DeleteReportCommand(row.Id, new string('س', 401)), default);

        Assert.Equal(ReportErrors.ReasonTooLong, tooLong.Error);
        Assert.Equal(400, tooLong.Error.StatusCode);
        Assert.False(row.IsDeleted);
        AssertNothingWritten(h);

        var exact = await h.Delete.Handle(
            new DeleteReportCommand(row.Id, new string('س', 400)), default);

        Assert.True(exact.IsSuccess);
        Assert.Equal(400, row.DeletedReason.Length);
    }

    /// <summary>
    /// ⚠️ <b>جديد — القديم كان بيكتب فوق مين مسح وليه.</b> مديرين
    /// فاتحين نفس الصفحة: التاني كان بيمحي دليل الأول.
    /// </summary>
    [Fact]
    public async Task A_deleted_report_cannot_be_deleted_again()
    {
        var h = Build(UserRole.Owner);
        var row = Seed(h, deleted: true);

        var result = await h.Delete.Handle(
            new DeleteReportCommand(row.Id, "سبب تاني خالص"), default);

        Assert.Equal(ReportErrors.AlreadyDeleted, result.Error);
        Assert.Equal(409, result.Error.StatusCode);

        // 🔴 الدليل الأول زي ما هو.
        Assert.Equal("اتفحص بالغلط", row.DeletedReason);
        Assert.Equal("مدير المخزن", row.DeletedByName);
        AssertNothingWritten(h);
    }

    /// <summary>
    /// ⚠️ <b>فحص اترجع قبل كده واتمسح تاني</b> — بيانات الاسترجاع
    /// بتفضل زي القديم، فتاريخه كله باين.
    /// </summary>
    [Fact]
    public async Task Deleting_keeps_an_earlier_restore_on_record()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);
        row.RestoredByName = "صاحب الورشة";
        row.RestoredReason = "اتمسح بالغلط";
        row.RestoredAtUtc = new DateTime(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc);

        await h.Delete.Handle(new DeleteReportCommand(row.Id, "المرة دي بجد"), default);

        Assert.True(row.IsDeleted);
        Assert.Equal("صاحب الورشة", row.RestoredByName);
        Assert.Equal("اتمسح بالغلط", row.RestoredReason);
        Assert.NotNull(row.RestoredAtUtc);
    }

    // =================================================================
    //  الاسترجاع
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاسترجاع بيرجّع الفحص للعدّ — وبيانات المسح بتفضل.</b>
    /// </summary>
    [Fact]
    public async Task The_owner_restores_and_the_delete_stays_on_record()
    {
        var h = Build(UserRole.Owner);
        var row = Seed(h, deleted: true);
        var before = DateTime.UtcNow;

        var result = await h.Restore.Handle(
            new RestoreReportCommand(row.Id, "  اتمسح بالغلط "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ReportActionResponse(row.Id, false, "الفحص رجع للحساب"), result.Value);

        Assert.False(row.IsDeleted);
        Assert.Equal(MyName, row.RestoredByName);
        Assert.Equal("اتمسح بالغلط", row.RestoredReason);
        Assert.True(row.RestoredAtUtc >= before);

        Assert.Equal("اتفحص بالغلط", row.DeletedReason);
        Assert.Equal("مدير المخزن", row.DeletedByName);
        Assert.NotNull(row.DeletedAtUtc);

        Assert.Equal(1, h.UnitOfWork.Saves);

        var entry = Assert.Single(h.Audit.Entries);
        Assert.Equal(AuditActions.ReportRestored, entry.Action);
        Assert.Equal("Report", entry.EntityType);
        Assert.Equal("فحص LAP-0042 رجع للحساب — اتمسح بالغلط", entry.Summary);
    }

    /// <summary>⚠️ السبب اختياري ومالوش حد أدنى — زي القديم.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("آه")]
    public async Task The_restore_reason_is_optional(string? reason)
    {
        var h = Build(UserRole.Owner);
        var row = Seed(h, deleted: true);

        var result = await h.Restore.Handle(new RestoreReportCommand(row.Id, reason), default);

        Assert.True(result.IsSuccess);
        Assert.False(row.IsDeleted);
        Assert.Equal((reason ?? "").Trim(), row.RestoredReason);

        var entry = Assert.Single(h.Audit.Entries);
        Assert.StartsWith("فحص LAP-0042 رجع للحساب", entry.Summary);
    }

    /// <summary>
    /// 🔴 <b>المالك بس.</b> مدير المخزن هو اللي بيمسح — لو هو اللي
    /// بيرجّع، المراجعة تبقى بلا معنى.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.FloorManager)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.Technician)]
    public async Task Only_the_owner_restores(UserRole role)
    {
        var h = Build(role);
        var row = Seed(h, deleted: true);

        var result = await h.Restore.Handle(new RestoreReportCommand(row.Id, "رجّعه"), default);

        Assert.Equal(ReportErrors.RestoreForbidden, result.Error);
        Assert.Equal(403, result.Error.StatusCode);
        Assert.Equal("الاسترجاع من صلاحية المدير العام", result.Error.Description);
        Assert.True(row.IsDeleted);
        AssertNothingWritten(h);
    }

    [Fact]
    public async Task Restoring_another_tenants_report_is_not_found()
    {
        var h = Build(UserRole.Owner);
        var theirs = Seed(h, tenant: Other, deleted: true);

        var result = await h.Restore.Handle(new RestoreReportCommand(theirs.Id, null), default);

        Assert.Equal(ReportErrors.NotFound, result.Error);
        Assert.True(theirs.IsDeleted);
        AssertNothingWritten(h);
    }

    /// <summary>
    /// ⚠️ <b>جديد — القديم كان بيكتب «اترجع» على فحص عمره ما
    /// اتمسح.</b>
    /// </summary>
    [Fact]
    public async Task A_live_report_cannot_be_restored()
    {
        var h = Build(UserRole.Owner);
        var row = Seed(h);

        var result = await h.Restore.Handle(new RestoreReportCommand(row.Id, "ليه"), default);

        Assert.Equal(ReportErrors.NotDeleted, result.Error);
        Assert.Equal(409, result.Error.StatusCode);
        Assert.Equal("", row.RestoredByName);
        Assert.Null(row.RestoredAtUtc);
        AssertNothingWritten(h);
    }

    [Fact]
    public async Task A_restore_reason_past_the_column_is_refused()
    {
        var h = Build(UserRole.Owner);
        var row = Seed(h, deleted: true);

        var result = await h.Restore.Handle(
            new RestoreReportCommand(row.Id, new string('ر', 401)), default);

        Assert.Equal(ReportErrors.ReasonTooLong, result.Error);
        Assert.True(row.IsDeleted);
        AssertNothingWritten(h);
    }

    // =================================================================
    //  التعديلات بعد التسليم
    // =================================================================

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.FloorManager)]
    [InlineData(UserRole.Owner)]
    public async Task Managers_read_the_edits_newest_first(UserRole role)
    {
        var h = Build(role);
        var row = Seed(h);
        var t0 = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

        h.Reports.Edits.Add(new ReportEdit
        {
            ReportId = row.Id, AtUtc = t0, ByName = "أحمد",
            Field = "الشاشة", OldValue = "A", NewValue = "B", Reason = "غلطة",
        });
        h.Reports.Edits.Add(new ReportEdit
        {
            ReportId = row.Id, AtUtc = t0.AddHours(1), ByName = "سامي",
            Field = "البطارية", OldValue = "80", NewValue = "60", Reason = "اتقاست تاني",
        });

        var result = await h.Edits.Handle(new GetReportEditsQuery(row.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                new ReportEditItem(t0.AddHours(1), "سامي", "البطارية", "80", "60", "اتقاست تاني"),
                new ReportEditItem(t0, "أحمد", "الشاشة", "A", "B", "غلطة"),
            ],
            result.Value);
    }

    /// <summary>⚠️ مفيش تعديلات = قايمة فاضية، مش خطأ — والشاشة بتخفي القسم.</summary>
    [Fact]
    public async Task No_edits_is_an_empty_list()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);

        var result = await h.Edits.Handle(new GetReportEditsQuery(row.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    /// <summary>⚠️ والفحص الممسوح تعديلاته بتتقري — هي جزء من الدليل.</summary>
    [Fact]
    public async Task A_deleted_reports_edits_are_still_readable()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h, deleted: true);
        h.Reports.Edits.Add(new ReportEdit { ReportId = row.Id, Field = "الشاشة" });

        var result = await h.Edits.Handle(new GetReportEditsQuery(row.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task Another_tenants_edits_are_not_found()
    {
        var h = Build(UserRole.Owner);
        var theirs = Seed(h, tenant: Other);
        h.Reports.Edits.Add(new ReportEdit { ReportId = theirs.Id, Field = "سرّي" });

        var result = await h.Edits.Handle(new GetReportEditsQuery(theirs.Id), default);

        Assert.Equal(ReportErrors.NotFound, result.Error);
    }

    [Theory]
    [InlineData(UserRole.Technician)]
    [InlineData(UserRole.Accountant)]
    public async Task Below_manager_cannot_read_the_edits(UserRole role)
    {
        var h = Build(role);
        var row = Seed(h);

        var result = await h.Edits.Handle(new GetReportEditsQuery(row.Id), default);

        Assert.Equal(ReportErrors.EditsForbidden, result.Error);
        Assert.Equal(403, result.Error.StatusCode);
    }

    // =================================================================
    //  صفحة الفحص — مين سلّم، وبيانات الاسترجاع
    // =================================================================

    [Fact]
    public async Task The_detail_carries_who_handed_over_and_the_restore()
    {
        var h = Build(UserRole.Owner);
        var row = Seed(h, deleted: true);
        row.IsDeleted = false;
        row.RestoredByName = "صاحب الورشة";
        row.RestoredReason = "اتمسح بالغلط";
        row.RestoredAtUtc = new DateTime(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc);

        h.Reports.Versions = new ReportVersionFacts("2.4.1", "7", "سامي الوردية التانية", "T007");

        var result = await h.Detail.Handle(new GetReportDetailQuery(row.Id), default);

        Assert.True(result.IsSuccess);
        var d = result.Value!;

        Assert.Equal("سامي الوردية التانية", d.CompletedByName);
        Assert.Equal("T007", d.CompletedByCode);
        Assert.Equal("أحمد الفني", d.TechnicianName);

        Assert.False(d.IsDeleted);
        Assert.Equal("اتفحص بالغلط", d.DeletedReason);
        Assert.Equal("مدير المخزن", d.DeletedByName);
        Assert.Equal("صاحب الورشة", d.RestoredByName);
        Assert.Equal("اتمسح بالغلط", d.RestoredReason);
        Assert.Equal(row.RestoredAtUtc, d.RestoredAtUtc);
    }

    /// <summary>
    /// ⚠️ <b>«مين سلّم» الناقصة فاضية، مش «غير متاح»</b> — القديم كان
    /// بيخفي السطر لما تبقى فاضية، و«غير متاح» كانت هتطلّعه على كل فحص
    /// قديم. والنسخ الناقصة بتفضل «غير متاح» زي ما هي.
    /// </summary>
    [Fact]
    public async Task A_missing_completed_by_is_empty_not_unavailable()
    {
        var h = Build(UserRole.Manager);
        var row = Seed(h);

        h.Reports.Versions = null;
        var none = (await h.Detail.Handle(new GetReportDetailQuery(row.Id), default)).Value!;

        Assert.Equal("", none.CompletedByName);
        Assert.Equal("", none.CompletedByCode);
        Assert.Equal("غير متاح", none.ApplicationVersion);
        Assert.Equal("", none.RestoredByName);
        Assert.Null(none.RestoredAtUtc);

        h.Reports.Versions = new ReportVersionFacts("2.4.1", null, null, null);
        var partial = (await h.Detail.Handle(new GetReportDetailQuery(row.Id), default)).Value!;

        Assert.Equal("", partial.CompletedByName);
        Assert.Equal("2.4.1", partial.ApplicationVersion);
    }
}
