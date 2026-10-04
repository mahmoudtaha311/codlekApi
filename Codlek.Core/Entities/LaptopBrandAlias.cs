using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// اسم تاني لنفس الماركة — <c>Hewlett-Packard</c> ← <c>HP</c>.
///
/// <para>🔴 <b>جدول مش عمود بفواصل، عشان التفرّد يتفرض في قاعدة
/// البيانات.</b> الاسم البديل لازم يكون فريد على مستوى <b>الشركة
/// كلها</b> مش جوّه الماركة الواحدة: لو ماركتين ادّعوا «HP»، الحل
/// بيبقى معتمد على الترتيب — ونفس اللاب بيتحل لماركة مختلفة بعد ما
/// حد يغيّر ترتيب العرض. وعمود بفواصل مينفعش يتعمل عليه فهرس.</para>
/// </summary>
public class LaptopBrandAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid BrandId { get; set; }
    public LaptopBrand? Brand { get; set; }

    /// <summary>زي ما صاحب الشغل كتبه.</summary>
    [MaxLength(80)]
    public string RawValue { get; set; } = "";

    /// <summary>بعد التوحيد — ده اللي بيتقارن بيه.</summary>
    [MaxLength(80)]
    public string NormalizedValue { get; set; } = "";
}
