using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Technicians;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Analytics;
using Codlek.Core.Technicians;
using Codlek.Core.Text;

namespace Codlek.Application.Features.Technicians;

/// <summary>
/// بناء جدول الإنتاجية — <b>مصدر واحد للشاشة وللملف</b>.
///
/// <para>🔴 <b>ليه اتفصل.</b> صاحب الشغل طلب تصدير إكسل لصفحة
/// الإنتاجية. ولو الملف اتبنى من استعلام تاني، أول اختلاف بين
/// الاتنين بيخلّي المدير يقارن الشاشة بالملف ويلاقي فرق مالوش
/// تفسير — وهو مش هيعرف أنهي واحد الصح.</para>
/// </summary>
internal static class TechnicianProductivity
{
    /// <summary>
    /// فترة شاشة الفنيين.
    ///
    /// <para>🔴 <b>الافتراضي هنا «من البداية»، والافتراضي على اللوحة
    /// ٣٠ يوم — والفرق مقصود.</b> اللوحة بتجاوب على «مين اشتغل الشهر
    /// ده»؛ والشاشة دي بتجاوب على «مين اشتغل أصلاً». فني آخر شغلانة
    /// ليه من تلات شهور لازم يفضل في القايمة وصفحته تفتح، وإلا
    /// بيختفي من النظام وهو لسه موظّف.</para>
    ///
    /// <para>⚠️ <b>و«all» بس هي اللي بتروح لـ«من البداية».</b>
    /// المفتاح الفاضي بيعدّي على القارئ العادي وبيرجع ٣٠ يوم — وده
    /// بالظبط اللي كان بيحصل في القديم، فمفيش سلوك بيتغيّر تحت أي
    /// مدخل كان شغّال قبل كده.</para>
    /// </summary>
    public static AnalyticsPeriod Window(string? range, DateTime? from, DateTime? to)
    {
        string key = (range ?? "all").Trim().ToLowerInvariant();

        return key == "all"
            ? AnalyticsPeriod.Everything()
            : AnalyticsPeriod.Resolve(key, from, to);
    }

    public static List<TechnicianListItem> Rows(
        IReadOnlyList<TestingProductivityFacts> testing,
        IReadOnlyDictionary<string, RepairProductivityFacts> repairs)
    {
        var seen = testing.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = testing
            .Select(t =>
            {
                var repair = repairs.TryGetValue(t.Code, out var r)
                    ? (r.Count, r.AverageMinutes)
                    : (Count: 0, AverageMinutes: 0d);

                return new TechnicianListItem(
                    Code: t.Code,

                    // ⚠️ «فني محطة T001» أنفع من «الاسم غير متاح»:
                    // الأولانية بتقول للمدير يدوّر فين.
                    Name: t.Name.Length > 0 ? t.Name : RackTechnicianLabel.Neutral(t.Code),

                    // ⚠️ علم صريح بدل ما الواجهة تخمّن من شكل الاسم.
                    NameAvailable: t.Name.Length > 0,

                    TotalReports: t.Total,
                    Counts: new TestCounts(t.Pass, t.Fail, t.Error, t.NotPresent, t.Skip),

                    AverageMinutes: t.Timed == 0
                        ? 0
                        : Math.Round(t.DurationMs / 60000.0 / t.Timed, 1),

                    LastAtUtc: t.LastAtUtc,
                    RepairsCompleted: repair.Count,
                    RepairAverageMinutes: repair.AverageMinutes);
            })
            .ToList();

        /*
          🔴 **الفني اللي عمل صيانة بس ومفيش فحوص لازم يبان.**

          الاستعلام الأساسي مبني من جدول **الفحوص**، فلو اتسيب زي ما
          هو، فني الصيانة اللي مابيفحصش **مش هيظهر في صفحة الإنتاجية
          خالص** — وده بالظبط الشكوى: «ولو أنا فني صيانة بردو يبانلي
          أي اللي اتعمل صيانة».
        */
        items.AddRange(repairs
            .Where(kv => !seen.Contains(kv.Key) && kv.Value.Count > 0)
            .Select(kv => new TechnicianListItem(
                Code: kv.Key,
                Name: kv.Value.Name.Length > 0
                    ? kv.Value.Name
                    : RackTechnicianLabel.Neutral(kv.Key),
                NameAvailable: kv.Value.Name.Length > 0,
                TotalReports: 0,
                Counts: new TestCounts(0, 0, 0, 0, 0),
                AverageMinutes: 0,
                LastAtUtc: kv.Value.LastAtUtc,
                RepairsCompleted: kv.Value.Count,
                RepairAverageMinutes: kv.Value.AverageMinutes)));

        return items;
    }

    /// <summary>
    /// بيضيّق القايمة على اللي اسمه أو كوده فيه اللي اتكتب.
    ///
    /// <para>🔴 <b>التطبيع العربي مش زينة.</b> اتجرّب على الترتيب
    /// <c>Arabic_CI_AI</c> إن «أحمد» <b>مش</b> بتساوي «احمد» —
    /// الهمزة حرف مستقل في يونيكود مش علامة تشكيل، فمفيش collation
    /// بيشيلها. ومن غير التطبيع المدير بيكتب «احمد» وبيلاقي القايمة
    /// فاضية ويفتكر إن الفني مالوش شغل.</para>
    ///
    /// <para>🔴 <b>والتطبيع بيتعمل على الطرفين.</b> الأسماء جاية خام
    /// من الفحص ومن جدول الفنيين — <b>ولا واحد فيهم عمود
    /// متطبّع</b>. وتطبيع اللي اتكتب وبس بيقارن «احمد» بـ«أحمد» الخام
    /// ويفشل بردو.</para>
    ///
    /// <para>⚠️ <b>والفلترة هنا مش في SQL بقرار، مش بنسيان.</b>
    /// التطبيع مالوش ترجمة لـSQL، وحطّه في <c>Where</c> كان هيخلّي
    /// EF يجيب الجدول كله في صمت. واللي بيتفلتر هنا هو ناتج
    /// <b>التجميع</b> — صف لكل فني، عشرات مش آلاف — فالقاعدة «جمّع
    /// في SQL» مالمستهاش.</para>
    /// </summary>
    public static List<TechnicianListItem> Matching(
        List<TechnicianListItem> items, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return items;

        string needle = ArabicText.Normalize(search);

        if (needle.Length == 0) return items;

        return items
            .Where(i =>
                ArabicText.Normalize(i.Name).Contains(needle, StringComparison.Ordinal)

                // ⚠️ والكود بيعدّي على نفس التطبيع: بيحوّل الأرقام
                // العربية-الهندية لأرقام لاتينية، فالبحث بالكود
                // شغّال بأي كيبورد.
                || ArabicText.Normalize(i.Code).Contains(needle, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// ترتيب الجدول — <b>دالة نقية</b>.
    ///
    /// <para>🔴 <b>المفاتيح دي بروتوكول مش نص للعرض.</b> الواجهة
    /// بتبعتها زي ما هي، والملف المصدَّر بيستعملها كمان — فالاسم
    /// المعروض للمستخدم بيتغيّر في الواجهة، والمفتاح هنا ثابت.</para>
    ///
    /// <para>⚠️ وأي مفتاح مش معروف بيرجع للافتراضي (الأشغل الأول)
    /// <b>في صمت</b> — زي باقي مفاتيح الفترة في المشروع.</para>
    /// </summary>
    public static List<TechnicianListItem> Sorted(
        List<TechnicianListItem> items, string? sort) =>
        (sort ?? "").Trim().ToLowerInvariant() switch
        {
            "name" => items.OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList(),

            "code" => items.OrderBy(i => i.Code, StringComparer.Ordinal).ToList(),

            "tests" => items.OrderByDescending(i => i.TotalReports)
                .ThenBy(i => i.Name, StringComparer.CurrentCulture).ToList(),

            "testtime" => items.OrderByDescending(i => i.AverageMinutes).ToList(),

            "repairs" => items.OrderByDescending(i => i.RepairsCompleted)
                .ThenBy(i => i.Name, StringComparer.CurrentCulture).ToList(),

            "repairtime" => items.OrderByDescending(i => i.RepairAverageMinutes).ToList(),

            /*
              ⚠️ **اللي مالوش نشاط بيروح الآخر لوحده:** في .NET
              الـ`null` أصغر من أي قيمة، فالترتيب التنازلي بيحطّها في
              الذيل.

              🔴 **وده مضمون في الذاكرة بس.** لو الترتيب ده اتنقل
              لـSQL يوم، فيه مزوّدين بيحطّوا `NULL` **الأول** في
              التنازلي — واللي عمره ما اشتغل بيدفن اللي بيشتغل.
              وفيه فحص بيثبّت الترتيب ده عشان النقلة متعديش في صمت.
            */
            "recent" => items.OrderByDescending(i => i.LastAtUtc).ToList(),

            "problems" => items.OrderByDescending(i => i.Counts.Fail + i.Counts.Error)
                .ThenByDescending(i => i.TotalReports).ToList(),

            // الافتراضي: الأشغل الأول (فحص + صيانة).
            _ => items.OrderByDescending(i => i.TotalReports + i.RepairsCompleted)
                .ThenBy(i => i.Name, StringComparer.Ordinal).ToList(),
        };
}
