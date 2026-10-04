using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Sync;
using MediatR;

namespace Codlek.Application.Features.Rack.SyncBatches;

/// <summary>
/// دفعة مزامنة من محطة <b>متحققة بمفتاحها</b> — نتيجة لكل صف.
///
/// <para>🔴 <b>الفرق عن <c>/api/sync/reports</c>:</b> القديمة بترجّع
/// عدّادات إجمالية، والراكة مش بتعرف منها <b>أنهي صفوف في طابورها</b>
/// اتقبلت. هنا كل صف بيرجع بمعرّفه، فالطابور بيقفل الصفوف واحد واحد —
/// وصف فاسد واحد بيترفض لوحده من غير ما يوقف الطابور.</para>
/// </summary>
/// <param name="TenantId">من المحطة. <b>مش من الطلب.</b></param>
/// <param name="RackCode">للسجلات بس — مش للتحقق.</param>
/// <param name="OfflineValidityDays">
/// ⚠️ من الإعدادات — نفس الرقم اللي دخول الفني بيختم بيه المهلة.
/// </param>
public sealed record SyncBatchCommand(
    Guid TenantId,
    Guid RackId,
    string RackCode,
    int OfflineValidityDays,
    SyncBatchRequest Request) : IRequest<Result<SyncBatchOutcome>>;

/// <summary>الرد، ومعاه «ده رد متخزّن من قبل؟».</summary>
/// <param name="Replayed">
/// 🔴 <b>الرد ده اتكتب قبل كده</b> — إعادة إرسال، أو سباق كسبه طلب
/// تاني. المنادي <b>مابيلمسش المحطة</b> في الحالة دي (العدّاد اتزوّد
/// مرة خلاص) وبيعلّم الرد بترويسة <c>Idempotent-Replay</c>.
/// </param>
public sealed record SyncBatchOutcome(SyncBatchResponse Response, bool Replayed);
