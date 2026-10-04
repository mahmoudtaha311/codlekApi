using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// قطعة اتركّبت في صيانة.
///
/// <para>⚠️ <b>مش <c>ReportPart</c>.</b> ده مربوط بـ<c>ReportId</c>
/// وبيتمسح ويتعمل من الأول في كل مزامنة لفحص اتغيّر — قطعة غيار
/// اتصرفت فعلاً ماينفعش تعيش في صف بيتحرق.</para>
/// </summary>
public class RepairPart
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }
    public RepairWorkItem? WorkItem { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>كود المخزون لو الراكة صرفتها من جردها — ممكن يفضل فاضي.</summary>
    [MaxLength(60)]
    public string InventoryCode { get; set; } = "";

    public int Quantity { get; set; } = 1;

    /// <summary>سيريال القطعة الجديدة لو اتقرا — مابيتخترعش.</summary>
    [MaxLength(120)]
    public string SerialNumber { get; set; } = "";

    public string Notes { get; set; } = "";
}
