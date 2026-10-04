using Codlek.Core.Text;

namespace Codlek.Core.Repairs;

/// <summary>
/// نص البحث على أمر الصيانة.
///
/// <para>🔴 <b>فيه كاتبين للعمود ده وهما مش متفقين — والاتنين
/// موجودين في بيانات الإنتاج.</b> مسار الموقع بيحط كود الجهاز،
/// ومسار الراكة بيحط اللي اتعمل في الصيانة. ماتوحّدهمش من غير قرار:
/// التوحيد معناه إن صفوف قديمة تبقى غير قابلة للبحث باللي
/// بتتبحث بيه النهاردة.</para>
/// </summary>
public static class RepairSearchText
{
    /// <summary>
    /// مسار الموقع — <c>(رقم الأمر · كود الجهاز · العطل · مين فتحه)</c>.
    /// </summary>
    public static string FromWeb(
        string publicCode, string? deviceCode, string? faultSummary, string? openedByName) =>
        ArabicText.Combine(publicCode, deviceCode, faultSummary, openedByName);

    /// <summary>
    /// مسار الراكة — <c>(رقم الأمر · العطل · اللي اتعمل · مين فتحه)</c>.
    ///
    /// <para>⚠️ <b>مفيش كود جهاز هنا.</b> كده في المشروع القديم
    /// بالحرف، والاختلاف ده حيّ في القاعدة.</para>
    /// </summary>
    public static string FromRack(
        string publicCode, string? faultSummary, string? repairActions, string? openedByName) =>
        ArabicText.Combine(publicCode, faultSummary, repairActions, openedByName);
}
