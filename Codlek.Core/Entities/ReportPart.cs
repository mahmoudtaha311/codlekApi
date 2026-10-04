using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

public class ReportPart
{
    public int Id { get; set; }
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    [MaxLength(200)] public string Name { get; set; } = "";
}
