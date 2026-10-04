using Codlek.Core.Text;

namespace Codlek.Core.Repairs;

/// <summary>
/// قيد الماركة على أوامر الصيانة — <b>دالة نقية</b>.
///
/// <para>🔴 <b>القاعدة دي بتتفحص في مكان واحد بس، والفحوص القديمة
/// بتثبّتها بتمان حالات.</b> أي خلل فيها بيبان كـ«الفني مش عارف يمسك
/// لاب من حقه» — أو أسوأ، «فني مسك لاب مش من تخصصه وبوّظه».</para>
///
/// <para>⚠️ <b>والقيد على <i>مسك</i> الأمر بس، مش على قفله.</b> سيبت
/// السبب مكتوب في <see cref="CanComplete"/> لأنه السؤال اللي بيتكرر.</para>
/// </summary>
public static class RepairBrandGate
{
    /// <summary>
    /// الفني ده يقدر ياخد اللاب ده؟
    /// </summary>
    /// <param name="allowedBrandIds">
    /// ماركات الفني. <b>فاضية معناها مفيش قيد خالص</b> — مش معناها
    /// «ممنوع من كل حاجة».
    /// </param>
    /// <param name="resolved">ناتج <see cref="BrandToken.Resolve"/> للمصنّع الخام.</param>
    /// <param name="approverOverride">
    /// 🔴 المحاسب بيعدّي القاعدة. <b>والتعدية لازم تتسجّل</b> — تعدية
    /// مابتتسجّلش معناها إن القاعدة مالهاش أي معنى.
    /// </param>
    public static bool CanAssign(
        IReadOnlyCollection<Guid> allowedBrandIds,
        BrandResult resolved,
        bool approverOverride = false)
    {
        if (approverOverride) return true;

        return BrandToken.Allows(allowedBrandIds, resolved);
    }

    /// <summary>
    /// 🔴 <b>قفل الأمر عمره ما بيتمنع بالماركة.</b>
    ///
    /// <para>السيناريو اللي الفحص القديم بيثبّته: الفني اشتغل على
    /// اللاب، وبعدين المدير عدّل إعداداته وشال منه الماركة. ولو
    /// القفل اتمنع، الأمر بيفضل مفتوح <b>للأبد</b> — واللاب بيقعد في
    /// الورشة ومحدش يقدر يخرّجه.</para>
    ///
    /// <para>⚠️ الدالة دي بترجّع <c>true</c> دايماً، وموجودة عشان
    /// السؤال يتسأل في الكود مرة واحدة بإجابة مكتوبة — بدل ما حد
    /// يزوّد فحص ماركة على القفل وهو فاكر إنه بيسدّ ثغرة.</para>
    /// </summary>
    public static bool CanComplete() => true;
}
