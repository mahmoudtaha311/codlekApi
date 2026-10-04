namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// عدّادات نتايج خطوات الفحص.
///
/// <para>🔴 <b>«ما اشتغلش» و«فشل» مش نفس الحاجة.</b>
/// <c>Error</c> معناها إن الفحص نفسه مانجحش يتنفّذ — مشكلة في
/// الفحص مش في الجهاز. وخلطهم بيخلّي «الشاشة بايظة» و«ماقدرناش
/// نفحص الشاشة» رقم واحد.</para>
/// </summary>
public sealed record TestCounts(
    int Pass,
    int Fail,
    int Error,
    int NotPresent,
    int Skip);
