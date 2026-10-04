using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.MarkReady;

/// <summary>
/// علّم «جاهز للتسليم» أو شيل العلامة.
///
/// <para>🔴 <b>الكمبيوتر بيعرف إن الفحص عدّى. مابيعرفش إن اللاب
/// جاهز.</b> اتنضّف؟ اتغلّف؟ اتلزق عليه ليبل؟ الحاجات دي مالهاش
/// أثر في أي بيانات، فمحدش غير بني آدم يقدر يقول عليها. النقطة دي
/// هي المكان اللي الحكم ده بيتسجّل فيه — <b>ومين قاله وإمتى</b>، مش
/// مجرد علم صح/غلط.</para>
///
/// <para>⚠️ <b>وللمديرين وفوق، مش للي بيسلّم بس.</b> المراجعة
/// والتسليم خطوتين مقصود إنهم منفصلين: مدير المخزن بيراجع، ومدير
/// الدور بيسلّم.</para>
/// </summary>
public sealed record MarkHandoverReadyCommand(List<Guid>? DeviceIds, bool Ready)
    : IRequest<Result<HandoverReadyResult>>;
