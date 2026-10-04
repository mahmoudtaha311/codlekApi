using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.GetActivationCodes;

/// <summary>
/// أكواد التفعيل المستنية.
///
/// <para>🔴 <b>وولا واحد فيهم بيرجّع الكود نفسه.</b> اللي في القاعدة
/// بصمة، واللي بيترجّع بادئة — والكود الكامل عمره ما بيترجّع غير مرة
/// واحدة وقت الإنشاء.</para>
/// </summary>
public sealed class GetActivationCodesQueryHandler(
    IRackRepository racks, ICurrentUser me)
    : IRequestHandler<GetActivationCodesQuery, Result<IReadOnlyList<ActivationCodeRow>>>
{
    public async Task<Result<IReadOnlyList<ActivationCodeRow>>> Handle(
        GetActivationCodesQuery query, CancellationToken cancellationToken)
    {
        var rows = await racks.PendingCodesAsync(me.TenantId, cancellationToken);

        /*
          🔴 **لحظة واحدة لكل الصفوف.**

          `DateTime.UtcNow` جوّه الحلقة معناها إن كل صف بيتقارن بوقت
          مختلف — وكود بيخلص في نفس الثانية كان بيطلع «منتهي» في
          القراية دي و«صالح» في اللي بعديها من غير ما حد يلمس حاجة.
        */
        var now = DateTime.UtcNow;

        var items = rows.Select(c => new ActivationCodeRow(
            c.Id,
            c.CodePrefix,
            c.IntendedName,
            c.IntendedLocation,
            c.CreatedByName,
            c.CreatedAtUtc,
            c.ExpiresAtUtc,
            RackPolicy.IsExpired(c.ExpiresAtUtc, now),
            c.FailedAttempts)).ToList();

        return Result.Success<IReadOnlyList<ActivationCodeRow>>(items);
    }
}
