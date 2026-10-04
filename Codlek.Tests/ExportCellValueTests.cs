using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using Codlek.Application.Features.Export.ExportAudit;
using Codlek.Application.Features.Export.ExportReports;
using Codlek.Application.Features.Export.ExportTechnicians;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Spreadsheets;

namespace Codlek.Tests;

/// <summary>
/// <b>قيم الخلايا</b> في ملفات التصدير.
///
/// <para>🔴 <b>والملف ده اتكتب بعد ما تلات تحويرات مقصودة نجت من
/// كل الفحوص.</b> كان فيه تغطية على الأعمدة والفلاتر والصلاحيات،
/// ومفيش ولا فحص بيبص على <u>اللي جوّه الخانة</u> في تصدير السجل
/// ولا الفحوص ولا الفنيين. التحويرات اللي نجت كانت:</para>
///
/// <list type="number">
///   <item>سجل المراجعة بيكتب كود الإجراء الخام
///   (<c>rack.paired</c>) بدل الترجمة — نفس الباج اللي فضل شهور في
///   القديم، بس في الملف مش في الشاشة.</item>
///
///   <item><b>البصمة اتحشرت في عمود «اسم المستخدم»</b> —
///   والفحص اللي اسمه «مفيش عمود سرّي» عدّى، لأنه كان بيبص على
///   <b>العناوين</b> وبس.</item>
///
///   <item>عمود «النتيجة» في الفحوص اتعكس: اللاب السليم بقى «محتاج
///   مراجعة» والعكس.</item>
/// </list>
/// </summary>
public class ExportCellValueTests
{
    // =================================================================
    //  بدائل
    // =================================================================

    private sealed class StubReportRepository(IReadOnlyList<Report> rows) : IReportRepository
    {
        public Dictionary<Guid, RackLabel> Racks = [];

        public Task<(IReadOnlyList<Report> Rows, int TotalItems)> ListAsync(
            Guid t, ReportListFilter f, CancellationToken ct = default) =>
            Task.FromResult((rows, rows.Count));

        public Task<IReadOnlyList<Report>> ExportAsync(
            Guid t, ReportListFilter f, int cap, CancellationToken ct = default) =>
            Task.FromResult(rows);

        public Task<Report?> FindDetailAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Report?>(null);

        public Task<int> SnapshotComponentCountAsync(
            Guid t, Guid reportId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<ReportVersionFacts?> VersionsAsync(
            Guid t, Guid reportId, CancellationToken ct = default) =>
            Task.FromResult<ReportVersionFacts?>(null);

        public Task<IReadOnlyDictionary<Guid, RackLabel>> RackLabelsAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, RackLabel>>(Racks);

        public Task<IReadOnlyDictionary<Guid, string>> DeviceCodesAsync(
            Guid t, IEnumerable<Guid?> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }

    private sealed class StubAuditRepository(IReadOnlyList<AuditEvent> rows) : IAuditRepository
    {
        public Task<(IReadOnlyList<AuditEvent> Rows, int TotalItems)> SearchAsync(
            Guid t, AuditFilter f, CancellationToken ct = default) =>
            Task.FromResult((rows, rows.Count));

        public Task<IReadOnlyList<AuditEvent>> ExportAsync(
            Guid t, AuditFilter f, int cap, CancellationToken ct = default) =>
            Task.FromResult(rows);

        public Task<(IReadOnlyList<string> Actions,
                     IReadOnlyList<string> EntityTypes,
                     IReadOnlyList<string> Actors)> DistinctValuesAsync(
            Guid t, int maxActors, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>)>
                (([], [], []));
    }

    private sealed class StubAccountRepository(IReadOnlyList<Technician> rows)
        : ITechnicianAccountRepository
    {
        public Task<IReadOnlyList<Technician>> ListAsync(
            Guid t, CancellationToken ct = default) => Task.FromResult(rows);

        public Task<Technician?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Technician?>(null);

        public Task<bool> UsernameTakenAsync(
            Guid t, string normalized, CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> CodeTakenAsync(Guid t, string code, CancellationToken ct = default) =>
            Task.FromResult(false);

        public void Add(Technician technician) { }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> BrandLinksAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
                new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyList<TechnicianBrand>> BrandLinksForAsync(
            Guid t, Guid technicianId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TechnicianBrand>>([]);

        public Task<int> CountKnownBrandsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<string>> BrandNamesAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public void RemoveBrandLinks(IEnumerable<TechnicianBrand> links) { }

        public void AddBrandLink(TechnicianBrand link) { }
    }

    private sealed class StubDepartmentRepository : IDepartmentRepository
    {
        public Task<IReadOnlyList<DepartmentRow>> ListAsync(
            Guid t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DepartmentRow>>([]);

        public Task<Department?> FindAsync(Guid t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Department?>(null);

        public Task<bool> NameTakenAsync(
            Guid t, string name, Guid? except, CancellationToken ct = default) =>
            Task.FromResult(false);

        public void Add(Department department) { }

        public Task<int> CountTechniciansAsync(
            Guid departmentId, CancellationToken ct = default) => Task.FromResult(0);
    }

    // =================================================================
    //  مساعدات
    // =================================================================

    private static Report NewReport(
        int fail = 0, string commercial = "", string model = "T480",
        Guid? rackId = null, long durationMs = 120_000)
    {
        return new Report
        {
            DeviceCode = "DV-0001",
            Manufacturer = "Lenovo",
            Model = model,
            CommercialModelName = commercial.Length == 0 ? null : commercial,
            Cpu = "i5-8250U",
            RamText = "8 GB",
            StorageText = "256 GB SSD",
            Gpu = "UHD 620",
            ScreenSummary = "14 FHD",
            SerialNumber = "PF0ABCDE",
            TechnicianName = "محمود الفني",
            TechnicianCode = "T001",
            StartedAtUtc = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            ReceivedAtUtc = new DateTime(2026, 9, 1, 9, 5, 0, DateTimeKind.Utc),
            DurationMs = durationMs,
            PassCount = 10,
            FailCount = fail,
            ErrorCount = 1,
            SkipCount = 2,
            NotPresentCount = 3,
            SourceRackId = rackId,
        };
    }

    private static IReadOnlyList<object?[]> RowsOf(Sheet sheet) => sheet.Rows.ToList();

    /// <summary>فهرس عمود بعنوانه — عشان الفحص مايتعلّقش بالترتيب.</summary>
    private static int Column(Sheet sheet, string header)
    {
        for (int i = 0; i < sheet.Columns.Length; i++)
            if (sheet.Columns[i].Header == header) return i;

        throw new InvalidOperationException($"مفيش عمود اسمه «{header}»");
    }

    private static async Task<Sheet> ReportSheetAsync(
        IReadOnlyList<Report> rows, Dictionary<Guid, RackLabel>? racks = null)
    {
        var repo = new StubReportRepository(rows);
        if (racks != null) repo.Racks = racks;

        var result = await new ExportReportsQueryHandler(
                repo, new FakeCurrentUser(UserRole.Manager))
            .Handle(new ExportReportsQuery(null, null, null, null, null, null), default);

        return result.Value.Sheets.Single();
    }

    // =================================================================
    //  عمود «النتيجة» في الفحوص
    // =================================================================

    /// <summary>
    /// 🔴 <b>الاتجاهين — ودي الحاجة اللي خلّت التحوير ينجا.</b>
    /// فحص على حالة واحدة بس مابيفرّقش بين العمود الصح والعمود
    /// المعكوس.
    /// </summary>
    [Theory]
    [InlineData(0, "سليم")]
    [InlineData(1, "محتاج مراجعة")]
    [InlineData(9, "محتاج مراجعة")]
    public async Task The_report_outcome_column_follows_the_fail_count(int fail, string expected)
    {
        var sheet = await ReportSheetAsync([NewReport(fail: fail)]);

        var row = Assert.Single(RowsOf(sheet));

        Assert.Equal(expected, row[Column(sheet, "النتيجة")]);
    }

    /// <summary>
    /// ⚠️ <b>الاسم التجاري بيكسب الموديل الخام — والماركة مش
    /// بتتحشر معاه.</b> «HP HP ProBook» كان بيطلع في القديم لما
    /// الاسمين اتجمعوا في خانة واحدة.
    /// </summary>
    [Theory]
    [InlineData("ThinkPad T480", "ThinkPad T480")]
    [InlineData("", "T480")]
    public async Task The_report_model_column_prefers_the_commercial_name(
        string commercial, string expected)
    {
        var sheet = await ReportSheetAsync([NewReport(commercial: commercial)]);
        var row = Assert.Single(RowsOf(sheet));

        Assert.Equal(expected, row[Column(sheet, "الموديل")]);

        // ⚠️ والماركة في عمودها.
        Assert.Equal("Lenovo", row[Column(sheet, "الماركة")]);
    }

    /// <summary>
    /// ⚠️ المدة بتتحوّل لدقايق — المصدر مللي ثانية، والمدير بيجمع
    /// العمود ده في إكسل.
    /// </summary>
    [Fact]
    public async Task The_report_duration_is_written_in_minutes()
    {
        var sheet = await ReportSheetAsync([NewReport(durationMs: 150_000)]);
        var row = Assert.Single(RowsOf(sheet));

        Assert.Equal(2.5, row[Column(sheet, "المدة (دقيقة)")]);
    }

    /// <summary>
    /// ⚠️ «اتخطّى» بيجمع المتخطّى والمش موجود — عمودين على الشاشة
    /// وعمود واحد في الملف، وده مقصود (نقل من القديم).
    /// </summary>
    [Fact]
    public async Task The_skipped_column_merges_skip_and_not_present()
    {
        var sheet = await ReportSheetAsync([NewReport()]);
        var row = Assert.Single(RowsOf(sheet));

        // 2 متخطّى + 3 مش موجود
        Assert.Equal(5, row[Column(sheet, "اتخطّى")]);
    }

    /// <summary>
    /// ⚠️ كود المحطة بييجي من القراية الواحدة، والمش معروف بيبقى
    /// خانة فاضية — مش معرّف خام.
    /// </summary>
    [Fact]
    public async Task The_rack_column_shows_the_code_or_nothing()
    {
        var rackId = Guid.NewGuid();

        var known = await ReportSheetAsync(
            [NewReport(rackId: rackId)],
            new Dictionary<Guid, RackLabel> { [rackId] = new("RK-07", "محطة سبعة") });

        Assert.Equal("RK-07", Assert.Single(RowsOf(known))[Column(known, "المحطة")]);

        var unknown = await ReportSheetAsync([NewReport(rackId: Guid.NewGuid())]);

        Assert.Equal("", Assert.Single(RowsOf(unknown))[Column(unknown, "المحطة")]);
    }

    /// <summary>
    /// ⚠️ ومحطة مش مبعوتة خالص (<c>null</c>) بتبقى فاضية — مش
    /// <c>Guid.Empty</c>.
    /// </summary>
    [Fact]
    public async Task A_report_with_no_rack_leaves_the_column_empty()
    {
        var sheet = await ReportSheetAsync([NewReport(rackId: null)]);

        Assert.Equal("", Assert.Single(RowsOf(sheet))[Column(sheet, "المحطة")]);
    }

    // =================================================================
    //  سجل المراجعة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الإجراء بيتترجم في الملف زي ما بيتترجم على
    /// الشاشة.</b>
    ///
    /// <para>والتحوير اللي نجا كان بيكتب <c>rack.paired</c> خام —
    /// نفس الباج اللي فضل <b>شهور</b> في القديم، بس في ملف إكسل
    /// والمالك بيفتحه ومايعرفش السطر ده بيقول إيه.</para>
    /// </summary>
    [Theory]
    [InlineData("rack.paired", "تفعيل محطة فحص")]
    [InlineData("device.identity_merged", "دمج هوية جهاز")]
    [InlineData("technician.created", "إنشاء فني")]
    public async Task The_audit_export_translates_the_action(string action, string expected)
    {
        var sheet = await AuditSheetAsync(new AuditEvent
        {
            Action = action,
            ActorName = "المالك",
            ActorType = "User",
            EntityType = "Rack",
            EntityCode = "RK-01",
            Summary = "ملخّص",
            OccurredAtUtc = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
        });

        var row = Assert.Single(RowsOf(sheet));

        Assert.Equal(expected, row[Column(sheet, "الإجراء")]);

        // 🔴 والكود الخام عمره ما يظهر في أي خانة.
        Assert.DoesNotContain(action, row.Select(c => c?.ToString() ?? ""));
    }

    /// <summary>
    /// ⚠️ <b>والكود المجهول بيرجع بنفسه — مش بيختفي.</b> سطر مالوش
    /// ترجمة لازم يفضل مقروء؛ إخفاؤه بيخلّي الرقابة ناقصة من غير ما
    /// حد يعرف.
    /// </summary>
    [Fact]
    public async Task An_unknown_audit_action_is_still_shown()
    {
        var sheet = await AuditSheetAsync(new AuditEvent
        {
            Action = "future.thing",
            ActorName = "النظام",
            ActorType = "System",
            Summary = "حاجة جديدة",
        });

        Assert.Equal("future.thing", Assert.Single(RowsOf(sheet))[Column(sheet, "الإجراء")]);
    }

    /// <summary>
    /// ⚠️ <b>ونوع الكيان بيتكتب خام عن قصد.</b> العمود بيستعمل
    /// للفرز في إكسل، فالقيمة الخام بتفرز مع بعضها — وده سلوك
    /// القديم.
    /// </summary>
    [Fact]
    public async Task The_audit_entity_type_stays_raw()
    {
        var sheet = await AuditSheetAsync(new AuditEvent
        {
            Action = "rack.paired", EntityType = "Rack", ActorType = "User",
        });

        Assert.Equal("Rack", Assert.Single(RowsOf(sheet))[Column(sheet, "نوع الكيان")]);
    }

    private static async Task<Sheet> AuditSheetAsync(params AuditEvent[] rows)
    {
        var result = await new ExportAuditQueryHandler(
                new StubAuditRepository(rows), new FakeCurrentUser(UserRole.Owner))
            .Handle(new ExportAuditQuery(null, null, null, null, null, null), default);

        return result.Value.Sheets.Single();
    }

    // =================================================================
    //  جدول حسابات الفنيين — الأسرار
    // =================================================================

    /// <summary>
    /// 🔴 <b>مفيش خانة واحدة في الملف كله فيها بصمة ولا ملح.</b>
    ///
    /// <para>⚠️ <b>والفحص ده بيبص على <u>القيم</u> مش على
    /// العناوين.</b> كان فيه فحص اسمه «الجدول مالوش عمود سرّي»
    /// وبيقرا العناوين وبس — فتحوير حشر البصمة في عمود «اسم
    /// المستخدم» وعدّى من تحته. العنوان بيفضل نضيف والبصمة في
    /// الملف.</para>
    /// </summary>
    [Fact]
    public async Task No_cell_anywhere_in_the_technician_export_carries_a_secret()
    {
        const string Hash = "HASH-ABCDEF0123456789";
        const string Salt = "SALT-9876543210FEDCBA";

        var technician = new Technician
        {
            Code = "T001",
            DisplayName = "محمود الفني",
            Username = "mahmoud",
            NormalizedUsername = "mahmoud",
            PasswordHash = Hash,
            Salt = Salt,
            IsActive = true,
            Specialty = TechnicianSpecialty.Screens,
            CanTest = true,
            CanRepair = false,
            MustChangePassword = true,
            SuspendedReason = "",
        };

        var result = await new ExportTechniciansQueryHandler(
                new StubReportRepository([]),
                new StubAccountRepository([technician]),
                new StubDepartmentRepository(),
                new FakeCurrentUser(UserRole.Manager))
            .Handle(new ExportTechniciansQuery(null, null, null, null), default);

        var cells = result.Value.Sheets
            .SelectMany(s => s.Rows)
            .SelectMany(row => row)
            .Select(v => v?.ToString() ?? "")
            .ToList();

        Assert.NotEmpty(cells);
        Assert.DoesNotContain(Hash, cells);
        Assert.DoesNotContain(Salt, cells);

        /*
          ⚠️ **وحتى جزء منهم.** البصمة المقطوعة سر بردو: الحشر في
          خانة ضيّقة بيقصّها، والمقارنة المضبوطة كانت بتعدّي.
        */
        Assert.All(cells, c =>
        {
            Assert.DoesNotContain("HASH-", c, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SALT-", c, StringComparison.OrdinalIgnoreCase);
        });

        // ⚠️ وحاجز: الاسم الحقيقي **موجود** — فالفحص مش بيمر على ملف فاضي.
        Assert.Contains("mahmoud", cells);
        Assert.Contains("محمود الفني", cells);
    }

    /// <summary>
    /// ⚠️ وجدول الحسابات بيعرض الحالة والصلاحيات بالعربي — مش
    /// <c>True</c>/<c>False</c>.
    /// </summary>
    [Fact]
    public async Task The_account_sheet_reads_in_arabic()
    {
        var technician = new Technician
        {
            Code = "T002",
            DisplayName = "كريم",
            Username = "kareem",
            NormalizedUsername = "kareem",
            IsActive = false,
            Specialty = TechnicianSpecialty.Boards,
            CanTest = false,
            CanRepair = true,
            MustChangePassword = false,
            SuspendedReason = "غياب",
        };

        var result = await new ExportTechniciansQueryHandler(
                new StubReportRepository([]),
                new StubAccountRepository([technician]),
                new StubDepartmentRepository(),
                new FakeCurrentUser(UserRole.Manager))
            .Handle(new ExportTechniciansQuery(null, null, null, null), default);

        var sheet = result.Value.Sheets.Single(s => s.Name == "الحسابات");
        var row = Assert.Single(RowsOf(sheet));

        Assert.Equal("موقوف", row[Column(sheet, "الحالة")]);
        Assert.Equal("بوردات", row[Column(sheet, "التخصص")]);
        Assert.Equal("لأ", row[Column(sheet, "يفحص")]);
        Assert.Equal("نعم", row[Column(sheet, "يصلّح")]);
        Assert.Equal("لأ", row[Column(sheet, "لازم يغيّر الباسورد")]);
        Assert.Equal("غياب", row[Column(sheet, "سبب الإيقاف")]);
    }
}
