namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// صف فحص في تاريخ اللاب — <b>خام</b>.
///
/// <para>⚠️ ومفيش حرف عربي ولا حسابة وقت هنا: الترجمة وكود المحطة
/// بيتحسبوا في المعالج بعد القراية.</para>
/// </summary>
public sealed record DeviceTestRow(
    Guid ReportId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    long DurationMs,
    Guid? TechnicianId,
    string TechnicianName,
    string TechnicianCode,
    Guid? SourceRackId,
    int PassCount,
    int FailCount,
    int ErrorCount,
    int NotPresentCount,
    int SkipCount,
    int StepCount,
    string GeneralNote,
    int NotRunCount);
