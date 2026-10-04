namespace Codlek.Application.Contracts.Brands;

public sealed record SaveBrandRequest(string? Name, int? SortOrder, bool? IsActive);
