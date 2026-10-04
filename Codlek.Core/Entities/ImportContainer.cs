using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// حاوية استيراد — الشحنة اللي اللاب جه فيها.
///
/// <para>🔴 <b>دي مش مكان تخزين.</b> صاحب الشغل وضّحها بالنص:
/// «الحاوية دي اللاب كان جاي من الاستيراد فيها، وبتاخد رمز ومش
/// بتتغير». يعني صفة ثابتة للاب نفسه، مش حالة بتتنقل.</para>
///
/// <para>⚠️ <b>وعشان كده مفيش جدول حركة ولا تاريخ نقل.</b> الربط
/// عمود واحد على <c>Device</c> بيتكتب <b>مرة واحدة</b> — سجل حركة
/// لحاجة ثابتة تعقيد بلا مقابل.</para>
///
/// <para>⚠️ <b>وليه مش <see cref="Location"/>.</b> <c>LocationKind</c>
/// بيوصف خمس محطات في الورشة واللاب بيبقى في واحدة منهم؛ الحاوية
/// صندوق بيتعمل بالمئات. حطّها كصفوف <c>Warehouse</c> كان هيكبّر
/// الجدول بلا حدود ويفقد <c>Kind</c> معناه. الشكل مستعار، مش
/// الصفوف.</para>
/// </summary>
public class ImportContainer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// رمز الحاوية زي ما الفني كتبه — <c>CN-2026-014</c>.
    ///
    /// <para>🔴 <b>فريد داخل الشركة</b> بفهرس مفلتر على
    /// <c>[Code] &lt;&gt; ''</c> — نفس نمط الأكواد التانية في المخطّط.
    /// من غير التفرّد، «نفس الحاوية» بتبقى صفّين والمحتوى بينقسم.</para>
    /// </summary>
    [MaxLength(40)]
    public string Code { get; set; } = "";

    /// <summary>
    /// الشكل المطبّع للمقارنة — <c>ArabicText.Normalize</c> + حروف صغيرة.
    ///
    /// <para>⚠️ <b>هو اللي عليه فهرس التفرّد، مش <see cref="Code"/>.</b>
    /// الفني بيكتب <c>cn-2026-014</c> و<c>CN-2026-014</c> و
    /// <c>CN‑2026‑014</c> بمسافات زيادة — من غير التطبيع دول تلات
    /// حاويات.</para>
    /// </summary>
    [MaxLength(40)]
    public string NormalizedCode { get; set; } = "";

    /// <summary>اسم وصفي اختياري — «شحنة مارس».</summary>
    [MaxLength(120)]
    public string Name { get; set; } = "";

    /// <summary>
    /// الحاوية بتتوقف، مابتتمسحش.
    ///
    /// <para>⚠️ فيه لابات بتشاور عليها؛ مسحها بيقطع نسب.</para>
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>مين عملها — «مزامنة الراكة» لو اتعملت من فحص.</summary>
    [MaxLength(120)]
    public string CreatedByName { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
