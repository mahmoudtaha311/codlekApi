using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// موقع تشغيلي — مخزن، قسم، نقطة بيع.
///
/// <para>⚠️ <b>مش <see cref="Department"/>.</b> القسم وحدة تنظيمية
/// بتملك ناس (<c>WebUser.DepartmentId</c>)؛ الموقع مكان بيشيل أجهزة.
/// وساعات الاتنين بنفس الاسم — «قسم الصيانة» قسم وكمان مكان — بس ده
/// تشابه أسماء مش نفس الكيان: الجهاز بينتقل لموقع، الموظف بينتمي لقسم.
/// وكمان <c>Technician.DepartmentId</c> مالوش لا مفتاح أجنبي ولا فهرس
/// لحد دلوقتي، فماينفعش نبني عليه حيازة أجهزة.</para>
/// </summary>
public class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>كود قصير للاختصار في القوايم — ممكن يفضل فاضي.</summary>
    [MaxLength(20)]
    public string Code { get; set; } = "";

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public LocationKind Kind { get; set; } = LocationKind.Warehouse;

    /// <summary>
    /// الموقع بيتوقف، مابيتمسحش.
    ///
    /// <para>⚠️ حركات قديمة بتشاور عليه؛ مسحه بيقطع تاريخ.</para>
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
