namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// قطعة غيار وكام مرة اتحطّت — صف في «أكتر قطع الغيار».
/// </summary>
/// <param name="Name">
/// ⚠️ <b>أشهر كتابة للقطعة، مش النص المطبّع.</b> المطبّع مفتاح
/// تجميع وبس — عرضه كان بيدّي «شاشه» بدل «شاشة».
/// </param>
/// <param name="InventoryCode">كود المخزن لو الفني كتبه — بيفضل فاضي كتير.</param>
/// <param name="FittedQuantity">
/// مجموع الكميات — قطعة واحدة اتحطّت ٣ منها تبقى ٣.
/// </param>
/// <param name="NotedOnTests">
/// كام مرة القطعة دي اتكتبت على <b>تقرير فحص</b>.
///
/// <para>⚠️ <b>الرقم ده بيتجمّد.</b> خانة قطع الغيار اتشالت من شاشة
/// الفحص في برنامج الراكة بقرار سابق، فالعمود ده <b>تاريخي</b> —
/// بيقلّ معناه مع الوقت ومابيزدش. متسيب لأن البيانات القديمة
/// حقيقية.</para>
/// </param>
public sealed record PartDemandItem(
    string Name,
    string InventoryCode,
    int FittedTimes,
    int FittedQuantity,
    int Devices,
    int NotedOnTests);
