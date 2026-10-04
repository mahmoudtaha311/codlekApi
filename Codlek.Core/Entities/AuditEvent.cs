using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// سجل الإجراءات المهمة.
///
/// <para><b>حدود واضحة عشان منعملش أربع جداول مراجعة:</b>
/// <c>LoginEvent</c> بيفضل للدخول وبس، و<c>ReportEdit</c> بيفضل لتعديلات
/// الفحوصات وبيسافر جوّه <c>RawJson</c>. الجدول ده للإجراءات الإدارية
/// وإجراءات النظام — اقتران راكة، إلغاء مفتاح، شك في استنساخ، تعارض هوية
/// جهاز. ومفيش جدول خامس.</para>
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>معرّف بيتولّد على الراكة لما الحدث أصله من هناك — بيمنع التكرار.</summary>
    public Guid? EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>User · Rack · System</summary>
    [MaxLength(20)]
    public string ActorType { get; set; } = "System";

    public Guid? ActorUserId { get; set; }
    public Guid? ActorRackId { get; set; }

    [MaxLength(120)]
    public string ActorName { get; set; } = "";

    /// <summary>rack.paired · rack.revoked · rack.clone_suspected …</summary>
    [MaxLength(60)]
    public string Action { get; set; } = "";

    [MaxLength(40)]
    public string EntityType { get; set; } = "";

    public Guid? EntityId { get; set; }

    [MaxLength(30)]
    public string EntityCode { get; set; } = "";

    [MaxLength(400)]
    public string Summary { get; set; } = "";

    public string DataJson { get; set; } = "";

    [MaxLength(60)]
    public string Ip { get; set; } = "";
}
