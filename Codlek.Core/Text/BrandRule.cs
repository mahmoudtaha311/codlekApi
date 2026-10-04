namespace Codlek.Core.Text;

/// <summary>ماركة واحدة وأسماؤها البديلة — <b>بيانات، مش جدول</b>.</summary>
public readonly record struct BrandRule(Guid Id, string Name, IReadOnlyList<string> Aliases);
