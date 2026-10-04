using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.DeleteActivationCode;

/// <summary>
/// مسح كود تفعيل — <b>المنتهي اللي مااتستعملش بس</b>.
///
/// <para>🔴 <b>المستهلك مابيتمسحش:</b> هو شايل
/// <c>ConsumedByRackId</c> — الدليل الوحيد على إن المحطة الفلانية
/// اتسجّلت بأنهي إذن ومن مين. مسحه بيقطع خيط المراجعة.</para>
///
/// <para>🔴 <b>واللي لسه صالح مابيتمسحش كمان:</b> ممكن يكون فيه حد
/// ماسكه دلوقتي عشان يفعّل بيه محطة. اللي عايز يبطّله قبل ميعاده، ده
/// طلب تاني اسمه «إلغاء» ومالوش وجود في الكيان.</para>
/// </summary>
public sealed class DeleteActivationCodeCommandHandler(
    IRackRepository racks,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<DeleteActivationCodeCommand, Result<RackActionResponse>>
{
    public async Task<Result<RackActionResponse>> Handle(
        DeleteActivationCodeCommand command, CancellationToken cancellationToken)
    {
        var row = await racks.FindCodeAsync(me.TenantId, command.Id, cancellationToken);

        if (row is null)
            return Result.Failure<RackActionResponse>(RackErrors.CodeNotFound);

        /*
          ⚠️ **والفحص ده مش تكرار للفلتر بتاع القايمة.**

          القايمة مابتعرضش المستهلك، بس النقطة دي بتتنده بـ**معرّف** —
          ومعرّف كود مستهلك ممكن يتبعت من سكريبت أو من صفحة قديمة
          مفتوحة في تاب. الرفض لازم يبقى هنا، مش في الواجهة.
        */
        var verdict = RackPolicy.Deletion(
            row.ConsumedAtUtc, row.ExpiresAtUtc, DateTime.UtcNow);

        if (verdict == CodeDeletion.AlreadyUsed)
            return Result.Failure<RackActionResponse>(RackErrors.CodeAlreadyUsed);

        if (verdict == CodeDeletion.StillValid)
            return Result.Failure<RackActionResponse>(RackErrors.CodeStillValid);

        racks.RemoveCode(row);

        /*
          🔴 **والبادئة بتتكتب في السجل هنا.**

          السطر بيتكتب والصف بيروح — فالبادئة هي الحاجة الوحيدة اللي
          بتربط «اتعمل كود لمحطة كذا» بـ«اتمسح كود». من غيرها، سطرين
          في السجل لنفس المحطة مش معروف لو هما نفس الكود.
        */
        audit.Record(
            AuditActions.RackCodeDeleted, "Rack",
            row.Id, row.CodePrefix,
            $"اتمسح كود تفعيل منتهي لمحطة «{row.IntendedName}»");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RackActionResponse("اتمسح"));
    }
}
