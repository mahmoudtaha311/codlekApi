namespace Codlek.Application.Contracts.Reports;

/// <summary>مسح فحص — السبب إجباري (٥ حروف على الأقل).</summary>
public sealed record DeleteReportRequest(string? Reason);
