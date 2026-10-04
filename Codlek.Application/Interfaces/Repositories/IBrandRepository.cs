using Codlek.Application.Contracts.Brands;
using Codlek.Core.Entities;
using Codlek.Core.Text;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// ماركات اللابات وأسماءها البديلة.
/// </summary>
public interface IBrandRepository
{
    /// <summary>
    /// ⚠️ <b>الموقوفة بترجع برضه</b> — فني متسند لماركة اتوقفت لازم
    /// اسمها يفضل ظاهر مش يبان فاضي.
    /// </summary>
    Task<IReadOnlyList<BrandRow>> ListAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>بيجيب الماركة <b>ومعاها أسماءها</b> — للتعديل.</summary>
    Task<LaptopBrand?> FindWithAliasesAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    Task<LaptopBrand?> FindAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<bool> NameTakenAsync(
        Guid tenantId, string normalizedName, Guid? exceptId = null,
        CancellationToken ct = default);

    /// <summary>
    /// ماركة اسمها المطبَّع = المفتاح ده، من غير اللي بنعدّله.
    ///
    /// <para>بترجّع <c>null</c> لو مفيش — واسمها راجع عشان الرسالة
    /// تقول «ده اسم ماركة فلان» بدل «محجوز».</para>
    /// </summary>
    Task<LaptopBrand?> FindByNormalizedNameAsync(
        Guid tenantId, string normalizedName, Guid? exceptId = null,
        CancellationToken ct = default);

    /// <summary>
    /// 🔴 الاسم البديل ده متسجّل لأي ماركة في الشركة؟ — <b>مش جوّه
    /// ماركة واحدة</b>.
    /// </summary>
    Task<(Guid BrandId, string BrandName)?> FindAliasOwnerAsync(
        Guid tenantId, string normalizedValue, CancellationToken ct = default);

    Task<LaptopBrandAlias?> FindAliasAsync(
        Guid tenantId, Guid brandId, string normalizedValue, CancellationToken ct = default);

    void Add(LaptopBrand brand);

    void AddAlias(LaptopBrandAlias alias);

    void RemoveAlias(LaptopBrandAlias alias);

    Task<int> CountTechniciansAsync(Guid brandId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> AliasValuesAsync(Guid brandId, CancellationToken ct = default);

    /// <summary>
    /// قواعد الماركات كبيانات — <b>عشان <see cref="BrandToken"/>
    /// تفضل دالة نقية</b>.
    ///
    /// <para>⚠️ والراكة بتشغّل <b>نفس</b> الدالة على نسختها المحفوظة،
    /// فالقواعد لازم تطلع بنفس الشكل بالحرف.</para>
    /// </summary>
    Task<IReadOnlyList<BrandRule>> RulesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// أسماء المصنّعين الخام في الأجهزة، بعددها.
    ///
    /// <para>⚠️ <b>التجميع على الخام مقصود:</b> الهدف إن صاحب الشغل
    /// يشوف الأسماء <b>زي ما وصلت</b> عشان يكتبها أسماء بديلة.</para>
    /// </summary>
    Task<IReadOnlyList<(string Name, int Count)>> RawManufacturersAsync(
        Guid tenantId, CancellationToken ct = default);
}
