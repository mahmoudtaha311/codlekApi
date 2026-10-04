using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// عدّاد لكل شركة — مصدر الأرقام المتسلسلة (RACK-001، LP-00000001).
///
/// <para>⚠️ مش <c>MAX(code)+1</c>. ده بيتسابق: اتنين بيقروا نفس الرقم
/// وبيكتبوا نفس الكود. الزيادة بتتعمل في جملة تحديث واحدة.</para>
/// </summary>
public class TenantCounter
{
    public Guid TenantId { get; set; }

    [MaxLength(40)]
    public string CounterName { get; set; } = "";

    public int NextValue { get; set; } = 1;
}
