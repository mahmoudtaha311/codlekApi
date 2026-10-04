namespace Codlek.Application.Contracts.Audit;

/// <summary>خيار في قايمة فلتر: القيمة للفلترة والنص للعرض.</summary>
public sealed record NamedOption(string Value, string Label);
