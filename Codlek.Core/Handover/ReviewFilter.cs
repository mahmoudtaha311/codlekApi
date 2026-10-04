namespace Codlek.Core.Handover;

/// <summary>
/// أنهي لابات تظهر في قايمة المرشّحين.
///
/// <para>🔴 <b>التلاتة دول تلات شاشات مختلفة:</b></para>
/// <list type="bullet">
///   <item><see cref="NotReviewed"/> — اتفحص وعدّى، بس <b>محدش
///         راجعه</b>. دي قايمة صفحة المراجعة: «وريني اللي مستني حد
///         يقول عليه جاهز».</item>
///   <item><see cref="Reviewed"/> — اتراجع فعلاً. دي القايمة
///         الافتراضية لصفحة التسليم، لأن الأصل إن اللي يتسلّم يكون
///         مراجَع.</item>
///   <item><see cref="Any"/> — للبحث الاستثنائي: المدير بيدوّر
///         بالكود على لاب مستعجل مش مراجَع عشان يسلّمه بسبب
///         مكتوب.</item>
/// </list>
///
/// <para>⚠️ <b>والأسماء دي مش على السلك.</b> اللي على السلك هو نص
/// <c>review=pending|ready|all</c> اللي الداش بورد بتبعته —
/// والتحويل في <c>HandoverPolicy.Review</c>. فإعادة تسمية القيم
/// هنا مابتكسرش حاجة.</para>
/// </summary>
public enum ReviewFilter
{
    /// <summary>مستني مراجعة — <c>review=pending</c>.</summary>
    NotReviewed,

    /// <summary>اتراجع — <c>review=ready</c>، وده الافتراضي.</summary>
    Reviewed,

    /// <summary>الكل — <c>review=all</c>.</summary>
    Any
}
