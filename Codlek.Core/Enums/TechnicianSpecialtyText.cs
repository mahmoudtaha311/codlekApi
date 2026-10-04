namespace Codlek.Core.Enums;

/// <summary>
/// تخصص الفني بالعربي — <b>مصدر واحد</b>.
///
/// <para>🔴 النصوص دي بتتعرض في المنسدلة اللي المحاسب بيختار منها
/// الفني، وفي ملفات التصدير، وفي سجل الإجراءات («التخصص: بوردات»).
/// فأي تغيير هنا بيبان في تلات أماكن على طول.</para>
///
/// <para>⚠️ وكانت مكتوبة في <c>ApiV1.cs</c> كدالة خاصة وبتتنده من
/// أربع حاجات — فالتكرار كان مسألة وقت.</para>
/// </summary>
public static class TechnicianSpecialtyText
{
    public static string Arabic(TechnicianSpecialty specialty) => specialty switch
    {
        TechnicianSpecialty.Testing => "فحص",
        TechnicianSpecialty.Boards => "بوردات",
        TechnicianSpecialty.Batteries => "بطاريات",
        TechnicianSpecialty.Screens => "شاشات",
        TechnicianSpecialty.Refinishing => "تجديد",
        TechnicianSpecialty.Grading => "تصنيف",

        // ⚠️ `None` بتوصل هنا — وهي الوضع الطبيعي لفني مفتوح على كل
        // حاجة، مش حالة ناقصة.
        _ => "غير محدد",
    };
}
