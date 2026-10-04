using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Text;
using MediatR;

namespace Codlek.Application.Features.Containers.CreateContainer;

/// <summary>
/// ⚠️ <b>الراكة كمان بتعمل حاويات وقت المزامنة</b> لما الفني يكتب رمز
/// جديد أوفلاين. النقطة دي للمدير اللي عايز يجهّز الليستة قبل ما
/// الشحنة توصل البنش.
/// </summary>
public sealed class CreateContainerCommandHandler(
    IContainerRepository containers,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<CreateContainerCommand, Result<ContainerListItem>>
{
    public async Task<Result<ContainerListItem>> Handle(
        CreateContainerCommand command, CancellationToken cancellationToken)
    {
        string code = command.Code.Trim();

        if (code.Length < ContainerCode.MinCodeLength)
            return Result.Failure<ContainerListItem>(ContainerErrors.CodeMissing);

        /*
          ⚠️ **القص قبل التطبيع، زي القديم بالحرف.**

          لأن التطبيع بيشيل الشرطات والمسافات — فرمز ٤٥ حرف بشرطات
          ممكن يطلع مطبَّعه ٣٨. ولو طبّعنا الأول وقصّينا بعدين، الرمز
          المخزّن كان هيبقى أطول من اللي القديم بيخزّنه لنفس الإدخال.
        */
        if (code.Length > ContainerCode.MaxCodeLength)
            code = code[..ContainerCode.MaxCodeLength];

        string key = ContainerCode.Normalize(code);

        // 🔴 فاضي معناه «مفيش ولا حرف ولا رقم» — مش «مكتبتش حاجة».
        if (key.Length == 0)
            return Result.Failure<ContainerListItem>(ContainerErrors.CodeInvalid);

        if (await containers.CodeTakenAsync(me.TenantId, key, cancellationToken))
            return Result.Failure<ContainerListItem>(ContainerErrors.CodeTaken(code));

        string name = command.Name.Trim();

        var row = new ImportContainer
        {
            TenantId = me.TenantId,
            Code = code,
            NormalizedCode = key,
            Name = name.Length > 120 ? name[..120] : name,
            SortOrder = command.SortOrder,

            // ⚠️ **اسم اللي عمل الحاوية لقطة، مش ربط.** لو المدير
            // اتشال بعدين، الحاوية لازم تفضل بتقول مين عملها.
            CreatedByName = me.DisplayName,
        };

        containers.Add(row);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ContainerListItem(
            row.Id, row.Code, row.Name, row.IsActive, 0,
            row.CreatedByName, row.CreatedAtUtc));
    }
}
