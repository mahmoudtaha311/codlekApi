using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>تعديل اتعمل على فحص بعد التسليم — بيظهر للمدير العام في المراجعة.</summary>
public class ReportEdit
{
    public int Id { get; set; }
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    public DateTime AtUtc { get; set; }
    [MaxLength(120)] public string ByName { get; set; } = "";
    [MaxLength(80)] public string Field { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    [MaxLength(400)] public string Reason { get; set; } = "";
}
