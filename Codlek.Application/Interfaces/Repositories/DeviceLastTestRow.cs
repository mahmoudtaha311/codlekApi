namespace Codlek.Application.Interfaces.Repositories;

/// <summary>آخر فحص للاب — خام.</summary>
public sealed record DeviceLastTestRow(
    Guid ReportId,
    DateTime StartedAtUtc,
    Guid? TechnicianId,
    string TechnicianName,
    string TechnicianCode,
    Guid? SourceRackId,
    int PassCount,
    int FailCount,
    int ErrorCount,
    int NotPresentCount,
    int SkipCount);
