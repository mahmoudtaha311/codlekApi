using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// ربط معرّف جهاز محلي على راكة بالجهاز الحقيقي على السيرفر.
///
/// <para>🔴 <b>ليه ده لازم.</b> الراكة بتشتغل أوفلاين وبتولّد معرّف
/// الجهاز محلياً. راكة تانية بتفحص نفس اللاب بتولّد معرّف تاني خالص.
/// السيرفر بيتعرّف على اللاب من المراسي ويقرّر إن الاتنين نفس الجهاز —
/// بس الراكة التانية هتفضل تبعت بمعرّفها هي في كل مزامنة جاية.</para>
///
/// <para>الصف ده بيخلّي السيرفر يترجم المعرّف ده للجهاز الكانوني
/// <b>من غير ما يطلب من الراكة تغيّر حاجة</b> — وده شرط عشان
/// الأوفلاين يفضل شغّال زي ما هو.</para>
///
/// <para>⚠️ append-only عملياً: مابنغيّرش الربط بعد ما يتعمل إلا في
/// دمج صريح.</para>
/// </summary>
public class DeviceAlias
{
    /// <summary>المعرّف اللي الراكة بتبعت بيه — هو المفتاح.</summary>
    public Guid AliasDeviceId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الجهاز الحقيقي على السيرفر.</summary>
    public Guid CanonicalDeviceId { get; set; }
    public Device? CanonicalDevice { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(300)]
    public string Reason { get; set; } = "";

    /// <summary>الراكة اللي جابت المعرّف ده.</summary>
    public Guid? SourceRackId { get; set; }
}
