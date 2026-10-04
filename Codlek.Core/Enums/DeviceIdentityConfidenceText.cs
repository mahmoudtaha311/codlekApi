namespace Codlek.Core.Enums;

/// <summary>
/// درجة ثقة هوية اللاب بالعربي.
///
/// <para>⚠️ <b>والصياغة دي مش نفس صياغة لقطة العتاد.</b> هنا «ج —
/// كود متلزّق» (الكود اللي الورشة لزقته على اللاب)، وفي لقطة العتاد
/// «ج — مسار جهاز» (المسار اللي ويندوز شايف بيه القطعة). الاتنين
/// درجة «ج» وكل واحدة بتوصف حاجة تانية خالص — والدمج كان بيخلّي
/// الشاشتين يقولوا نفس الكلام على حاجتين مختلفتين.</para>
/// </summary>
public static class DeviceIdentityConfidenceText
{
    public static string Arabic(DeviceIdentityConfidence confidence) => confidence switch
    {
        DeviceIdentityConfidence.A => "أ — سيريال حقيقي",
        DeviceIdentityConfidence.B => "ب — بصمة",
        DeviceIdentityConfidence.C => "ج — كود متلزّق",
        _ => "مفيش",
    };
}
