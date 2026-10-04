namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// مواصفات اللاب زي ما الفحص قراها.
/// </summary>
/// <param name="CommercialModelSource">
/// ⚠️ منين جه الاسم التجاري (كتالوج · يدوي · …) — عشان اللي بيقرا
/// يعرف يثق فيه قد إيه.
/// </param>
public sealed record ReportSpecs(
    string Manufacturer,
    string Model,
    string CommercialModelName,
    string CommercialModelSource,
    string MachineType,
    string Cpu,
    string RamText,
    string StorageText,
    string Gpu,
    string ScreenSummary,
    string SerialNumber,
    double BatteryHealthPercent,
    int? BenchmarkScore,
    double? MaxCpuTemp,
    bool ThrottlingDetected);
