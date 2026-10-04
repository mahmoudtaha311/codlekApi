using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Containers;
using MediatR;

namespace Codlek.Application.Features.Containers.UpdateContainer;

/// <summary>
/// تعديل حاوية.
///
/// <para>🔴 <b>مفيش <c>Code</c> في الأمر ده عن قصد.</b> الرمز مطبوع
/// على الشحنة وموجود على لابات اتفحصت خلاص؛ تغييره بيخلّي اللي ماسك
/// ورقة الاستيراد مايلاقيش حاجة. واللي بيتعدّل: الاسم والترتيب
/// والتفعيل.</para>
/// </summary>
public sealed record UpdateContainerCommand(
    Guid Id,
    string? Name,
    int? SortOrder,
    bool? IsActive) : IRequest<Result<ContainerListItem>>;
