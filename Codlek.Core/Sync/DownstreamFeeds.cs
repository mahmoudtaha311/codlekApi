namespace Codlek.Core.Sync;

/// <summary>
/// التغذيات النازلة اللي السيرفر بيقدّمها — <b>أسماء مجمّدة</b>.
///
/// <para>🔴 <b>وقبل كده المزامنة كانت رفع بس.</b> أمر الصيانة
/// بيتفتح على راكة الفحص وبيتسند لفني صيانة بيشتغل على راكة تانية
/// — وأمره ماكانش بيوصله <b>أبداً</b>. الأمر بيترفع للسيرفر ويقعد
/// هناك، والمدير شايفه على الموقع، والفني قدام راكته مش شايف
/// حاجة.</para>
///
/// <para>⚠️ <b>والقناة دي ضيقة عن قصد.</b> بتنقل اللي السيرفر صاحبه
/// (الإسناد) واللي الراكة محتاجاه عشان تعرض الأمر — وبس. شغل
/// الصيانة نفسه (الحالة، التوقيتات، القطع، الملاحظات) بيفضل ملك
/// الراكة وبيمشي لفوق زي ما هو. لو نزل تاني كان هيدهس شغل لسه
/// مارفعش.</para>
/// </summary>
public static class DownstreamFeeds
{
    /// <summary>قايمة فنيي الصيانة في الشركة — عشان الإسناد أوفلاين.</summary>
    public const string RepairRoster = "repair-roster";

    /// <summary>أوامر الصيانة المسنودة — عشان توصل راكة الفني.</summary>
    public const string RepairAssigned = "repair-assigned";

    /// <summary>حاويات الاستيراد.</summary>
    public const string Containers = "containers";

    /// <summary>
    /// ⚠️ <b>الترتيب ده جزء من العقد.</b> فيه فحص في القديم بيقارن
    /// القايمة كاملة بالترتيب — والراكة بتخزّنها على القرص.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
        [RepairRoster, RepairAssigned, Containers];
}
