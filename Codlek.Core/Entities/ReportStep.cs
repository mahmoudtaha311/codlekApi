using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Codlek.Core.Entities;

/// <summary>نتيجة مرحلة واحدة — 0 لسه، 1 شغال، 2 فيه مشكلة، 3 اتخطّى.</summary>
public class ReportStep
{
    public int Id { get; set; }
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    [MaxLength(60)] public string StepId { get; set; } = "";
    [MaxLength(120)] public string Title { get; set; } = "";
    public int Status { get; set; }

    public string Note { get; set; } = "";
    public string SkipReason { get; set; } = "";
    public string Detail { get; set; } = "";
    public long DurationMs { get; set; }

    // ⛔ PORT-BLOCKER (ملحوظة نقل، مش من الأصل): StatusText بتنادي
    // StepStatus، وهي static helper متعرّفة جوّه Entities.cs نفسه ومش
    // في قايمة الكيانات — فمانقلتهاش ومااخترعتهاش. [NotMapped] يعني
    // مفيش أي أثر على الschema. النص الأصلي بالحرف تحت.
//     [NotMapped]
//     public string StatusText => StepStatus.Text(Status);
}
