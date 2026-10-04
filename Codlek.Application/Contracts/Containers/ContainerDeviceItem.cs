namespace Codlek.Application.Contracts.Containers;

public sealed record ContainerDeviceItem(
    Guid Id,
    string PublicCode,
    string Manufacturer,
    string Model,
    DateTime LastSeenAtUtc,
    int TestCount,

    /// <summary>فشل + خطأ في آخر فحص. صفر = سليم.</summary>
    int LastProblems);
