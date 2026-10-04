using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// تعريف عطل معروف — عشان الأعطال تبقى قابلة للاستعلام مش نص حر.
///
/// <para>«السماعة اليمنى مابتشتغلش» مكتوبة بإيد الفني عشرين مرة بعشرين
/// صيغة مابتردش على سؤال «كام لاب فيه عطل سماعة الشهر ده». الكود هنا
/// بيرد.</para>
/// </summary>
public class IssueCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>كود ثابت — <c>audio.right_speaker_dead</c> مثلاً.</summary>
    [MaxLength(40)]
    public string Code { get; set; } = "";

    [MaxLength(160)]
    public string Title { get; set; } = "";

    /// <summary>المرحلة اللي العطل بيطلع منها — <c>audio</c>، <c>ports</c>.</summary>
    [MaxLength(40)]
    public string Category { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
