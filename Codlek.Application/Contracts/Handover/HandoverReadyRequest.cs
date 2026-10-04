namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// علّم «جاهز للتسليم» أو شيل العلامة.
/// </summary>
/// <param name="Ready">
/// 🔴 <b>شيل العلامة مالوش شروط. حطّها ليها شروط.</b>
///
/// <para>الرجوع عن غلطة لازم يفضل ممكن دايماً: لاب اتعلّم جاهز
/// وبعدين اتفتحله أمر صيانة مبقاش «مؤهّل» — ولو منعنا شيل العلامة
/// عنه، بيفضل معلّم غلط لحد ما الصيانة تخلص.</para>
/// </param>
public sealed record HandoverReadyRequest(List<Guid>? DeviceIds, bool Ready);
