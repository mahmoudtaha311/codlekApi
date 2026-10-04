using Codlek.Application.Contracts.Analytics;

namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// صف في جدول الإنتاجية.
/// </summary>
/// <param name="NameAvailable">
/// ⚠️ <b>علم صريح بدل ما الواجهة تخمّن من شكل الاسم.</b> الاسم
/// البديل («فني محطة T001») اسم صالح للعرض، فمقارنته بنص ثابت في
/// الواجهة كانت بتكسر أول ما الصياغة تتغيّر.
/// </param>
/// <param name="RepairsCompleted">
/// 🔴 <b>والفني اللي عمل صيانة بس لازم يبان.</b> الاستعلام الأساسي
/// مبني من جدول <b>الفحوص</b>، فلو اتسيب زي ما هو فني الصيانة اللي
/// مابيفحصش <b>مش هيظهر في الصفحة خالص</b> — وده بالظبط اللي صاحب
/// الشغل اشتكى منه: «ولو أنا فني صيانة بردو يبانلي أي اللي اتعمل
/// صيانة».
/// </param>
public sealed record TechnicianListItem(
    string Code,
    string Name,
    bool NameAvailable,
    int TotalReports,
    TestCounts Counts,
    double AverageMinutes,
    DateTime? LastAtUtc,
    int RepairsCompleted = 0,
    double RepairAverageMinutes = 0);
