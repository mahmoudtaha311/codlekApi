using System.Text.Json;
using Codlek.Application.Contracts.Hardware;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Hardware;

namespace Codlek.Application.Features.Hardware;

/// <summary>
/// تحويل اللقطة للعقد.
///
/// <para>🔴 <b>مكان واحد للتلات نقط.</b> لقطة الفحص ولقطة الجهاز
/// والمقارنة كلهم بيرجّعوا <b>نفس</b> شكل القطعة. ولو التحويل اتكتب
/// تلات مرات، أول عمود يتزاد في واحدة بيخلّي شاشة من التلاتة تعرض
/// بيانات ناقصة من غير أي خطأ.</para>
/// </summary>
internal static class HardwareMapping
{
    public static ReportHardwareResponse Snapshot(
        SnapshotHeaderFacts header,
        string devicePublicCode,
        string rackCode,
        IReadOnlyList<ReportSnapshotComponent> components) =>
        new(
            ReportId: header.ReportId,
            DeviceId: header.DeviceId,
            DevicePublicCode: devicePublicCode,
            StartedAtUtc: header.StartedAtUtc,
            CapturedAtUtc: header.CapturedAtUtc,
            TechnicianName: header.TechnicianName,
            TechnicianCode: header.TechnicianCode,
            RackCode: rackCode,
            CollectorVersion: header.CollectorVersion,
            IsPartial: header.IsPartial,
            RanAsAdministrator: header.RanAsAdministrator,
            Warnings: header.Warnings,
            Components: components
                .Select(c => Component(c, header.CommercialModelName))
                .ToList());

    /// <summary>
    /// 🔴 <b>كود اللاب المربوط هو الأصل، واللي في اللقطة
    /// احتياطي.</b>
    ///
    /// <para>اللاب بياخد كود جديد لما يتدمج أو يترقّم من جديد،
    /// واللقطة القديمة فاضلة بالكود القديم. عرض القديم بيخلّي الفني
    /// يدوّر على كود مش موجود على أي لاب.</para>
    ///
    /// <para>⚠️ والاحتياطي موجود عشان الفحص اللي <b>لسه</b> مش مربوط
    /// بجهاز — ساعتها اللي في اللقطة هو كل اللي عندنا.</para>
    /// </summary>
    public static string DeviceCode(DeviceCodeFacts? device, string fromSnapshot) =>
        device is { PublicCode: { Length: > 0 } code } ? code : fromSnapshot;

    public static HardwareComponentItem Component(
        ReportSnapshotComponent c, string commercialName) =>
        new(
            Type: c.Type,
            TypeText: ComponentType.Arabic(c.Type),
            InstanceIndex: c.InstanceIndex,
            Slot: c.SlotOrPosition,
            Manufacturer: c.Manufacturer,
            Model: c.Model,

            // ⚠️ على كارت النظام وبس — هو الكارت اللي بيمثّل اللاب
            // نفسه. ولو اتحط على كل القطع، الذاكرة والهارد كانوا
            // هيتعرضوا باسم اللاب بدل أسمائهم.
            CommercialModelName: c.Type == ComponentType.System ? commercialName : "",

            PartNumber: c.PartNumber,

            // ⚠️ بيخرج زي ما هو — الفاضي يفضل فاضي. «غير متاح» كلمة
            // عرض، والواجهة هي اللي بتقولها.
            Serial: c.ManufacturerSerial,

            PnPDeviceId: c.PnPDeviceId,
            Confidence: c.IdentityConfidence,
            ConfidenceText: IdentityConfidenceText.Arabic(c.IdentityConfidence),
            IsPresent: c.IsPresent,
            CapacityBytes: c.CapacityBytes,
            SpeedMhz: c.SpeedMhz,
            HealthPercent: c.HealthPercent,
            PowerOnHours: c.PowerOnHours,
            Source: c.Source,
            Attributes: Attributes(c.AttributesJson));

    public static ComponentDiffItem Diff(ComponentDiff d) =>
        new(
            Type: d.Type,
            TypeText: ComponentType.Arabic(d.Type),

            // 🔴 الاسم كنص — الواجهة بتتفرّع عليه.
            Kind: d.Kind.ToString(),

            KindText: ChangeKindText.Arabic(d.Kind),
            Tone: ChangeKindText.Tone(d.Kind),
            MatchedBy: d.MatchedBy,
            Explanation: d.Explanation,
            StrongBothSides: d.StrongBothSides,

            // ⚠️ المقارنة مابتعرضش اسم تجاري — بتقارن القيم اللي
            // القارئ رجّعها. الفاضي هنا مقصود.
            Left: d.A is null ? null : Component(d.A, ""),
            Right: d.B is null ? null : Component(d.B, ""));

    public static CompareSideInfo Side(
        SnapshotHeaderFacts h, string rackCode, int componentCount) =>
        new(
            ReportId: h.ReportId,
            StartedAtUtc: h.StartedAtUtc,
            CapturedAtUtc: h.CapturedAtUtc,
            TechnicianName: h.TechnicianName,
            TechnicianCode: h.TechnicianCode,
            RackCode: rackCode,

            // ⚠️ العدد من القايمة المحمّلة — راجع `CompareSideInfo`.
            ComponentCount: componentCount,

            IsPartial: h.IsPartial,
            RanAsAdministrator: h.RanAsAdministrator);

    /// <summary>
    /// العدّادات — <b>خمس خانات، و«مش مؤكّدة» ليها خانة
    /// لوحدها</b>.
    /// </summary>
    public static CompareSummary Summarize(IReadOnlyList<ComponentDiff> diffs) =>
        new(
            Unchanged: diffs.Count(d => d.Kind == ChangeKind.Unchanged),

            Changed: diffs.Count(d => d.Kind is ChangeKind.SerialChanged
                                           or ChangeKind.ModelChanged
                                           or ChangeKind.ConfigurationDifferent),

            Added: diffs.Count(d => d.Kind == ChangeKind.Added),
            Removed: diffs.Count(d => d.Kind == ChangeKind.Removed),

            // 🔴 الخانة دي هي اللي بتمنع الاتهام من غير دليل: ضمّها
            // لـ«اتغيّرت» بيخلّي الرقم يشيل حالات مش قادرين نثبتها.
            Uncertain: diffs.Count(d => d.Kind is ChangeKind.IdentityUncertain
                                             or ChangeKind.NotObserved));

    /// <summary>
    /// نتيجة كل مرحلة في الفحصين، مطابقة <b>بالاسم</b>.
    ///
    /// <para>🔴 <b>مش بالترتيب.</b> قايمة المراحل بتتغيّر بين نسخ
    /// البرنامج — مرحلة اتضافت في النص بتزحزح كل اللي بعدها،
    /// والمطابقة بالفهرس كانت هتقول إن نص المراحل اتغيّرت
    /// نتيجتها.</para>
    /// </summary>
    public static List<StepDiffItem> Steps(
        IReadOnlyList<StepOutcomeFacts> rows, Guid leftReportId, Guid rightReportId)
    {
        // ⚠️ `First` مش `Single`: فحص قديم ممكن يكون فيه نفس عنوان
        // المرحلة مرتين، والصفحة لازم تفضل تفتح.
        var left = Side(rows, leftReportId);
        var right = Side(rows, rightReportId);

        var titles = left.Keys
            .Concat(right.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal);

        var result = new List<StepDiffItem>();

        foreach (string title in titles)
        {
            int? a = left.TryGetValue(title, out int l) ? l : null;
            int? b = right.TryGetValue(title, out int r) ? r : null;

            result.Add(new StepDiffItem(
                Title: title,

                // ⚠️ `-1` و«—» معناهم «المرحلة دي مش موجودة في الجهة
                // دي» — مش حالة رقمها صفر.
                LeftStatus: a ?? -1,
                LeftText: a is { } la ? StepOutcome.Arabic(la) : "—",
                RightStatus: b ?? -1,
                RightText: b is { } rb ? StepOutcome.Arabic(rb) : "—",

                Direction: StepOutcome.Direction(a, b)));
        }

        return result;
    }

    private static Dictionary<string, int> Side(
        IReadOnlyList<StepOutcomeFacts> rows, Guid reportId) =>
        rows.Where(r => r.ReportId == reportId)
            .GroupBy(r => r.Title, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Status, StringComparer.Ordinal);

    /// <summary>
    /// بيفكّ <c>AttributesJson</c> لقاموس نصوص.
    ///
    /// <para>⚠️ <b>JSON بايظ بيرجّع قاموس فاضي — مايوقّعش
    /// الصفحة.</b> نفس سياسة <c>SnapshotComparison</c> بالظبط:
    /// تفاصيل مش مقروءة بتختفي، والصفحة بتفضل تفتح.</para>
    ///
    /// <para>⚠️ والقيم بتتحوّل لنص عن قصد: العقد ده بيتعرض كجدول
    /// «مفتاح/قيمة»، والواجهة مالهاش تفهم أنواع مختلفة لكل نوع
    /// مكوّن.</para>
    /// </summary>
    private static Dictionary<string, string> Attributes(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(json)) return map;

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                string value = property.Value.ValueKind switch
                {
                    JsonValueKind.Null => "",
                    JsonValueKind.String => property.Value.GetString() ?? "",
                    _ => property.Value.ToString(),
                };

                // ⚠️ الفاضي مابينزلش الجدول — صف «المفتاح: » مالوش
                // معنى للّي بيقرا.
                if (value.Length > 0) map[property.Name] = value;
            }
        }
        catch (JsonException)
        {
            // تفاصيل مش مقروءة مابتوقّعش الصفحة — بتختفي وخلاص.
        }

        return map;
    }
}
