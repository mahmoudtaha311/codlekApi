namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// صف في سجل التسليمات.
///
/// <para>⚠️ «هاني سلّم لمين الشهر ده؟» — السؤال اللي
/// <c>ReceivedByName</c> اتعمل عشانه. لو الاسم كان اتحط في
/// <c>Reason</c> كنص حر، السؤال ده مالوش إجابة غير بالقراءة
/// بالعين.</para>
/// </summary>
/// <param name="ByName">اللي سلّم — بخلاف اللي استلم.</param>
/// <param name="Reason">
/// ⚠️ والاستثناء بيبان هنا: التسليم من غير مراجعة سببه مكتوب في
/// أول السطر، فاللي بيراجع السجل بعد شهر يشوفه من غير ما يفتح
/// حاجة.
/// </param>
public sealed record HandoverLogItem(
    long Id,
    Guid DeviceId,
    string DeviceCode,
    Guid? DestinationId,
    string DestinationName,
    string ReceivedByName,
    string ByName,
    DateTime AtUtc,
    string Reason);
