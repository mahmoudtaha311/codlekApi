using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// قسم في الشركة — الوحدة اللي الجهاز بيتحرّك بينها.
///
/// <para>⚠️ <b>القسم ≠ التخصص.</b> القسم وحدة تنظيمية (التوجيه بيروح
/// لقسم)، والتخصص مهارة الشخص (الأمر بيتوجّه لتخصص). قسم واحد ممكن يبقى
/// فيه أكتر من تخصص. خلطهم في حقل واحد بيخلي التوجيه والتوزيع نفس
/// الحاجة، وهما مختلفين.</para>
///
/// <para>مركزي وقابل للتعديل من لوحة الإدارة — مش enum في الكود، لأن
/// الأقسام بتتغيّر والشركة المفروض تضيف قسم من غير نشر نسخة جديدة.</para>
/// </summary>
public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(20)]
    public string Code { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
