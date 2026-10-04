using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Brands;

public static class BrandErrors
{
    public static readonly Error NameRequired =
        new("brand.name_required", "اسم الماركة مطلوب.", 400);

    /// <summary>
    /// ⚠️ التكرار بيتقاس على الشكل المطبَّع مش الخام — «hp» و«HP »
    /// نفس الماركة.
    /// </summary>
    public static Error NameTaken(string name) =>
        new("brand.name_taken", $"فيه ماركة «{name}» خلاص.", 400);

    public static readonly Error NotFound =
        new("brand.not_found", "الماركة دي مش موجودة.", 404);

    public static readonly Error AliasRequired =
        new("brand.alias_required", "الاسم البديل مطلوب.", 400);

    /// <summary>
    /// 🔴 <b>الاسم البديل فريد على مستوى الشركة كلها، مش جوّه
    /// الماركة.</b>
    ///
    /// <para>لو ماركتين ادّعوا «HP»، حل اللاب بيبقى معتمد على ترتيب
    /// الصفوف — ونفس اللاب يتحل لماركة مختلفة بعد ما حد يغيّر
    /// الترتيب.</para>
    /// </summary>
    public static Error AliasTakenBy(string raw, string brandName) =>
        new("brand.alias_taken", $"«{raw}» متسجّل خلاص لماركة «{brandName}».", 400);

    /// <summary>
    /// ⚠️ والاسم اللي هو نفسه اسم ماركة تانية بيترفض كمان — غير كده
    /// الاسم بيبقى ليه معنيين.
    /// </summary>
    public static Error AliasIsAnotherBrandName(string raw, string brandName) =>
        new("brand.alias_is_brand_name", $"«{raw}» هو اسم ماركة «{brandName}».", 400);

    public static readonly Error AliasNotFound =
        new("brand.alias_not_found", "الاسم البديل ده مش موجود.", 404);
}
