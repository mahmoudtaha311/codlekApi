namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// ملاحظة على اللاب.
///
/// <para>⚠️ <b>ومفيش نقطة كتابة للملاحظات في المشروع ده.</b>
/// الملاحظات بتتكتب من خدمات (تسليم، صيانة، دمج هوية) مش من
/// المستخدم مباشرةً — فالعقد ده قراية بس.</para>
/// </summary>
public sealed record DeviceNoteItem(
    long Id,
    string Body,
    string CreatedByName,
    DateTime CreatedAtUtc);
