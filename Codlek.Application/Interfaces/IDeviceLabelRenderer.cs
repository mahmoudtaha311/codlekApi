namespace Codlek.Application.Interfaces;

/// <summary>
/// بيرسم ليبل الـQR بتاع اللاب.
///
/// <para>🔴 <b>وليه بورت.</b> مكتبة الـQR تفصيلة بنية تحتية — وهي
/// <b>نفس المكتبة ونفس الإعدادات اللي في القديم بالحرف</b>
/// (<c>Net.Codecrete.QrCodeGenerator</c> 3.2.1). ليبل اتطبع من
/// الشاشة القديمة وليبل من الجديدة لازم يبقوا نفس الرمز.</para>
/// </summary>
public interface IDeviceLabelRenderer
{
    /// <summary>
    /// رمز SVG للقيمة دي — <b>عنصر <c>&lt;svg&gt;</c> لوحده</b> من غير
    /// ترويسة XML.
    /// </summary>
    /// <param name="publicCode">
    /// ⚠️ <b>كود اللاب العام وبس، ومش فاضي.</b> المنادي هو اللي بيرفض
    /// الفاضي — الرسّام مايعرفش يقول «مفيش ليبل».
    /// </param>
    string Svg(string publicCode);
}
