namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// الأرقام الرئيسية فوق اللوحة.
/// </summary>
/// <param name="PassRate">
/// 🔴 <b>البسط فحوص صفر فشل <u>وصفر</u> خطأ قراءة.</b> فحص فيه خطوة
/// ما اشتغلتش <b>مش</b> ناجح — الجهاز مش متثبت إنه سليم، وحسابه
/// نجاح بيرفع الرقم بشغل ناقص.
/// </param>
/// <param name="AverageMinutes">
/// ⚠️ بيتحسب على الفحوص اللي ليها مدة بس. الفحوص المستوردة من
/// شيتات قديمة مدّتها صفر، وإدخالها في المتوسط بيجيبه لتحت من غير
/// أي معنى.
/// </param>
/// <param name="ActiveStations">
/// 🔴 <b>رقم على مستوى الشركة — <u>للمديرين وفوق</u>.</b> باقي
/// اللوحة بيتضيّق فالفني بيشوف فحوصاته هو؛ والعدّاد ده كان بيعدّي
/// من غير تضييق، فالفني كان بيعرف كام محطة في الورشة. بيرجع
/// <c>0</c> للفني — والنقطة نفسها مابترجّعش الرقم، مش الواجهة
/// اللي بتخفيه.
/// </param>
public sealed record DashboardKpis(
    int Reports,
    int DevicesTested,
    int NeedsReview,
    double PassRate,
    double AverageMinutes,
    int ActiveTechnicians,
    int ActiveStations);
