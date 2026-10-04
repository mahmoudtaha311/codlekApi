using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Features.Containers.UpdateContainer;

public sealed class UpdateContainerCommandHandler(
    IContainerRepository containers,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<UpdateContainerCommand, Result<ContainerListItem>>
{
    public async Task<Result<ContainerListItem>> Handle(
        UpdateContainerCommand command, CancellationToken cancellationToken)
    {
        var row = await containers.FindAsync(me.TenantId, command.Id, cancellationToken);

        if (row is null) return Result.Failure<ContainerListItem>(ContainerErrors.NotFound);

        /*
          ⚠️ **الاسم بيتكتب دايماً — حتى لو فاضي.**

          ده سلوك القديم بالحرف، ومختلف عن الأقسام: هناك الاسم الفاضي
          معناه «ماتغيّرش». هنا الاسم وصف اختياري (الرمز هو المفتاح)،
          فتفضيته حاجة مشروعة: «شلت الوصف الغلط».
        */
        string name = (command.Name ?? "").Trim();
        row.Name = name.Length > 120 ? name[..120] : name;

        // ⚠️ والترتيب والتفعيل بيتغيّروا لو اتبعتوا بس — `false` من
        // حقل ناقص كان بيوقف حاويات شغّالة.
        if (command.SortOrder is { } sort) row.SortOrder = sort;
        if (command.IsActive is { } active) row.IsActive = active;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        int devices = await containers.CountDevicesAsync(
            me.TenantId, row.Id, cancellationToken);

        return Result.Success(new ContainerListItem(
            row.Id, row.Code, row.Name, row.IsActive, devices,
            row.CreatedByName, row.CreatedAtUtc));
    }
}
