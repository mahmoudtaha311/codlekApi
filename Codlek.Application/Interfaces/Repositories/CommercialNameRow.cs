namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// اسم تجاري متخزّن على جهاز أو فحص — لتنضيف أكواد المصنّع.
/// </summary>
public sealed record CommercialNameRow(Guid Id, string? CommercialModelName);
