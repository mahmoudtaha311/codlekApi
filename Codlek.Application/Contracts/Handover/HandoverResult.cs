namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// نتيجة التسليم.
///
/// <para>🔴 <b>الكل أو ولا واحد.</b> لو ٣٩ من ٤٠ نجحوا، الشحنة
/// ناقصة والسجل بيقول إنها تمّت — فالرقم ده إما العدد كله إما
/// الطلب كله بيترفض.</para>
/// </summary>
public sealed record HandoverResult(
    int Moved,
    string DestinationName,
    string ReceivedByName,
    DateTime AtUtc);
