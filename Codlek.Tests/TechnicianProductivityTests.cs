using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Features.Technicians;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Technicians;

namespace Codlek.Tests;

/// <summary>
/// جدول إنتاجية الفنيين — <b>الدوال النقية</b>.
///
/// <para>🔴 <b>وأهم حاجة هنا: الفني اللي عمل صيانة بس لازم
/// يبان.</b> الاستعلام الأساسي مبني من جدول <b>الفحوص</b>، فلو اتسيب
/// زي ما هو فني الصيانة اللي مابيفحصش <b>مش هيظهر في الصفحة
/// خالص</b> — وده بالظبط اللي صاحب الشغل اشتكى منه.</para>
/// </summary>
public class TechnicianProductivityTests
{
    private static TestingProductivityFacts Testing(
        string code, string name = "", int total = 10,
        int fail = 0, int error = 0, int timed = 10, long durationMs = 6_000_000,
        DateTime? lastAtUtc = null) =>
        new(code, name, total, total - fail - error, fail, error, 0, 0,
            timed, durationMs, lastAtUtc);

    private static RepairProductivityFacts Repair(
        string name = "", int count = 3, double average = 45,
        DateTime? lastAtUtc = null) =>
        new(name, count, average, lastAtUtc);

    // =================================================================
    //  الفترة
    // =================================================================

    /// <summary>
    /// 🔴 <b>الافتراضي «من البداية»، والافتراضي على اللوحة ٣٠ يوم —
    /// والفرق مقصود.</b>
    ///
    /// <para>اللوحة بتجاوب على «مين اشتغل الشهر ده»؛ والشاشة دي
    /// بتجاوب على «مين اشتغل أصلاً». فني آخر شغلانة ليه من تلات
    /// شهور لازم يفضل في القايمة، وإلا بيختفي من النظام وهو لسه
    /// موظّف.</para>
    /// </summary>
    [Fact]
    public void The_default_window_is_everything_not_thirty_days()
    {
        Assert.Equal("all", TechnicianProductivity.Window(null, null, null).Key);
        Assert.Equal("all", TechnicianProductivity.Window("all", null, null).Key);
        Assert.Equal("all", TechnicianProductivity.Window("  ALL ", null, null).Key);

        // ⚠️ والمفتاح الفاضي بيعدّي على القارئ العادي — ٣٠ يوم.
        Assert.Equal("30", TechnicianProductivity.Window("", null, null).Key);
        Assert.Equal("30", TechnicianProductivity.Window("whatever", null, null).Key);
        Assert.Equal("7", TechnicianProductivity.Window("7", null, null).Key);
    }

    /// <summary>
    /// ⚠️ و«من البداية» بتبدأ من بداية الزمن فعلاً — مش من ٣٦٥
    /// يوم.
    /// </summary>
    [Fact]
    public void Everything_really_starts_at_the_beginning()
    {
        var window = TechnicianProductivity.Window("all", null, null);

        Assert.Equal(DateTime.UnixEpoch, window.FromUtc);
        Assert.False(window.Hourly);
    }

    // =================================================================
    //  الضم بين الفحص والصيانة
    // =================================================================

    [Fact]
    public void A_technician_who_both_tests_and_repairs_gets_one_row()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T001", "محمود", total: 20, timed: 20, durationMs: 12_000_000)],
            new Dictionary<string, RepairProductivityFacts>
            {
                ["T001"] = Repair("محمود", count: 5, average: 30),
            });

        var row = Assert.Single(rows);

        Assert.Equal("T001", row.Code);
        Assert.Equal("محمود", row.Name);
        Assert.True(row.NameAvailable);
        Assert.Equal(20, row.TotalReports);
        Assert.Equal(5, row.RepairsCompleted);
        Assert.Equal(30, row.RepairAverageMinutes);

        // ⚠️ ١٢ مليون مللي على ٢٠ فحص = عشر دقايق.
        Assert.Equal(10, row.AverageMinutes);
    }

    /// <summary>
    /// 🔴 <b>والفني اللي عمل صيانة بس لازم يبان بصفوف فحص
    /// أصفار.</b>
    /// </summary>
    [Fact]
    public void A_repair_only_technician_still_appears()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T001", "محمود")],
            new Dictionary<string, RepairProductivityFacts>
            {
                ["T999"] = Repair("كريم الصيانة", count: 7, average: 55),
            });

        Assert.Equal(2, rows.Count);

        var repairOnly = rows.Single(r => r.Code == "T999");

        Assert.Equal("كريم الصيانة", repairOnly.Name);
        Assert.Equal(0, repairOnly.TotalReports);
        Assert.Equal(7, repairOnly.RepairsCompleted);
        Assert.Equal(55, repairOnly.RepairAverageMinutes);
        Assert.Equal(0, repairOnly.AverageMinutes);
    }

    /// <summary>
    /// ⚠️ <b>والفني اللي مالوش ولا صيانة خلصت مابيتزادش</b> — صفر
    /// في صفر مش صف.
    /// </summary>
    [Fact]
    public void A_repair_row_with_no_completed_work_is_not_added()
    {
        var rows = TechnicianProductivity.Rows(
            [],
            new Dictionary<string, RepairProductivityFacts>
            {
                ["T999"] = Repair("كريم", count: 0),
            });

        Assert.Empty(rows);
    }

    /// <summary>
    /// ⚠️ والضم بحالة أحرف متجاهلة — الكود بيتكتب بأشكال مختلفة.
    /// </summary>
    [Fact]
    public void The_join_between_the_two_keys_ignores_case()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("t001", "محمود")],
            new Dictionary<string, RepairProductivityFacts>(StringComparer.OrdinalIgnoreCase)
            {
                ["T001"] = Repair("محمود", count: 4),
            });

        var row = Assert.Single(rows);

        Assert.Equal(4, row.RepairsCompleted);
    }

    // =================================================================
    //  الاسم الناقص
    // =================================================================

    /// <summary>
    /// ⚠️ <b>«فني محطة T001» أنفع من «الاسم غير متاح»:</b>
    /// الأولانية بتقول للمدير يدوّر فين، والتانية بتقول إن فيه حاجة
    /// باظت.
    /// </summary>
    [Fact]
    public void A_missing_name_becomes_a_station_label_with_a_flag()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T777", name: "")],
            new Dictionary<string, RepairProductivityFacts>());

        var row = Assert.Single(rows);

        Assert.Equal("فني محطة T777", row.Name);
        Assert.Equal(RackTechnicianLabel.Neutral("T777"), row.Name);

        // 🔴 وعلم صريح بدل ما الواجهة تخمّن من شكل الاسم.
        Assert.False(row.NameAvailable);
    }

    // =================================================================
    //  المتوسطات
    // =================================================================

    /// <summary>
    /// ⚠️ <b>الفحوص اللي مالهاش مدة مابتدخلش المقام.</b> ولو
    /// دخلت، المتوسط بينزل من غير أي معنى.
    /// </summary>
    [Fact]
    public void Untimed_reports_stay_out_of_the_average()
    {
        var rows = TechnicianProductivity.Rows(
            // ٢٠ فحص، ١٠ بس ليهم مدة، ومجموعهم ٦ مليون مللي.
            [Testing("T001", "محمود", total: 20, timed: 10, durationMs: 6_000_000)],
            new Dictionary<string, RepairProductivityFacts>());

        // ⚠️ عشر دقايق (على العشرة) — مش خمسة (على العشرين).
        Assert.Equal(10, Assert.Single(rows).AverageMinutes);
    }

    [Fact]
    public void A_technician_with_no_timed_report_averages_zero_not_infinity()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T001", "محمود", total: 5, timed: 0, durationMs: 0)],
            new Dictionary<string, RepairProductivityFacts>());

        Assert.Equal(0, Assert.Single(rows).AverageMinutes);
    }

    // =================================================================
    //  البحث
    // =================================================================

    /// <summary>
    /// 🔴 <b>التطبيع العربي مش زينة.</b>
    ///
    /// <para>«أحمد» <b>مش</b> بتساوي «احمد» في أي collation — الهمزة
    /// حرف مستقل في يونيكود مش علامة تشكيل. ومن غير التطبيع المدير
    /// بيكتب «احمد» وبيلاقي القايمة فاضية ويفتكر إن الفني مالوش
    /// شغل.</para>
    ///
    /// <para>🔴 <b>والتطبيع على الطرفين:</b> الأسماء جاية خام،
    /// فتطبيع اللي اتكتب وبس بيقارن «احمد» بـ«أحمد» الخام ويفشل
    /// بردو.</para>
    /// </summary>
    [Theory]
    [InlineData("احمد")]
    [InlineData("أحمد")]
    [InlineData("احمـد")]
    public void Search_finds_a_hamza_name_however_it_is_typed(string typed)
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T001", "أحمد عبد الله"), Testing("T002", "محمود")],
            new Dictionary<string, RepairProductivityFacts>());

        var found = TechnicianProductivity.Matching(rows, typed);

        Assert.Equal(["T001"], found.Select(r => r.Code));
    }

    /// <summary>
    /// ⚠️ والكود بيعدّي على نفس التطبيع: الأرقام العربية-الهندية
    /// بتتحوّل لاتينية، فالبحث بالكود شغّال بأي كيبورد.
    /// </summary>
    [Fact]
    public void Search_by_code_works_with_arabic_indic_digits()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("100001", "المالك"), Testing("100002", "المحاسب")],
            new Dictionary<string, RepairProductivityFacts>());

        var found = TechnicianProductivity.Matching(rows, "١٠٠٠٠١");

        Assert.Equal(["100001"], found.Select(r => r.Code));
    }

    [Fact]
    public void An_empty_search_changes_nothing()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T001", "محمود")],
            new Dictionary<string, RepairProductivityFacts>());

        Assert.Single(TechnicianProductivity.Matching(rows, null));
        Assert.Single(TechnicianProductivity.Matching(rows, "   "));
    }

    // =================================================================
    //  الترتيب
    // =================================================================

    /// <summary>
    /// 🔴 <b>اللي مالوش نشاط بيروح الآخر في ترتيب «الأحدث».</b>
    ///
    /// <para>في .NET الـ<c>null</c> أصغر من أي قيمة، فالترتيب
    /// التنازلي بيحطّها في الذيل. <b>وده مضمون في الذاكرة بس:</b> لو
    /// الترتيب ده اتنقل لـSQL يوم، فيه مزوّدين بيحطّوا <c>NULL</c>
    /// <b>الأول</b> في التنازلي — واللي عمره ما اشتغل بيدفن اللي
    /// بيشتغل. والفحص ده بيثبّت الترتيب عشان النقلة متعديش في
    /// صمت.</para>
    /// </summary>
    [Fact]
    public void Sorting_by_recent_pushes_never_worked_to_the_tail()
    {
        var day = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        var rows = TechnicianProductivity.Rows(
            [
                Testing("T-NEVER", "عمره ما اشتغل", lastAtUtc: null),
                Testing("T-OLD", "قديم", lastAtUtc: day.AddDays(-30)),
                Testing("T-NEW", "جديد", lastAtUtc: day),
            ],
            new Dictionary<string, RepairProductivityFacts>());

        var sorted = TechnicianProductivity.Sorted(rows, "recent");

        Assert.Equal(["T-NEW", "T-OLD", "T-NEVER"], sorted.Select(r => r.Code));
    }

    /// <summary>
    /// ⚠️ والافتراضي «الأشغل الأول» = فحص <b>+</b> صيانة — عشان
    /// فني الصيانة مايقعدش آخر الجدول.
    /// </summary>
    [Fact]
    public void The_default_sort_adds_tests_and_repairs_together()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T-TESTER", "فاحص", total: 10), Testing("T-BOTH", "الاتنين", total: 5)],
            new Dictionary<string, RepairProductivityFacts>
            {
                ["T-BOTH"] = Repair("الاتنين", count: 20),
                ["T-REPAIR"] = Repair("صيانة بس", count: 12),
            });

        var sorted = TechnicianProductivity.Sorted(rows, null);

        // ٢٥ · ١٢ · ١٠
        Assert.Equal(["T-BOTH", "T-REPAIR", "T-TESTER"], sorted.Select(r => r.Code));
    }

    /// <summary>
    /// 🔴 <b>المفاتيح دي بروتوكول مش نص للعرض</b> — الواجهة بتبعتها
    /// زي ما هي، والملف المصدَّر بيستعملها كمان.
    /// </summary>
    [Theory]
    [InlineData("name")]
    [InlineData("code")]
    [InlineData("tests")]
    [InlineData("testtime")]
    [InlineData("repairs")]
    [InlineData("repairtime")]
    [InlineData("recent")]
    [InlineData("problems")]
    public void Every_documented_sort_key_is_handled(string key)
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T002", "ب"), Testing("T001", "أ")],
            new Dictionary<string, RepairProductivityFacts>());

        var sorted = TechnicianProductivity.Sorted(rows, key);

        // ⚠️ الفحص ده بيثبّت إن المفتاح <b>متعامل معاه</b>، مش إن
        // ترتيبه معيّن — كل مفتاح ليه فحص لوحده فوق لما يلزم.
        Assert.Equal(2, sorted.Count);
    }

    [Fact]
    public void Sorting_by_code_and_by_problems_does_what_it_says()
    {
        var rows = TechnicianProductivity.Rows(
            [
                Testing("T003", "ج", total: 10, fail: 5),
                Testing("T001", "أ", total: 10, fail: 1, error: 1),
                Testing("T002", "ب", total: 10),
            ],
            new Dictionary<string, RepairProductivityFacts>());

        Assert.Equal(
            ["T001", "T002", "T003"],
            TechnicianProductivity.Sorted(rows, "code").Select(r => r.Code));

        // ⚠️ المشاكل = فشل + خطأ قراءة.
        Assert.Equal(
            ["T003", "T001", "T002"],
            TechnicianProductivity.Sorted(rows, "problems").Select(r => r.Code));
    }

    /// <summary>
    /// ⚠️ وأي مفتاح مش معروف بيرجع للافتراضي <b>في صمت</b> — زي
    /// باقي مفاتيح الفترة في المشروع.
    /// </summary>
    [Fact]
    public void An_unknown_sort_key_falls_back_silently()
    {
        var rows = TechnicianProductivity.Rows(
            [Testing("T-BUSY", "مشغول", total: 10), Testing("T-IDLE", "فاضي", total: 1)],
            new Dictionary<string, RepairProductivityFacts>());

        Assert.Equal(
            TechnicianProductivity.Sorted(rows, null).Select(r => r.Code),
            TechnicianProductivity.Sorted(rows, "كلام").Select(r => r.Code));
    }
}
