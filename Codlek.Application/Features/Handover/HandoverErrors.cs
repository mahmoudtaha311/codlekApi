using Codlek.Application.Abstractions;
using Codlek.Core.Handover;
using Codlek.Core.Text;

namespace Codlek.Application.Features.Handover;

/// <summary>
/// أسباب فشل نقط التسليم — <b>منقولة بالحرف</b>.
///
/// <para>⚠️ ومفيش نقطة واحدة فيهم بترجّع <c>404</c>: التسليم بياخد
/// قايمة معرّفات، والرد الصح على معرّف غلط هو رسالة بتقول أنهي
/// واحد.</para>
/// </summary>
public static class HandoverErrors
{
    public static readonly Error NothingSelected =
        new("handover.nothing_selected", "اختار لاب واحد على الأقل", 400);

    /// <summary>
    /// ⚠️ الرسالة بتقول الرقم عشان المدير يقسّم الدفعة بنفسه.
    /// </summary>
    public static readonly Error BatchTooBigForHandover =
        new("handover.batch_too_big",
            $"الدفعة كبيرة أوي — سلّم {Limit} لاب على الأكتر في المرة", 400);

    public static readonly Error BatchTooBigForReview =
        new("handover.review_batch_too_big",
            $"الدفعة كبيرة أوي — راجع {Limit} لاب على الأكتر في المرة", 400);

    public static readonly Error ReceiverRequired =
        new("handover.receiver_required", "اكتب اسم اللي استلم اللابات", 400);

    public static readonly Error DestinationNotFound =
        new("handover.destination_not_found", "الجهة دي مش موجودة", 400);

    /// <summary>
    /// ⚠️ الجهة الموقوفة مش جهة ناقصة — الرسالة بتقول اسمها عشان
    /// المدير يعرف إنه اختار الصح والمشكلة في حالتها.
    /// </summary>
    public static Error DestinationSuspended(string name) =>
        new("handover.destination_suspended", $"الجهة «{name}» موقوفة", 400);

    public static readonly Error DeviceMissing =
        new("handover.device_missing", "فيه لاب في القايمة مش موجود", 400);

    /// <summary>
    /// 🔴 <b>الرسالة بتقول أنهي لاب بالكود.</b>
    ///
    /// <para>«فيه لاب مش مؤهّل» بتخلّي اللي بيراجع يلغي الدفعة كلها
    /// وهو مش عارف ليه.</para>
    /// </summary>
    public static Error NotEligibleForHandover(IEnumerable<string> codes) =>
        new("handover.not_eligible",
            "اللابات دي مينفعش تتسلّم (آخر فحص فيه فشل أو خطأ، أو عمرها ما اتفحصت، "
            + "أو عليها صيانة مفتوحة، أو لسه مع فني): " + Join(codes), 400);

    public static Error NotEligibleForReview(IEnumerable<string> codes) =>
        new("handover.not_eligible_for_review",
            "اللابات دي مينفعش تتراجع (آخر فحص فيه مشكلة، أو عليها صيانة مفتوحة، "
            + "أو لسه مع فني): " + Join(codes), 400);

    /// <summary>
    /// 🔴 <b>الباب مفتوح بشرط إنه يتكتب.</b>
    ///
    /// <para>الباب المقفول بالكامل بيتحايل عليه: حالة مستعجلة
    /// والمراجعة مقفولة معناها إن حد هيعلّم «جاهز» على السريع عشان
    /// يعدّي — وساعتها المراجعة تبقى ختم مالوش معنى وإحنا مش عارفين
    /// إن ده حصل.</para>
    /// </summary>
    public static Error NotReviewed(IEnumerable<string> codes) =>
        new("handover.not_reviewed",
            "اللابات دي لسه محدش راجعها: " + Join(codes)
            + ". راجعها الأول، أو اكتب سبب التسليم من غير مراجعة "
            + $"({MinReason} حروف على الأقل).", 400);

    /// <summary>
    /// ⚠️ فشل الحركة بيرجّع نص الخدمة زي ما هو — هي اللي عارفة
    /// السبب (موقع مش موجود، جهاز مدموج، …).
    /// </summary>
    public static Error MoveRefused(string message) =>
        new("handover.move_refused", message, 400);

    private static string Join(IEnumerable<string> codes) => string.Join("، ", codes);

    /*
      🔴 **الأرقام عربية-هندية — ودي اتلقطت من فحص على HTTP.**

      رسايل القديم مكتوبة «٥٠٠ لاب» و«١٠ حروف». والنسخة الأولى هنا
      كانت بتحقن الثابت فيطلع «500» و«10» — نفس الطلب يدّي رسالة
      مختلفة من الشاشتين وهما على نفس القاعدة.
    */
    private static string Limit => ArabicDigits.Of(HandoverPolicy.BatchLimit);

    private static string MinReason => ArabicDigits.Of(HandoverPolicy.MinOverrideReason);
}
