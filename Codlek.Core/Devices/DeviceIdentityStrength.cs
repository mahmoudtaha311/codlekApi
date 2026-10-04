using Codlek.Core.Enums;

namespace Codlek.Core.Devices;

/// <summary>
/// ترتيب قوة مراسي الهوية — <b>ومطابق للراكة بالحرف</b>.
///
/// <para>🔴 <b>والترتيب ده عقد مش رأي.</b> المطابقة بتمشي على
/// المراسي بالترتيب ده وبتوقف عند أول واحدة بتدّي نتيجة واحدة. أي
/// اختلاف بين السيرفر والراكة معناه إن <b>نفس اللاب بياخد هوية
/// مختلفة حسب مين اللي طابق</b> — والنتيجة جهازين لنفس اللاب،
/// وتاريخه بيتقسم عليهم.</para>
///
/// <para>⚠️ <b>ومرساة واحدة بتشاور على جهازين = عيب بيانات.</b>
/// ساعتها بنسيب الفحص للمراجعة بدل ما نختار واحد عشوائي — واختيار
/// عشوائي هنا معناه إن فحص بيروح لجهاز غلط ومحدّش بيعرف.</para>
/// </summary>
public static class DeviceIdentityStrength
{
    /// <summary>
    /// من الأقوى للأضعف.
    ///
    /// <para>⚠️ <c>DiskSerial</c> آخر واحدة عن قصد: الهارد بيتنقل بين
    /// لابات، فهو أضعف دليل على «ده نفس الجهاز» — بس لسه أقوى من
    /// مفيش.</para>
    /// </summary>
    public static readonly IReadOnlyList<DeviceIdentifierKind> Order =
    [
        DeviceIdentifierKind.SystemUuid,
        DeviceIdentifierKind.BiosSerial,
        DeviceIdentifierKind.BoardSerial,
        DeviceIdentifierKind.DiskSerial,
    ];

    /// <summary>
    /// ⚠️ <b>اتنين كفاية.</b> إحنا بنسأل «واحد ولا أكتر؟» — والعدّ
    /// الكامل رحلة زيادة على مرساة ممكن تكون على مية جهاز.
    /// </summary>
    public const int ProbeTake = 2;
}
