namespace Codlek.Application.Contracts.Containers;

public sealed record ContainerListItem(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,

    /// <summary>كام لاب متسجّل في الحاوية دي.</summary>
    int DeviceCount,

    string CreatedByName,
    DateTime CreatedAtUtc);
