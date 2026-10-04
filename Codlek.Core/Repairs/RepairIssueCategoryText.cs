namespace Codlek.Core.Repairs;

/// <summary>
/// اسم مجموعة العطل بالعربي.
///
/// <para>⚠️ <b>المجهول بيظهر زي ما هو</b> — مجموعة جديدة من نسخة
/// أحدث لازم تفضل مرئية مش تختفي.</para>
///
/// <para>⚠️ و<c>ToLowerInvariant</c> مش <c>ToLower</c>: سيرفر على
/// لغة تركية بيحوّل الـ<c>I</c> غلط.</para>
/// </summary>
public static class RepairIssueCategoryText
{
    public static string Of(string? category) => (category ?? "").ToLowerInvariant() switch
    {
        "audio" => "الصوت",
        "ports" => "المنافذ",
        "display" => "الشاشة",
        "keyboard" => "الكيبورد",
        "battery" => "البطارية",
        "storage" => "التخزين",
        "network" => "الشبكة",
        "camera" => "الكاميرا",
        "microphone" => "الميكروفون",
        "touchpad" => "التاتش باد",
        "" => "",
        _ => category ?? "",
    };
}
