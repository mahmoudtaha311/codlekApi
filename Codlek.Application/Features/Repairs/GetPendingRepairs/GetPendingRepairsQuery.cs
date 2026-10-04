using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Repairs;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetPendingRepairs;

/// <summary>
/// الأوامر المستنية قرار المحاسب.
///
/// <para>⚠️ <b>النقطة دي قراية، وسياستها <c>RepairsViewer</c> — يعني
/// المحاسب داخل.</b> هو موجود عشان يوافق، ومش بيوافق على حاجة مش
/// شايفها.</para>
/// </summary>
public sealed record GetPendingRepairsQuery
    : IRequest<Result<IReadOnlyList<PendingRepairRow>>>;
