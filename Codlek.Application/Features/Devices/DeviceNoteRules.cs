namespace Codlek.Application.Features.Devices;

/// <summary>
/// قواعد ملاحظة اللاب.
/// </summary>
public static class DeviceNoteRules
{
    /// <summary>
    /// أطول ملاحظة مقبولة — <b>نفس <c>[MaxLength]</c> على
    /// العمود</b>، ونفس <c>MaxNoteLength</c> في القديم.
    ///
    /// <para>⚠️ والقياس على النص <b>بعد</b> القص من الطرفين — زي
    /// القديم بالظبط: ٢٠٠٠ حرف ومعاهم مسافات في الآخر بتعدّي.</para>
    /// </summary>
    public const int MaxLength = 2000;
}
