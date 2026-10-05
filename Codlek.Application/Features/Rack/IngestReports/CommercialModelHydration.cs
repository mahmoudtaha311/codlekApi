using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;

namespace Codlek.Application.Features.Rack.IngestReports;

/// <summary>
/// الاسم التجاري اللي المفروض يبقى على الجهاز، من أدلة فحوصه.
///
/// <para>🔴 <b>مكان واحد للقاعدة — الاستقبال وصيانة الإقلاع.</b> القديم
/// كان بيستعمل نفس الدالة (<c>DeviceCommercialModelHydrator.HydrateAsync</c>)
/// في الاتنين. لو اتنسخت، جهاز بيتملى باسم بعد فحص وباسم تاني بعد
/// إعادة التشغيل.</para>
/// </summary>
public static class CommercialModelHydration
{
    /// <summary>القيم التلاتة اللي على الجهاز.</summary>
    public readonly record struct Values(string? Name, string? Source, string? MachineType);

    /// <summary>
    /// القيم الجديدة لو فيه تغيير — أو <c>null</c> لو مفيش دليل أو مفيش
    /// فرق.
    /// </summary>
    /// <param name="evidence">أدلة الفحوص <b>من الأحدث للأقدم</b>.</param>
    public static Values? Next(Values current, IReadOnlyList<ModelEvidenceRow> evidence)
    {
        if (evidence.Count == 0) return null;

        int pick = CommercialModelEvidence.Pick(
            [.. evidence.Select(e => e.CommercialModelSource)],
            [.. evidence.Select(e => e.CommercialModelName)]);

        // ⚠️ مفيش دليل موثوق ← الخام يفضل، ومابنلمسش حاجة.
        if (pick < 0) return null;

        var best = evidence[pick];

        // ⚠️ كود المصنع بيتملى بس لو موجود — مابنمسحش قيمة بفاضي.
        string? machineType = string.IsNullOrWhiteSpace(best.MachineType)
            ? current.MachineType
            : best.MachineType;

        var next = new Values(best.CommercialModelName, best.CommercialModelSource, machineType);

        bool changed =
            !string.Equals(current.Name, next.Name, StringComparison.Ordinal)
            || !string.Equals(current.Source, next.Source, StringComparison.Ordinal)
            || !string.Equals(current.MachineType, next.MachineType, StringComparison.Ordinal);

        return changed ? next : null;
    }
}
