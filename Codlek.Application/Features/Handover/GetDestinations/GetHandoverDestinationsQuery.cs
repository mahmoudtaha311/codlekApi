using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.GetDestinations;

/// <summary>
/// الجهات المتاحة للتسليم.
///
/// <para>⚠️ <b>للمديرين وفوق — مش للي بيسلّم بس.</b> المدير العادي
/// محتاج يقرا أسماء الجهات عشان يفهم سجلات التسليم اللي بيشوفها،
/// حتى لو مش بيسلّم بنفسه.</para>
/// </summary>
public sealed record GetHandoverDestinationsQuery
    : IRequest<Result<IReadOnlyList<HandoverDestination>>>;
