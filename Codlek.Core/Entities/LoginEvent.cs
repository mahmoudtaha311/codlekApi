using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// سجل دخول. بيخلّي المدير يشوف مين فتح الموقع وإمتى ومن فين —
/// نفس منطق «الراكة بتتشارك»، بس هنا للموقع.
/// </summary>
public class LoginEvent
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }

    [MaxLength(60)] public string Username { get; set; } = "";
    [MaxLength(120)] public string DisplayName { get; set; } = "";
    public bool Success { get; set; }
    [MaxLength(200)] public string Reason { get; set; } = "";
    [MaxLength(60)] public string Ip { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
