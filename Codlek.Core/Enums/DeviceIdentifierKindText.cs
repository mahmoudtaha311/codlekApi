namespace Codlek.Core.Enums;

/// <summary>
/// اسم نوع مرسى الهوية بالعربي.
///
/// <para>⚠️ <b>والخريطة دي كانت عايشة على موديل صفحة Razor في
/// القديم</b> (<c>Pages/Devices/Details.cshtml.cs</c>) — يعني طبقة
/// العرض. والنقطة كانت بتناديها من هناك، فالمشروع الجديد مايقدرش
/// يشاور عليها خالص: هي في مشروع تاني بيتشال.</para>
///
/// <para>⚠️ <b>والافتراضي بيرجع اسم القيمة زي ما هو.</b> نوع جديد
/// بينضاف بكرة بيظهر <c>NvmeSerial</c> بالإنجليزي — وده أحسن من
/// «غير معروف»: اللي بيبص على مراسي لاب بيحتاج يعرف إن فيه مرسى
/// مش متعرّف، مش إنه مش موجود.</para>
/// </summary>
public static class DeviceIdentifierKindText
{
    public static string Arabic(DeviceIdentifierKind kind) => kind switch
    {
        DeviceIdentifierKind.SystemUuid => "معرّف النظام (UUID)",
        DeviceIdentifierKind.BiosSerial => "سيريال البيوس",
        DeviceIdentifierKind.BoardSerial => "سيريال البوردة",
        DeviceIdentifierKind.DiskSerial => "سيريال الهارد",
        DeviceIdentifierKind.MacAddress => "عنوان MAC",
        DeviceIdentifierKind.PanelEdidSerial => "سيريال الشاشة",
        DeviceIdentifierKind.BatterySerial => "سيريال البطارية",
        DeviceIdentifierKind.CompanyCode => "كود الشركة",
        _ => kind.ToString(),
    };
}
