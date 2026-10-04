using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>مرساة هوية — تاريخ مش عمود.</summary>
public class DeviceIdentifierRow
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    public DeviceIdentifierKind Kind { get; set; }

    [MaxLength(200)]
    public string RawValue { get; set; } = "";

    [MaxLength(200)]
    public string NormalizedValue { get; set; } = "";

    [MaxLength(120)]
    public string Source { get; set; } = "";

    public DeviceIdentityConfidence Confidence { get; set; } = DeviceIdentityConfidence.None;

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public DateTime? SupersededAtUtc { get; set; }

    [MaxLength(400)]
    public string SupersededReason { get; set; } = "";

    [MaxLength(120)]
    public string SupersededByName { get; set; } = "";
}
