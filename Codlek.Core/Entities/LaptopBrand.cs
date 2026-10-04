using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// ماركة لابات — <b>قايمة صاحب الشغل بيديرها بإيده</b>.
///
/// <para>🔴 <b>ليه قايمة مُدارة مش استنتاج من البيانات.</b> اسم
/// الماركة في جدول الأجهزة خام زي ما ويندوز قاله، ونفس الشركة بتيجي
/// <c>HP</c> و<c>Hewlett-Packard</c>. استنتاج القايمة لوحدها كان
/// هيدمج أسماء مالهاش علاقة ببعض (مثلاً سيرفرات
/// <c>Hewlett Packard Enterprise</c> مع لابات HP) ويفرّق أسماء
/// لنفس الشركة — وقاعدة منع مبنية على تخمين أسوأ من مفيش قاعدة.</para>
///
/// <para>⚠️ <b>والماركة بتتوقف مابتتمسحش.</b> نفس قاعدة الحاوية
/// والموقع: أمر صيانة قديم بيشاور على ماركة، ومسحها بيحوّل السجل
/// لصفوف يتيمة.</para>
/// </summary>
public class LaptopBrand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    /// <summary>الاسم زي ما صاحب الشغل كتبه — ده اللي بيتعرض.</summary>
    [MaxLength(80)]
    public string Name { get; set; } = "";

    /// <summary>
    /// الاسم بعد التوحيد — <b>ده اللي بيتقارن بيه</b>.
    ///
    /// <para>⚠️ متخزّن مش محسوب وقت الاستعلام: المقارنة بتحصل في
    /// قاعدة البيانات، ودالة C# مالهاش ترجمة SQL.</para>
    /// </summary>
    [MaxLength(80)]
    public string NormalizedName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<LaptopBrandAlias> Aliases { get; set; } = new List<LaptopBrandAlias>();
}
