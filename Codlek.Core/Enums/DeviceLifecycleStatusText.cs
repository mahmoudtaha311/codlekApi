namespace Codlek.Core.Enums;

/// <summary>
/// حالة دورة حياة اللاب بالعربي.
///
/// <para>⚠️ والقيمة المجهولة بترجع باسمها الإنجليزي — دي الحاجة
/// الوحيدة هنا اللي مش بالعربي، ومنقولة زي ما هي: حالة من نسخة أحدث
/// لازم تفضل <b>ظاهرة</b> مش تختفي في «غير معروف».</para>
/// </summary>
public static class DeviceLifecycleStatusText
{
    public static string Arabic(DeviceLifecycleStatus status) => status switch
    {
        DeviceLifecycleStatus.Active => "نشط",
        DeviceLifecycleStatus.DuplicateSuspected => "يُشتبه إنه مكرر",
        DeviceLifecycleStatus.Merged => "مدموج",
        DeviceLifecycleStatus.Retired => "متقاعد",
        _ => status.ToString(),
    };
}
