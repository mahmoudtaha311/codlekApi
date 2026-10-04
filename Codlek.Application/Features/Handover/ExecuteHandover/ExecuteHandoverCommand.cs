using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.ExecuteHandover;

/// <summary>
/// سلّم دفعة لابات لجهة.
///
/// <para>🔴 <b>دي القدرة اللي مدير الدور عنده والمدير مالوش.</b>
/// صاحب الشغل قالها بالنص: «عنده نفس كل حاجة عند المدير بس عنده
/// ميزة زيادة إنه يقدر يسلّم لابات». فالسياسة على النقطة
/// <c>HandoverAllowed</c> مش <c>ManagerOrAbove</c> — لو اتحطّت على
/// الأخيرة تبقى مش ميزة زيادة.</para>
///
/// <para>⚠️ <b>ومفيش متحقّق على الأمر ده.</b> كل الفحوص ليها رسايل
/// مخصوصة بتقول أنهي لاب وأنهي سبب، والمتحقّق بيرد بشكل تاني
/// (<c>ValidationProblem</c>) والواجهة مابتعرضهوش.</para>
/// </summary>
public sealed record ExecuteHandoverCommand(
    List<Guid>? DeviceIds,
    Guid DestinationId,
    string? ReceivedByName,
    string? Reason,
    string? Notes,
    string? OverrideReason) : IRequest<Result<HandoverResult>>;
