namespace Codlek.Core.Text;

/// <summary>نتيجة الحل.</summary>
public readonly record struct BrandResult(Guid? BrandId, string Name, BrandMatch Match)
{
    public static BrandResult None(string raw) => new(null, raw, BrandMatch.Unknown);
}
