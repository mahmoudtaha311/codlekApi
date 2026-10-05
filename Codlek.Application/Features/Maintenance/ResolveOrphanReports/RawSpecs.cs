using System.Text.Json;
using Codlek.Application.Contracts.Sync;

namespace Codlek.Application.Features.Maintenance.ResolveOrphanReports;

/// <summary>
/// المراسي من النسخة الخام المتخزّنة على الفحص.
///
/// <para>⚠️ <b>الأعمدة على الصف مش كفاية للمطابقة</b> — فيها
/// <c>SerialNumber</c> بس، ومفيهاش <c>SystemUuid</c> ولا
/// <c>BoardSerial</c> اللي المطابقة قايمة عليهم. النسخة الخام فيها
/// المواصفات كاملة، وده سبب وجودها.</para>
///
/// <para>🔴 <b>بنقرا المراسي بس — مش الفحص كله.</b> النسخ الخام على
/// الإنتاج مكتوبة بأشكال القديم (<c>"Specs"</c> بحرف كبير، وأولاد
/// بأنواع اتغيّرت مع الوقت). قراية الفحص كله في <c>LaptopReportPayload</c>
/// معناها إن خانة واحدة مالهاش علاقة بالهوية (مرحلة، تاريخ) تكسر
/// القراية — والفحص يتعلّم للمراجعة ومراسيه سليمة. القديم كان بيقرا
/// بأنواعه هو فمكانش بيشوف المشكلة دي.</para>
/// </summary>
public static class RawSpecs
{
    /// <summary>
    /// المراسي — أو <c>null</c> لو النسخة فاضية أو مكسورة أو من غير
    /// مواصفات (والفحص ساعتها بيتعلّم للمراجعة، زي القديم).
    /// </summary>
    public static DeviceSpecsPayload? From(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);

            if (!TryGet(doc.RootElement, "specs", out var specs)
                || specs.ValueKind != JsonValueKind.Object)
                return null;

            var payload = new DeviceSpecsPayload
            {
                SystemUuid = Text(specs, "systemUuid"),
                SerialNumber = Text(specs, "serialNumber"),
                BoardSerial = Text(specs, "boardSerial"),
            };

            if (TryGet(specs, "internalDisks", out var disks)
                && disks.ValueKind == JsonValueKind.Array)
            {
                foreach (var disk in disks.EnumerateArray())
                    payload.InternalDisks.Add(new DiskInfoPayload { SerialNumber = Text(disk, "serialNumber") });
            }

            return payload;
        }
        catch (JsonException)
        {
            // نسخة خام مكسورة مش سبب إن اللفة تقف — الصف بيتعلّم للمراجعة.
            return null;
        }
    }

    /// <summary>
    /// ⚠️ <b>من غير حساسية للحالة</b> — القديم كتب <c>Specs</c> والجديد
    /// <c>specs</c>، ونفس خيار القراية بتاع الاستقبال.
    /// </summary>
    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string Text(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
