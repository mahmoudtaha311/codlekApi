namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// نتيجة <b>آخر</b> فحص.
///
/// <para>🔴 <b>آخر فحص، مش أي فحص.</b> لاب باظ الشهر اللي فات
/// واتصلّح النهاردة <b>مش</b> في «فيه مشكلة» — والعكس كمان: لاب
/// اتفحص نضيف وبعدين باظ مش «سليم». وده نفس تعريف شاشة التسليم
/// بالحرف.</para>
///
/// <para>⚠️ و<see cref="Clean"/> بتشترط إن فيه فحص أصلاً: مفيش
/// فحص ≠ سليم.</para>
/// </summary>
public enum DeviceOutcomeFilter
{
    Any,

    /// <summary><c>outcome=failures</c> — آخر فحص فيه فشل.</summary>
    Failures,

    /// <summary><c>outcome=errors</c> — آخر فحص فيه خطأ قراءة.</summary>
    Errors,

    /// <summary><c>outcome=clean</c> — اتفحص، وآخر فحص نضيف.</summary>
    Clean,

    /// <summary><c>outcome=never</c> — عمره ما اتفحص.</summary>
    NeverTested
}
