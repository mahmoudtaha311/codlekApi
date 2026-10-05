namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// ملاحظة على اللاب.
///
/// <para>⚠️ <b>والملاحظات بتيجي من مصدرين:</b> خدمات (تسليم، صيانة،
/// دمج هوية)، والمدير بإيده من <c>POST /api/v1/devices/{id}/notes</c>
/// — ونفس العقد ده هو رد النقطة دي. ومفيش تعديل ولا مسح: التصحيح
/// ملاحظة جديدة.</para>
/// </summary>
public sealed record DeviceNoteItem(
    long Id,
    string Body,
    string CreatedByName,
    DateTime CreatedAtUtc);
