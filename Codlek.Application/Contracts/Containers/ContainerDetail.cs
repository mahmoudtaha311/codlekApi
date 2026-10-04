namespace Codlek.Application.Contracts.Containers;

public sealed record ContainerDetail(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    string CreatedByName,
    DateTime CreatedAtUtc,
    IReadOnlyList<ContainerDeviceItem> Devices);
