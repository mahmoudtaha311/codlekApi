using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// ملاحظة بشرية على جهاز.
///
/// <para><b>بتتضاف وبس.</b> مفيش تعديل ومفيش مسح. التصحيح
/// بيتكتب كملاحظة جديدة — لأن اللي اتكتب عن جهاز في وقته
/// جزء من تاريخه؛ تغييره بعدين بيخلي السجل يقول حاجة ماحصلتش.</para>
///
/// <para>⚠️ <b>دي ملاحظات، مش أوامر شغل.</b> مفيش إسناد ولا حالة
/// ولا قطع غيار ولا مرفقات. أول ما حاجة من دي تتضاف هنا يبقى ده
/// نظام صيانة متخبّي في جدول ملاحظات، ومكانه مرحلة الصيانة.</para>
/// </summary>
public class DeviceNote
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    [MaxLength(2000)]
    public string Body { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>مين كتبها — من الجلسة على السيرفر، مش من المتصفح.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// اسمه وقت الكتابة — لقطة مقصودة.
    ///
    /// <para>لو الحساب اتمسح أو اسمه اتغير، الملاحظة بتفضل
    /// بتقول مين كتبها يومها. ربط بمفتاح أجنبي بس كان هيخلي
    /// السجل يتغير بأثر رجعي.</para>
    /// </summary>
    [MaxLength(120)]
    public string CreatedByName { get; set; } = "";
}
