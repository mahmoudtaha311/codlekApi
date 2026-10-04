using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// الشركة. النظام متعدد الشركات من أول يوم عشان لما تيجي شركة تانية
/// منغيّرش قاعدة البيانات — كل استعلام بيترشّح بـ TenantId.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<WebUser> Users { get; set; } = new();
    public List<Report> Reports { get; set; } = new();
}
