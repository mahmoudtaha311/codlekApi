namespace Codlek.Core.Hardware;

/// <summary>
/// أرقام فئات المكوّنات — <b>زي ما الراكة بتكتبها</b>.
///
/// <para>🔴 <b>الأرقام دي عقد.</b> الراكة بتكتب <c>Type</c> كرقم في
/// صف اللقطة، والصفوف دي عايشة في الإنتاج. تغيير رقم معناه إن
/// لقطات قديمة تتقرا غلط — «الذاكرة» تبان «البطارية».</para>
///
/// <para>⚠️ <b>والنص اسم عرض، مش مفتاح.</b> المطابقة والمقارنة كلها
/// بتشتغل على الرقم؛ النص مابيتقارنش ومابيتخزّنش، فتعديله تعديل لغة
/// بس.</para>
///
/// <para>⚠️ <b>والقيم التقنية مابتتترجمش:</b> «DDR4» و«NVMe»
/// و«BIOS» بتفضل زي ما هي — دي أسماء مواصفات، وترجمتها بتخلّي الفني
/// مش لاقي اللي شايفه على القطعة.</para>
/// </summary>
public static class ComponentType
{
    public const int System = 0;
    public const int Motherboard = 1;
    public const int Bios = 2;
    public const int Cpu = 3;
    public const int Memory = 4;
    public const int Storage = 5;
    public const int Battery = 6;
    public const int Display = 7;
    public const int Gpu = 8;
    public const int Network = 9;
    public const int Keyboard = 10;
    public const int Touchpad = 11;
    public const int Camera = 12;
    public const int Audio = 13;

    /// <summary>
    /// اسم الفئة بالعربي المهني.
    ///
    /// <para>⚠️ «اللوحة الأم» مش «البوردة»، و«الذاكرة» مش «الرام» —
    /// الكلمات المنقولة بالحروف بتبان دارجة في واجهة بتتعرض على صاحب
    /// المحل وعلى الزبون.</para>
    /// </summary>
    public static string Arabic(int type) => type switch
    {
        System => "معلومات النظام",
        Motherboard => "اللوحة الأم",
        Bios => "BIOS",
        Cpu => "المعالج",
        Memory => "الذاكرة",
        Storage => "وحدات التخزين",
        Battery => "البطارية",
        Display => "الشاشات",
        Gpu => "معالج الرسوميات",
        Network => "الشبكة",
        Keyboard => "لوحة المفاتيح",
        Touchpad => "لوحة اللمس",
        Camera => "الكاميرا",
        Audio => "الصوت",

        // ⚠️ فئة من نسخة راكة أحدث بتبان برقمها بدل ما الصفحة توقع.
        _ => "نوع " + type
    };
}
