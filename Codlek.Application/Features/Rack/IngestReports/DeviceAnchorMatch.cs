using Codlek.Application.Contracts.Sync;
using Codlek.Core.Devices;
using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Application.Features.Rack.IngestReports;

/// <summary>
/// بيدوّر على جهاز <b>موجود</b> بمراسي فحص وصل من غير معرّف جهاز.
///
/// <para>🔴 <b>مكان واحد للمطابقة — الاستقبال وصيانة الإقلاع.</b> كانت
/// دالة خاصة جوّه الاستقبال، والقديم كان بيستعمل نفس الدالة في الاتنين
/// (<c>DeviceSyncService.TryMatchAsync</c>). لو اتنسخت، أول تعديل في
/// واحدة بيخلّي نفس الفحص يتربط بجهاز لو وصل دلوقتي وبجهاز تاني لو
/// اتربط بعد الإقلاع.</para>
///
/// <para>🔴 <b>وبنفس ترتيب القوة اللي على الراكة بالظبط</b>
/// (<see cref="DeviceIdentityStrength.Order"/>). أي اختلاف بين الطرفين
/// معناه إن <b>نفس اللاب بياخد هوية مختلفة حسب مين اللي طابق</b>.</para>
///
/// <para>⚠️ <b>ربط بس، مش إنشاء.</b> مفيش تطابق ← <c>null</c>،
/// والمنادي بيعلّم الفحص للمراجعة.</para>
/// </summary>
public static class DeviceAnchorMatch
{
    /// <param name="specs">المواصفات — <c>null</c> بيرجّع <c>null</c>.</param>
    /// <param name="devicesByAnchor">
    /// الأجهزة اللي المرساة دي بتشاور عليها <b>في شركة المنادي</b> —
    /// <see cref="DeviceIdentityStrength.ProbeTake"/> كفاية.
    /// </param>
    public static async Task<Guid?> TryMatchAsync(
        DeviceSpecsPayload? specs,
        Func<DeviceIdentifierKind, string, CancellationToken, Task<IReadOnlyList<Guid>>> devicesByAnchor,
        CancellationToken ct)
    {
        if (specs is null) return null;

        foreach (var kind in DeviceIdentityStrength.Order)
        {
            IEnumerable<string?> values = kind == DeviceIdentifierKind.DiskSerial

                // ⚠️ كل الأقراص — أي واحد فيهم ممكن يكون المرساة.
                //    و`?? []` عشان نسخة خام قديمة فيها `"internalDisks": null`
                //    — القديم كان بيعمل نفس الحاجة (`?? new List`).
                ? (specs.InternalDisks ?? []).Select(d => d?.SerialNumber)

                : [Probe(kind, specs)];

            foreach (string? value in values)
            {
                if (IdentityValues.IsPlaceholder(value)) continue;

                string normalized = IdentityValues.Normalize(value);

                if (normalized.Length == 0) continue;

                var matches = await devicesByAnchor(kind, normalized, ct);

                /*
                  🔴 **مرساة واحدة بتشاور على جهازين = عيب بيانات.**

                  بنسيبه للمراجعة بدل ما نختار واحد عشوائي — والاختيار
                  العشوائي هنا معناه فحص بيروح لجهاز غلط ومحدّش بيعرف.
                */
                if (matches.Count == 1) return matches[0];
                if (matches.Count > 1) return null;
            }
        }

        return null;
    }

    private static string? Probe(DeviceIdentifierKind kind, DeviceSpecsPayload specs) => kind switch
    {
        DeviceIdentifierKind.SystemUuid => specs.SystemUuid,
        DeviceIdentifierKind.BiosSerial => specs.SerialNumber,
        DeviceIdentifierKind.BoardSerial => specs.BoardSerial,
        _ => "",
    };
}
