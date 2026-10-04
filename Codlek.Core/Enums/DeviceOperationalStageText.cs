namespace Codlek.Core.Enums;

/// <summary>
/// مرحلة اللاب بالعربي.
///
/// <para>⚠️ <b>دي أسماء عرض مش مفاتيح.</b> الفلترة والمقارنة كلها
/// على الـenum؛ النص ده مابيتقارنش ومابيتخزّنش، فتعديله تعديل لغة
/// بس.</para>
///
/// <para>⚠️ والمجهول ومرحلة من نسخة أحدث بياخدوا نفس الرد: «مش
/// معروف». الحاجة اللي مالهاش اسم تفضل <b>ظاهرة</b>،
/// مابتختفيش.</para>
/// </summary>
public static class DeviceOperationalStageText
{
    public static string Arabic(DeviceOperationalStage stage) => stage switch
    {
        DeviceOperationalStage.Received => "مستلم",
        DeviceOperationalStage.Testing => "بيتفحص",
        DeviceOperationalStage.Tested => "اتفحص",
        DeviceOperationalStage.NeedsRepair => "محتاج صيانة",
        DeviceOperationalStage.UnderRepair => "في الصيانة",
        DeviceOperationalStage.Ready => "جاهز",
        DeviceOperationalStage.WithSales => "مع المبيعات",
        DeviceOperationalStage.AtPointOfSale => "في نقطة بيع",
        DeviceOperationalStage.Returned => "مرتجع",
        _ => "مش معروف",
    };
}
