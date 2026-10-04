namespace Codlek.Core.Enums;

/// <summary>
/// حركة السجل التشغيلي بالعربي.
///
/// <para>⚠️ <b>المجهول بيظهر زي ما هو.</b> حركة من نسخة أحدث لازم
/// تفضل مرئية على الخط الزمني مش تختفي.</para>
///
/// <para>🔴 و<c>RepairCompleted</c> بتتعرض «خلصت الصيانة» <b>للنجاح
/// وللتعذّر على السواء</b> — النوع لوحده مش بيفرّق، اللي بيفرّق
/// <c>ToStage</c> (جاهز مقابل بحاجة إلى صيانة).</para>
/// </summary>
public static class DeviceWorkflowEventTypeText
{
    public static string Of(DeviceWorkflowEventType eventType) => eventType switch
    {
        DeviceWorkflowEventType.SentToRepair => "اتبعت للصيانة",
        DeviceWorkflowEventType.RepairStarted => "بدأت الصيانة",
        DeviceWorkflowEventType.RepairCompleted => "خلصت الصيانة",
        DeviceWorkflowEventType.CustodyHandoff => "تسليم",
        DeviceWorkflowEventType.LocationMoved => "نقل مكان",
        DeviceWorkflowEventType.StageChanged => "تغيير مرحلة",
        _ => eventType.ToString(),
    };
}
