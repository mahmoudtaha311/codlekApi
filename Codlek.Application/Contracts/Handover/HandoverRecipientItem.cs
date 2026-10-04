namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// مستلم وكام لاب استلم.
///
/// <para>⚠️ <b>«كل جهة عندها كام لاب» و«مين استلم إيه» سؤالين
/// مختلفين.</b> الأولاني في جرد المخزن وبيعدّ <b>المكان الحالي</b>؛
/// ده بيعدّ <b>حركات التسليم</b>. والرقمين ممكن يختلفوا: لاب اتسلّم
/// وبعدين اترجّع بيفضل في سجل المستلم ومابيبقاش في عدّاد المخزن —
/// وده صح، الحركة حصلت فعلاً.</para>
/// </summary>
public sealed record HandoverRecipientItem(
    string ReceivedByName,
    int DeviceCount,
    DateTime LastAtUtc,
    IReadOnlyList<string> Destinations);
