using System.Reflection;
using Codlek.Api.Authorization;
using Codlek.Api.Controllers;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Features.Export;
using Codlek.Application.Features.Export.ExportProductivity;
using Codlek.Application.Features.Export.ExportRacks;
using Codlek.Application.Features.Export.ExportRepairs;
using Codlek.Application.Features.Technicians;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Export;
using Codlek.Core.Spreadsheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Codlek.Tests;

/// <summary>
/// التصدير.
///
/// <para>🔴 <b>القاعدة الحاكمة في الملف ده: الملف لازم يطلع زي
/// الشاشة.</b> الزرار في الواجهة مكتوب فوقه «اللي مفلتر على الشاشة
/// مفلتر في الإكسل»، وفي القديم ماكانش صح — تلات فلاتر كانوا
/// ناقصين من نسخة التصدير، فالمدير يفلتر على «مخزن الجاهز» ويصدّر
/// فيطلعله <b>كل</b> الأجهزة.</para>
///
/// <para>⚠️ <b>والقاعدة التانية: عدد الأعمدة = عدد القيم.</b>
/// عمود زيادة في واحد من غير التاني بيزحلق كل الشيت، والمدير بيقرا
/// «الماركة» تحت عنوان «الموديل» ومايلاحظش.</para>
/// </summary>
public class ExportSliceTests
{
    // =================================================================
    //  الأعمدة تطابق القيم
    // =================================================================

    /// <summary>
    /// 🔴 <b>كل صف لازم يطابق أعمدته في العدد.</b>
    ///
    /// <para>⚠️ والكاتب <b>مابيشتكيش</b>: هو بيكتب لحد أقصر
    /// الاتنين. يعني القيمة الزيادة بتختفي في صمت، والناقصة بتسيب
    /// عمود فاضي — والاتنين بيتقروا على إنهم بيانات.</para>
    /// </summary>
    [Fact]
    public async Task Every_racks_row_has_one_value_per_column()
    {
        var repo = new FakeRackExportRepository();
        var me = new FakeCurrentUser(UserRole.Owner);

        repo.Racks.Add(new Rack
        {
            TenantId = me.TenantId, RackCode = "RK-01", Name = "محطة",
            Location = "الورشة", Status = RackStatus.Active, AppVersion = "1.0",
            ReportsReceived = 3, RevokedReason = "",
        });

        var result = await new ExportRacksQueryHandler(repo, me)
            .Handle(new ExportRacksQuery(), default);

        var sheet = Assert.Single(result.Value.Sheets);

        Assert.All(sheet.Rows, row =>
            Assert.Equal(sheet.Columns.Length, row.Length));
    }

    [Fact]
    public async Task Every_productivity_row_has_one_value_per_column()
    {
        var repo = new FakeProductivityRepository();
        var me = new FakeCurrentUser(UserRole.Manager);

        repo.Testing.Add(new TestingProductivityFacts(
            "T001", "محمود", 4, 2, 1, 1, 0, 0, Timed: 4, 120_000, DateTime.UtcNow));

        var result = await new ExportProductivityQueryHandler(repo, me)
            .Handle(new ExportProductivityQuery(null, null, null, null, null, null), default);

        var sheet = Assert.Single(result.Value.Sheets);

        Assert.NotEmpty(sheet.Rows);
        Assert.All(sheet.Rows, row => Assert.Equal(sheet.Columns.Length, row.Length));
    }

    /// <summary>
    /// ⚠️ <b>سطر القص هو الاستثناء الوحيد المسموح.</b> عمود واحد
    /// في صف عرضه عشرة — ومقصود: التحذير بيتقرا في أول خانة.
    /// </summary>
    [Fact]
    public void The_truncation_row_is_the_only_short_row()
    {
        var rows = Enumerable.Range(0, ExportLimits.MaxRows + 1).ToList();

        var capped = ExportLimits.Capped(rows, i => new object?[] { i, i, i });

        Assert.All(capped.SkipLast(1), row => Assert.Equal(3, row.Length));
        Assert.Single(capped[^1]);
    }

    // =================================================================
    //  الملف زي الشاشة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الفلتر اللي بيوصل التصدير هو نفسه اللي بيوصل
    /// القايمة.</b>
    ///
    /// <para>والفحص ده بيبعت <b>كل</b> المعاملات للنقطتين ويقارن
    /// الكائن الناتج — فأي فلتر بينضاف لواحدة وبينسى في التانية
    /// بيوقّعه.</para>
    /// </summary>
    [Fact]
    public async Task The_repairs_export_sends_the_same_filter_as_the_list()
    {
        var me = new FakeCurrentUser(UserRole.Manager);
        var technician = Guid.NewGuid();
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);

        var listRepo = new FakeRepairRepository();
        var exportRepo = new FakeRepairRepository();

        await new Application.Features.Repairs.GetRepairs.GetRepairsQueryHandler(listRepo, me)
            .Handle(
                new Application.Features.Repairs.GetRepairs.GetRepairsQuery(
                    "بحث", "InProgress", "Pending", technician, from, to, "oldest", 1, 25),
                default);

        await new ExportRepairsQueryHandler(exportRepo, me).Handle(
            new ExportRepairsQuery("بحث", "InProgress", "Pending", technician, from, to, "oldest"),
            default);

        var list = listRepo.LastFilter!;
        var export = exportRepo.LastFilter!;

        /*
          ⚠️ **المقارنة على الفلتر كله، مش حقل حقل.**

          `record` بيقارن بكل الخصائص — فحقل جديد بينضاف للفلتر
          وبيتحط في واحدة بس بيوقّع الفحص ده من غير ما حد يفتكر
          يعدّله.

          ⚠️ والصفحة بس هي اللي بتختلف: التصدير بلا تصفيح.
        */
        Assert.Equal(list with { Page = 0, PageSize = 0 }, export with { Page = 0, PageSize = 0 });
    }

    /// <summary>
    /// 🔴 <b>والشاشة بتفلتر بالفني في المتصفح — فالملف لازم يفلتر
    /// على السيرفر.</b>
    ///
    /// <para>من غير ده المدير بيفلتر على فني واحد، يدوس «تصدير»،
    /// ويلاقي <b>كل</b> الفنيين في الملف.</para>
    /// </summary>
    [Fact]
    public async Task Exporting_one_technician_leaves_the_others_out()
    {
        var repo = new FakeProductivityRepository();
        var me = new FakeCurrentUser(UserRole.Manager);

        repo.Testing.Add(new TestingProductivityFacts(
            "T001", "محمود", 4, 4, 0, 0, 0, 0, Timed: 4, 60_000, DateTime.UtcNow));
        repo.Testing.Add(new TestingProductivityFacts(
            "T002", "كريم", 9, 9, 0, 0, 0, 0, Timed: 9, 60_000, DateTime.UtcNow));

        var result = await new ExportProductivityQueryHandler(repo, me).Handle(
            new ExportProductivityQuery(null, null, null, null, null, "T001"), default);

        var rows = Assert.Single(result.Value.Sheets).Rows.ToList();
        var row = Assert.Single(rows);

        Assert.Equal("T001", row[1]);
    }

    /// <summary>⚠️ ومن غير الفلتر، الاتنين بيطلعوا.</summary>
    [Fact]
    public async Task Exporting_without_a_technician_keeps_everyone()
    {
        var repo = new FakeProductivityRepository();
        var me = new FakeCurrentUser(UserRole.Manager);

        repo.Testing.Add(new TestingProductivityFacts(
            "T001", "محمود", 4, 4, 0, 0, 0, 0, Timed: 4, 60_000, DateTime.UtcNow));
        repo.Testing.Add(new TestingProductivityFacts(
            "T002", "كريم", 9, 9, 0, 0, 0, 0, Timed: 9, 60_000, DateTime.UtcNow));

        var result = await new ExportProductivityQueryHandler(repo, me).Handle(
            new ExportProductivityQuery(null, null, null, null, null, null), default);

        Assert.Equal(2, Assert.Single(result.Value.Sheets).Rows.Count());
    }

    /// <summary>
    /// ⚠️ <b>والترتيب نفس ترتيب الشاشة.</b> الملف اللي بيطلع بترتيب
    /// تاني بيخلّي المدير يقارن صف بصف ويلاقي فرق مالوش تفسير.
    ///
    /// <para>🔴 <b>والفحص بيعدّي على <u>كل</u> المفاتيح — ودي نسخة
    /// تانية.</b> النسخة الأولى كانت بتبعت <c>"name"</c> وبتقارن
    /// بـ<c>"name"</c>، فتثبيت المفتاح في الكود كان بيعدّي من
    /// تحتها: المعالج يتجاهل اللي المنادي بعته ويرتّب بالاسم على
    /// طول، والفحص يفضل أخضر. (اتجرّب: التحوير <b>نجا</b>.)</para>
    /// </summary>
    [Theory]
    [InlineData("name")]
    [InlineData("busiest")]
    [InlineData("code")]
    [InlineData("last")]
    [InlineData(null)]
    public async Task The_productivity_export_uses_the_screen_sort(string? sort)
    {
        var repo = new FakeProductivityRepository();
        var me = new FakeCurrentUser(UserRole.Manager);

        /*
          ⚠️ **البيانات متظبّطة عشان المفاتيح تدّي ترتيب مختلف.**

          «ياسر» أقل شغل وكوده أصغر وآخر نشاطه أقدم؛ «أحمد» العكس.
          فلو الاتنين بيطلعوا بنفس الترتيب في كل المفاتيح، الفحص
          مابيقيسش حاجة.
        */
        var old = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        repo.Testing.Add(new TestingProductivityFacts(
            "T001", "ياسر", 1, 1, 0, 0, 0, 0, Timed: 1, 60_000, old));
        repo.Testing.Add(new TestingProductivityFacts(
            "T002", "أحمد", 9, 9, 0, 0, 0, 0, Timed: 9, 60_000, recent));

        var result = await new ExportProductivityQueryHandler(repo, me).Handle(
            new ExportProductivityQuery(null, null, null, null, sort, null), default);

        var screen = TechnicianProductivity.Sorted(
            TechnicianProductivity.Rows(repo.Testing, repo.Repairs), sort);

        Assert.Equal(
            screen.Select(r => r.Code),
            Assert.Single(result.Value.Sheets).Rows.Select(row => (string?)row[1]));
    }

    /// <summary>
    /// ⚠️ <b>حاجز على الفحص اللي فوق:</b> لازم مفتاحين على الأقل
    /// يدّوا ترتيب <b>مختلف</b> على البيانات دي. لو كلهم بيدّوا نفس
    /// الترتيب، الفحص بيعدّي وهو مش بيقيس حاجة.
    /// </summary>
    [Fact]
    public void The_sort_keys_actually_disagree_on_this_data()
    {
        var old = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        List<TestingProductivityFacts> testing =
        [
            new("T001", "ياسر", 1, 1, 0, 0, 0, 0, Timed: 1, 60_000, old),
            new("T002", "أحمد", 9, 9, 0, 0, 0, 0, Timed: 9, 60_000, recent),
        ];

        var rows = TechnicianProductivity.Rows(
            testing, new Dictionary<string, RepairProductivityFacts>());

        var orders = new[] { "name", "busiest", "code", "last", null }
            .Select(key => string.Join(
                ",", TechnicianProductivity.Sorted(rows, key).Select(r => r.Code)))
            .Distinct()
            .ToList();

        Assert.True(orders.Count >= 2,
            $"كل المفاتيح بتدّي نفس الترتيب ({orders.Count}) — الفحص اللي فوق بقى فاضي");
    }

    // =================================================================
    //  الأسرار
    // =================================================================

    /// <summary>
    /// 🔴 <b>ولا مفتاح ولا بصمة ولا ملح في ملف المحطات.</b> الملف
    /// بينزل على جهاز المالك وبيتبعت في واتساب أحياناً.
    /// </summary>
    [Fact]
    public async Task The_rack_export_carries_no_key_material()
    {
        var repo = new FakeRackExportRepository();
        var me = new FakeCurrentUser(UserRole.Owner);

        repo.Racks.Add(new Rack
        {
            TenantId = me.TenantId, RackCode = "RK-01", Name = "محطة",
            ApiKeyHash = "SECRET-HASH", Salt = "SECRET-SALT", KeyPrefix = "SECRET-PFX",
            InstallationId = "SECRET-INSTALL", Status = RackStatus.Active,
        });

        var result = await new ExportRacksQueryHandler(repo, me)
            .Handle(new ExportRacksQuery(), default);

        var cells = Assert.Single(result.Value.Sheets).Rows
            .SelectMany(row => row)
            .Select(v => v?.ToString() ?? "")
            .ToList();

        Assert.DoesNotContain(cells, c => c.Contains("SECRET", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>وجدول حسابات الفنيين مالوش عمود بصمة.</b> عمود واحد
    /// منهم معناه تسريب كل باسوردات الفنيين في ملف واحد.
    /// </summary>
    [Fact]
    public void The_technician_account_sheet_has_no_secret_column()
    {
        var headers = ExportColumns.TechnicianAccounts
            .Select(c => c.Header)
            .ToList();

        Assert.DoesNotContain(headers, h => h.Contains("بصمة", StringComparison.Ordinal));
        Assert.DoesNotContain(headers, h => h.Contains("ملح", StringComparison.Ordinal));

        // ⚠️ و«لازم يغيّر الباسورد» عمود مشروع — الكلمة لوحدها مش دليل.
        Assert.Contains("لازم يغيّر الباسورد", headers);
    }

    // =================================================================
    //  العناوين والسياسات
    // =================================================================

    private static HttpMethodAttribute Route(string action) =>
        typeof(ExportController).GetMethod(action)!
            .GetCustomAttributes().OfType<HttpMethodAttribute>().Single();

    private static string? Policy(string action) =>
        typeof(ExportController).GetMethod(action)!
            .GetCustomAttributes().OfType<AuthorizeAttribute>()
            .SingleOrDefault()?.Policy;

    public static TheoryData<string, string, string?> Expected => new()
    {
        /*
          🔴 **الفحوص من غير سياسة — ودي مقصودة.**

          الفني بيصدّر شغله هو، والتضييق في الاستعلام نفسه. وحطّ
          `ManagerOrAbove` عليها كان هيمنع الفني من تصدير فحوصاته.
        */
        { nameof(ExportController.Reports), "reports.xlsx", null },

        { nameof(ExportController.Devices), "devices.xlsx", Policies.ManagerOrAbove },
        { nameof(ExportController.Repairs), "repairs.xlsx", Policies.ManagerOrAbove },
        { nameof(ExportController.Productivity), "productivity.xlsx", Policies.ManagerOrAbove },
        { nameof(ExportController.Technicians), "technicians.xlsx", Policies.ManagerOrAbove },

        // 🔴 المحطات والسجل للمالك — دول بيكشفوا الورشة كلها.
        { nameof(ExportController.Racks), "racks.xlsx", Policies.OwnerOnly },
        { nameof(ExportController.Audit), "audit.xlsx", Policies.OwnerOnly },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Every_export_keeps_its_url_and_its_policy(
        string action, string template, string? policy)
    {
        var route = Route(action);

        Assert.Equal(["GET"], route.HttpMethods);
        Assert.Equal(template, route.Template);
        Assert.Equal(policy, Policy(action));
    }

    [Fact]
    public void No_export_is_left_out_of_this_matrix()
    {
        int actions = typeof(ExportController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());

        Assert.Equal(Expected.Count(), actions);
    }

    /// <summary>
    /// 🔴 <b>وحارس الكلاس من غير سياسة.</b> ASP.NET بيجمع سياسة
    /// الكلاس وسياسة الإجراء بـ«و» — فـ<c>OwnerOnly</c> على الكلاس
    /// كانت هتمنع المدير من تصدير الأجهزة والفني من تصدير فحوصاته.
    /// </summary>
    [Fact]
    public void The_class_level_gate_carries_no_policy()
    {
        var onClass = typeof(ExportController)
            .GetCustomAttributes().OfType<AuthorizeAttribute>().Single();

        Assert.Null(onClass.Policy);
        Assert.Null(onClass.Roles);
    }

    [Fact]
    public void The_export_prefix_is_frozen()
    {
        var route = typeof(ExportController)
            .GetCustomAttributes().OfType<RouteAttribute>().Single();

        Assert.Equal("api/v1/export", route.Template);
    }

    /// <summary>
    /// ⚠️ <b>الامتداد <c>.xlsx</c> جزء من العنوان مش من نوع
    /// المحتوى.</b> اللوحة بتفتح الرابط في تاب جديد، والمتصفح بيسمّي
    /// الملف من آخر مقطع لو الترويسة ضاعت.
    /// </summary>
    [Fact]
    public void Every_export_url_ends_with_the_extension()
    {
        foreach (var row in Expected)
            Assert.EndsWith(".xlsx", (string)row[1]!, StringComparison.Ordinal);
    }

    // =================================================================
    //  بدائل
    // =================================================================

    private sealed class FakeRackExportRepository : IRackRepository
    {
        public readonly List<Rack> Racks = [];

        public Task<IReadOnlyList<Rack>> ListAsync(Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Rack>>(Racks.Where(r => r.TenantId == t).ToList());

        public Task<Rack?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Rack?>(null);

        public Task<IReadOnlyList<Rack>> ActiveByKeyPrefixAsync(
            string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Rack>>(
                Racks.Where(r => r.Status == RackStatus.Active
                              && r.KeyPrefix == prefix).ToList());

        public Task<IReadOnlyList<RackPairingCode>> PendingCodesAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>([]);

        public Task<RackPairingCode?> FindCodeAsync(
            Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<RackPairingCode?>(null);

        public void AddCode(RackPairingCode code) { }

        public Task<IReadOnlyList<RackPairingCode>> CodesByPrefixAsync(
            string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RackPairingCode>>([]);

        public Task<bool> ConsumeCodeAsync(
            Guid codeId, DateTime atUtc, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task LinkCodeToRackAsync(
            Guid codeId, Guid rackId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Add(Rack rack) { }

        public Task<IReadOnlyList<Rack>> TwinsByInstallationAsync(
            Guid t, string installationId, Guid except, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Rack>>([]);


        public Task TouchAsync(
            Guid rackId, int reportsReceived, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<string?> TenantNameAsync(Guid t, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public void RemoveCode(RackPairingCode code) { }
    }

    private sealed class FakeProductivityRepository : ITechnicianProductivityRepository
    {
        public readonly List<TestingProductivityFacts> Testing = [];
        public readonly Dictionary<string, RepairProductivityFacts> Repairs = [];

        public Task<IReadOnlyList<TestingProductivityFacts>> TestingAsync(
            Guid t, AnalyticsPeriod w, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TestingProductivityFacts>>(Testing);

        public Task<IReadOnlyDictionary<string, RepairProductivityFacts>> RepairsAsync(
            Guid t, AnalyticsPeriod w, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, RepairProductivityFacts>>(Repairs);

        public Task<bool> HasAnyReportAsync(Guid t, string code, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<string?> NameFromReportsAsync(
            Guid t, string code, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<TestingProductivityFacts?> OneAsync(
            Guid t, string code, AnalyticsPeriod w, CancellationToken ct = default) =>
            Task.FromResult<TestingProductivityFacts?>(null);

        public Task<IReadOnlyList<Report>> RecentAsync(
            Guid t, string code, AnalyticsPeriod w, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Report>>([]);
    }
}
