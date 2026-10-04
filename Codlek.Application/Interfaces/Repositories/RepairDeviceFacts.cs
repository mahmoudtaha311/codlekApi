namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// بيانات عرض اللاب على شاشة الأمر.
///
/// <para>⚠️ <c>null</c> من المستودع معناها «الجهاز مش موجود» —
/// والشاشة بتعرض نصوص فاضية بدل خطأ. أمر صيانة لجهاز اتمسح لسه
/// ليه قيمة: هو سجل شغل اتعمل.</para>
/// </summary>
public sealed record RepairDeviceFacts(
    string PublicCode,
    string Manufacturer,
    string CommercialModelName,
    string RawModel);
