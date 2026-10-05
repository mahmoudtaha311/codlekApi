using Codlek.Application.Features.Devices.GetDevices;
using Codlek.Application.Features.Export.ExportDevices;
using Codlek.Application.Features.Export.ExportReports;
using Codlek.Application.Features.Reports.GetReports;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// <b>الملف لازم يطلع زي الشاشة.</b>
///
/// <para>🔴 <b>والفحوص دي بتقيس الحاجة الوحيدة اللي بتكسر القاعدة
/// دي فعلاً: قايمة المعاملات عند النداء.</b> بنّاء الفلتر مشترك
/// خلاص، فالخطر مش فيه — الخطر إن نقطة التصدير تنسى تمرّر معامل.
/// وده اللي حصل في القديم بالحرف: تلات فلاتر (التحذيرات والتسليم
/// والجهة) كانوا بيوصلوا القايمة ومابيوصلوش التصدير، فالمدير يفلتر
/// على «مخزن الجاهز» ويصدّر فيطلعله <b>كل</b> الأجهزة.</para>
///
/// <para>⚠️ <b>والمقارنة على الفلتر <u>كله</u> مش حقل حقل.</b>
/// <c>record</c> بيقارن بكل خصائصه — فحقل جديد بينضاف للفلتر
/// وبيتحط في نقطة واحدة بس بيوقّع الفحص ده من غير ما حد يفتكر
/// يعدّله.</para>
/// </summary>
public class ExportFilterParityTests
{
    /// <summary>
    /// ⚠️ مستودع بيحفظ الفلتر اللي وصله ويرجّع فاضي — الفحص بيقيس
    /// <b>اللي اتبعت</b> مش اللي رجع.
    /// </summary>
    private sealed class FilterSpyDeviceRepository : IDeviceRepository
    {
        public DeviceListFilter? Last;

        public Task<(IReadOnlyList<DeviceListRow> Rows, int TotalItems)> ListAsync(
            Guid t, DeviceListFilter f, CancellationToken ct = default)
        {
            Last = f;
            return Task.FromResult<(IReadOnlyList<DeviceListRow>, int)>(([], 0));
        }

        public Task<IReadOnlyList<DeviceExportRow>> ExportAsync(
            Guid t, DeviceListFilter f, int cap, CancellationToken ct = default)
        {
            Last = f;
            return Task.FromResult<IReadOnlyList<DeviceExportRow>>([]);
        }

        public Task<Device?> FindByCodeAsync(
            Guid t, string code, CancellationToken ct = default) =>
            Task.FromResult<Device?>(null);

        public Task<IReadOnlyList<DeviceCodeHit>> ResolveCodeAsync(
            Guid t, string code, string normalized, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceCodeHit>>([]);

        public Task<Device?> FindDetailAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Device?>(null);

        public Task<DeviceDetailFacts> DetailFactsAsync(
            Guid t, Device d, CancellationToken ct = default) =>
            Task.FromResult(new DeviceDetailFacts(0, 0, 0, 0, null, "", "", null, "", ""));

        public Task<DeviceTimelineCounts> TimelineCountsAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(new DeviceTimelineCounts(0, 0, 0, 0));

        public Task<IReadOnlyList<TimelineReportRow>> TimelineReportsAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineReportRow>>([]);

        public Task<IReadOnlyList<TimelineNoteRow>> TimelineNotesAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineNoteRow>>([]);

        public Task<IReadOnlyList<TimelineRepairMomentRow>> TimelineRepairMomentsAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineRepairMomentRow>>([]);

        public Task<IReadOnlyList<TimelineMovementRow>> TimelineMovementsAsync(
            Guid t, Guid id, int need, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TimelineMovementRow>>([]);

        public Task<IReadOnlyList<DeviceIdentifierRow>> IdentifiersAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceIdentifierRow>>([]);

        public Task<IReadOnlyList<DeviceNote>> NotesAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceNote>>([]);

        public void AddNote(DeviceNote note) { }

        public Task<IReadOnlyDictionary<Guid, ReportVersionFacts>> TestVersionsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ReportVersionFacts>>(
                new Dictionary<Guid, ReportVersionFacts>());

        public Task<(IReadOnlyList<DeviceTestRow> Rows, int TotalItems)> TestsAsync(
            Guid t, Guid id, int page, int size, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<DeviceTestRow>, int)>(([], 0));

        public Task<IReadOnlyDictionary<Guid, string>> LocationNamesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<IReadOnlyDictionary<Guid, TechnicianLabel>> HolderNamesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, TechnicianLabel>>(
                new Dictionary<Guid, TechnicianLabel>());

        public Task<IReadOnlyDictionary<Guid, string>> RackCodesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<IReadOnlyDictionary<Guid, string>> RackLocationsAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }

    private sealed class FilterSpyReportRepository : IReportRepository
    {
        public ReportListFilter? Last;

        public Task<(IReadOnlyList<Report> Rows, int TotalItems)> ListAsync(
            Guid t, ReportListFilter f, CancellationToken ct = default)
        {
            Last = f;
            return Task.FromResult<(IReadOnlyList<Report>, int)>(([], 0));
        }

        public Task<IReadOnlyList<Report>> ExportAsync(
            Guid t, ReportListFilter f, int cap, CancellationToken ct = default)
        {
            Last = f;
            return Task.FromResult<IReadOnlyList<Report>>([]);
        }

        public Task<Report?> FindDetailAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Report?>(null);

        public Task<int> SnapshotComponentCountAsync(
            Guid t, Guid reportId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<ReportVersionFacts?> VersionsAsync(
            Guid t, Guid reportId, CancellationToken ct = default) =>
            Task.FromResult<ReportVersionFacts?>(null);

        public Task<IReadOnlyDictionary<Guid, RackLabel>> RackLabelsAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, RackLabel>>(
                new Dictionary<Guid, RackLabel>());

        public Task<IReadOnlyDictionary<Guid, string>> DeviceCodesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }

    // =================================================================
    //  الأجهزة
    // =================================================================

    /// <summary>
    /// 🔴 <b>أربعتاشر معامل — وكلهم لازم يوصلوا التصدير.</b>
    /// </summary>
    [Fact]
    public async Task The_devices_export_sends_the_same_filter_as_the_list()
    {
        var me = new FakeCurrentUser(UserRole.Manager);
        var rack = Guid.NewGuid();
        var container = Guid.NewGuid();
        var location = Guid.NewGuid();
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);

        var listRepo = new FilterSpyDeviceRepository();
        var exportRepo = new FilterSpyDeviceRepository();

        await new GetDevicesQueryHandler(listRepo, me).Handle(
            new GetDevicesQuery(
                "بحث", "Active", "A", "failures", "T001", rack, from, to,
                "Tested", "oldest", container, "attention", "no", location, 1, 25),
            default);

        await new ExportDevicesQueryHandler(exportRepo, me).Handle(
            new ExportDevicesQuery(
                "بحث", "Active", "A", "failures", "T001", rack, from, to,
                "Tested", "oldest", container, "attention", "no", location),
            default);

        // ⚠️ الصفحة بس هي اللي بتختلف: التصدير بلا تصفيح.
        Assert.Equal(
            listRepo.Last! with { Page = 0, PageSize = 0 },
            exportRepo.Last! with { Page = 0, PageSize = 0 });
    }

    /// <summary>
    /// ⚠️ <b>وحاجز على الفحص اللي فوق:</b> لازم الفلتر يكون
    /// <u>مليان</u> فعلاً. لو كل حقوله فاضية، المقارنة بتعدّي وهي
    /// مش بتقيس حاجة — وده بالظبط اللي بيحصل لو المعاملات مااتمرّرتش.
    /// </summary>
    [Fact]
    public async Task The_captured_device_filter_is_not_empty()
    {
        var me = new FakeCurrentUser(UserRole.Manager);
        var repo = new FilterSpyDeviceRepository();

        await new ExportDevicesQueryHandler(repo, me).Handle(
            new ExportDevicesQuery(
                "بحث", "Active", "A", "failures", "T001", Guid.NewGuid(),
                new DateTime(2026, 9, 1), new DateTime(2026, 9, 30),
                "Tested", "oldest", Guid.NewGuid(), "attention", "no", Guid.NewGuid()),
            default);

        var f = repo.Last!;

        Assert.NotNull(f.SearchPattern);
        Assert.Equal(DeviceLifecycleStatus.Active, f.Status);
        Assert.Equal(DeviceIdentityConfidence.A, f.Confidence);
        Assert.Equal("T001", f.TechnicianCode);
        Assert.NotNull(f.RackId);
        Assert.NotNull(f.ContainerId);
        Assert.Equal(DeviceOperationalStage.Tested, f.Stage);
        Assert.NotNull(f.FromUtc);
        Assert.NotNull(f.ToUtc);

        /*
          🔴 **والتلاتة دول بالذات.**

          دول اللي كانوا ناقصين من تصدير القديم — فلتر أيقونة
          التحذيرات، وفلتر «اتسلّم/مااتسلّمش»، والجهة.
        */
        Assert.Equal(DeviceAttentionFlag.Attention, f.Flag);
        Assert.Equal(DeviceHandoverFilter.InWorkshop, f.Handover);
        Assert.NotNull(f.LocationId);
    }

    // =================================================================
    //  الفحوص
    // =================================================================

    [Fact]
    public async Task The_reports_export_sends_the_same_filter_as_the_list()
    {
        var me = new FakeCurrentUser(UserRole.Manager);
        var container = Guid.NewGuid();
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);

        var listRepo = new FilterSpyReportRepository();
        var exportRepo = new FilterSpyReportRepository();

        await new GetReportsQueryHandler(listRepo, me).Handle(
            new GetReportsQuery("بحث", "repair", "T001", container, from, to, 1, 25),
            default);

        await new ExportReportsQueryHandler(exportRepo, me)
            .Handle(
                new ExportReportsQuery("بحث", "repair", "T001", container, from, to),
                default);

        Assert.Equal(
            listRepo.Last! with { Page = 0, PageSize = 0 },
            exportRepo.Last! with { Page = 0, PageSize = 0 });
    }

    /// <summary>
    /// 🔴 <b>والفني بيصدّر شغله هو — مش شغل اللي بيطلبه.</b>
    ///
    /// <para>نقطة تصدير الفحوص مفتوحة لأي مستخدم داخل، والتضييق
    /// بيحصل في الفلتر. ولو الكود المبعوت غلب، الفني كان بيقدر
    /// يصدّر شغل حد تاني بتغيير رابط.</para>
    /// </summary>
    [Fact]
    public async Task A_technician_exporting_reports_cannot_ask_for_someone_else()
    {
        var me = new FakeCurrentUser(UserRole.Technician);
        var repo = new FilterSpyReportRepository();

        await new ExportReportsQueryHandler(repo, me)
            .Handle(new ExportReportsQuery(null, null, "T999", null, null, null), default);

        Assert.Equal(me.Code, repo.Last!.TechnicianCode);
    }

    /// <summary>⚠️ والمدير بيقدر يطلب أي فني.</summary>
    [Fact]
    public async Task A_manager_exporting_reports_can_ask_for_any_technician()
    {
        var me = new FakeCurrentUser(UserRole.Manager);
        var repo = new FilterSpyReportRepository();

        await new ExportReportsQueryHandler(repo, me)
            .Handle(new ExportReportsQuery(null, null, "T999", null, null, null), default);

        Assert.Equal("T999", repo.Last!.TechnicianCode);
    }
}
