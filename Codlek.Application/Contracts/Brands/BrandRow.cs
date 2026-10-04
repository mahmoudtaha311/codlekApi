namespace Codlek.Application.Contracts.Brands;

public sealed record BrandRow(
    Guid Id,
    string Name,
    bool IsActive,
    int SortOrder,

    /// <summary>الأسماء التانية اللي بتوصل لنفس الماركة.</summary>
    IReadOnlyList<string> Aliases,

    /// <summary>كام فني متقيّد بالماركة دي.</summary>
    int TechnicianCount);
