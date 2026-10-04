using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.GetRecipients;

/// <summary>
/// مين استلم إيه.
///
/// <para>⚠️ <b>ده مش «كل جهة عندها كام لاب».</b> الأولاني في جرد
/// المخزن وبيعدّ <b>المكان الحالي</b>؛ ده بيعدّ <b>حركات
/// التسليم</b>. والرقمين ممكن يختلفوا: لاب اتسلّم وبعدين اترجّع
/// بيفضل في سجل المستلم ومابيبقاش في عدّاد المخزن — وده صح، الحركة
/// حصلت فعلاً.</para>
/// </summary>
public sealed record GetHandoverRecipientsQuery(DateTime? From, DateTime? To)
    : IRequest<Result<IReadOnlyList<HandoverRecipientItem>>>;
