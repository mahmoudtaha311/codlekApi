using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Reports;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Reports;

namespace Codlek.Application.Features.Reports;

/// <summary>
/// تحويل الفحص للعقد — <b>مكان واحد للقايمة وللتفاصيل
/// وللتحليلات</b>.
/// </summary>
internal static class ReportMapping
{
    public static ReportListItem Row(
        Report r,
        IReadOnlyDictionary<Guid, RackLabel> racks,
        IReadOnlyDictionary<Guid, string> deviceCodes) =>
        new(
            Id: r.Id,
            DeviceId: r.DeviceId,

            // 🔴 كود الجهاز المربوط هو الأصل، واللي في الفحص احتياطي
            // للفحص اللي لسه مش مربوط.
            DevicePublicCode: DeviceCode(deviceCodes, r.DeviceId, r.DeviceCode),

            Manufacturer: r.Manufacturer,
            Model: r.Model,
            CommercialModelName: r.CommercialModelName ?? "",
            Cpu: r.Cpu,
            RamText: r.RamText,
            StorageText: r.StorageText,
            TechnicianId: r.TechnicianId,
            TechnicianName: r.TechnicianName,
            TechnicianCode: r.TechnicianCode,
            RackCode: Rack(racks, r.SourceRackId).Code,
            RackName: Rack(racks, r.SourceRackId).Name,
            StartedAtUtc: r.StartedAtUtc,
            ReceivedAtUtc: r.ReceivedAtUtc,
            DurationMs: r.DurationMs,
            Counts: Counts(r),
            Scope: r.Scope,
            ScopeText: ReportScopeText.Arabic(r.Scope),
            NotRunCount: r.NotRunCount);

    public static TestCounts Counts(Report r) =>
        new(r.PassCount, r.FailCount, r.ErrorCount, r.NotPresentCount, r.SkipCount);

    /// <summary>
    /// ⚠️ الحقايق اللي حكم المرحلة محتاجها — بتتحسب مرة واحدة لكل
    /// فحص، مش لكل مرحلة.
    /// </summary>
    public static StageFacts Facts(Report r) =>
        new(
            RepairRecorded:
                !string.IsNullOrWhiteSpace(r.HousingPaint)
                || !string.IsNullOrWhiteSpace(r.HousingCrack)
                || !string.IsNullOrWhiteSpace(r.Disassembly)
                || r.BatteryService == StageOutcomeText.BatteryNeedsService,

            HasGeneralNote: !string.IsNullOrWhiteSpace(r.GeneralNote),

            // ⚠️ وقت الانتهاء بيتكتب عند التسليم وبس.
            Handed: r.EndedAtUtc.HasValue);

    public static string DeviceCode(
        IReadOnlyDictionary<Guid, string> codes, Guid? deviceId, string fromReport) =>
        deviceId is { } id && codes.TryGetValue(id, out var live) && live.Length > 0
            ? live
            : fromReport;

    private static RackLabel Rack(
        IReadOnlyDictionary<Guid, RackLabel> racks, Guid? rackId) =>
        rackId is { } id && racks.TryGetValue(id, out var label) ? label : new RackLabel("", "");
}
