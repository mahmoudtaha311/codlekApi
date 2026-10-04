namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// عدّادات التحذيرات لترويسة اللوحة.
///
/// <para>⚠️ <b>مفيش فترة هنا بقصد.</b> التحذير حالة قايمة دلوقتي،
/// مش رقم فترة — لو اتحط على اللوحة كان هيتغيّر لما المستخدم يغيّر
/// التاريخ وده غلط.</para>
///
/// <para>⚠️ <b>وبيرجع أصفار للفني مش <c>403</c>.</b> الأيقونة
/// بتتنده على كل صفحة، و<c>403</c> كان بيطلّع رسالة خطأ في ترويسة
/// صفحة الفني كل مرة.</para>
/// </summary>
public sealed record AlertsSummary(
    int PartChanged,
    int DuplicateSuspected,
    int NeedsDeviceResolution,
    int Total);
