namespace Codlek.Application.Contracts.Hardware;

/// <summary>
/// قطعة واحدة في اللقطة.
///
/// <para>⚠️ <c>Serial</c> بيخرج <b>زي ما هو</b> — الفاضي يفضل
/// فاضي. مفيش «غير متاح» هنا: دي كلمة عرض، والواجهة هي اللي
/// بتقولها.</para>
/// </summary>
/// <param name="CommercialModelName">
/// الاسم التجاري للّاب — بيتحط على <b>كارت معلومات النظام وبس</b>،
/// هو الكارت اللي بيمثّل اللاب نفسه.
///
/// <para>🔴 لو اتحط على كل القطع، الذاكرة والهارد كانوا هيتعرضوا
/// باسم اللاب بدل أسمائهم.</para>
/// </param>
public sealed record HardwareComponentItem(
    int Type,
    string TypeText,
    int InstanceIndex,
    string Slot,
    string Manufacturer,
    string Model,
    string CommercialModelName,
    string PartNumber,
    string Serial,
    string PnPDeviceId,
    int Confidence,
    string ConfidenceText,
    bool IsPresent,
    long? CapacityBytes,
    int? SpeedMhz,
    int? HealthPercent,
    int? PowerOnHours,
    string Source,
    IReadOnlyDictionary<string, string> Attributes);
