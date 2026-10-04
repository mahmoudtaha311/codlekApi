namespace Codlek.Application.Contracts.Common;

/// <summary>
/// صفحة من قايمة.
///
/// <para>⚠️ <c>AwaitingApproval</c> في الآخر وبقيمة افتراضية
/// <c>null</c> — <b>قايمة الصيانة لوحدها هي اللي بتملاه</b>.</para>
///
/// <para>🔴 <b>والرقم ده بيتحسب برّه كل الفلاتر وبرّه التصفيح.</b>
/// هو «كام أمر مستني قرار في الشركة كلها» — مش «كام في الصفحة دي».
/// ولو اتحسب جوّه الفلتر، المحاسب بيفلتر على فني واحد والشارة بتقول
/// «٢ مستنيين» والحقيقة ٤٠.</para>
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    int? AwaitingApproval = null);
