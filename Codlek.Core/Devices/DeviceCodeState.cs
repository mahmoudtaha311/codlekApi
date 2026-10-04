namespace Codlek.Core.Devices;

/// <summary>
/// حالة كود اللاب بالعربي.
///
/// <para>⚠️ <b>و<c>Device.CodeState</c> رقم خام مش <c>enum</c>.</b>
/// منقول زي ما هو: العمود في القاعدة <c>int</c> والراكة بتكتبه
/// كرقم، وتحويله لـ<c>enum</c> دلوقتي هجرة مالهاش مكسب — بس يعني
/// إن الترجمة لازم تتعامل مع أي رقم، مش مع قايمة مقفولة.</para>
///
/// <para>⚠️ <b>والخريطة دي كانت على موديل صفحة Razor في القديم</b>
/// (<c>Pages/Devices/Details.cshtml.cs</c>) — طبقة عرض في مشروع
/// بيتشال، فمفيش طريقة نشاور عليها.</para>
///
/// <para>⚠️ <b>والافتراضي بيرجع الرقم نفسه كنص.</b> حالة جديدة
/// بتظهر «3» بدل «غير معروف» — واللي بيبص على اللاب يعرف إن فيه
/// حالة مش متعرّفة بدل ما يفتكر إن مفيش.</para>
/// </summary>
public static class DeviceCodeState
{
    /// <summary>الراكة كانت أوفلاين وبلوك أكوادها خلص — فمفيش كود.</summary>
    public const int Pending = 0;

    /// <summary>كود من بلوك الراكة المحجوز — صالح بس لسه ماتأكّدش.</summary>
    public const int Provisional = 1;

    /// <summary>السيرفر أكّده — وعادةً تأكيد شكلي لأنه كان محجوز ليها أصلاً.</summary>
    public const int Confirmed = 2;

    public static string Arabic(int state) => state switch
    {
        Pending => "مستنّي — محطة الفحص كانت أوفلاين",
        Provisional => "مبدئي من أكواد محطة الفحص",
        Confirmed => "مؤكّد من السيرفر",
        _ => state.ToString(),
    };
}
