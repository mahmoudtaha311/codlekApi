namespace Codlek.Application.Contracts.Hardware;

/// <summary>
/// مقارنة لقطتين على نفس اللاب.
///
/// <para>🔴 <b>الحساب كله على السيرفر، وممنوع إعادة تنفيذه في
/// الواجهة.</b> القواعد دي بتتحوّل لاتهام إن موظف غيّر قطعة. نسخة
/// تانية منها في الواجهة معناها نسختين من الحكم، وواحدة منهم هتختلف
/// عن التانية أول ما حد يعدّل واحدة بس.</para>
/// </summary>
/// <param name="Left">
/// ⚠️ <b>الأقدم دايماً.</b> المقارنة اتجاهية — «اتضافت» و«اتشالت»
/// بيتقلبوا لو الترتيب اتعكس، والمستخدم ممكن يختار الأحدث في الخانة
/// الأولى من غير ما ياخد باله.
/// </param>
/// <param name="EitherPartial">
/// 🔴 واحدة من اللقطتين ناقصة؟ — ساعتها كل كلمة «اتشالت» في الجدول
/// مشكوك فيها، والشاشة لازم تقول كده فوق.
/// </param>
public sealed record CompareResponse(
    Guid DeviceId,
    string DevicePublicCode,
    CompareSideInfo Left,
    CompareSideInfo Right,
    CompareSummary Summary,
    string? MajorWarning,
    bool EitherPartial,
    IReadOnlyList<ComponentDiffItem> Components,
    IReadOnlyList<StepDiffItem> Steps);
