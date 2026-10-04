namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// «عندنا كام لاب سليم، وكام اتسلّم، وكام لسه».
///
/// <para>🔴 <b>مفيش فترة هنا بقصد</b> — دي حالة قايمة دلوقتي. لو
/// اتحطّت على اللوحة كانت هتتغيّر لما المدير يغيّر التاريخ، و«عندي
/// كام لاب جاهز» سؤال مالوش علاقة بالتاريخ.</para>
/// </summary>
/// <param name="Healthy">
/// 🔴 «سليم» = <b>آخر</b> فحص مفيهوش فشل ولا خطأ — نفس تعريف شاشة
/// التسليم بالحرف، عشان الرقمين يطابقوا بعض.
/// </param>
/// <param name="HealthyNotHandedOver">
/// ⚠️ <b>استعلام مستقل مش طرح.</b> «سليم» و«اتسلّم» مجموعتين
/// متقاطعتين — لاب سليم اتسلّم موجود في الاتنين، فالطرح بيدّي رقم
/// سالب أو مالوش معنى.
/// </param>
public sealed record InventorySummary(
    int Total,
    int Healthy,
    int NeedsAttention,
    int NeverTested,
    int HandedOver,
    int HealthyNotHandedOver,
    IReadOnlyList<NamedCountItem> ByDestination);
