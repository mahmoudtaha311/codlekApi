namespace Codlek.Application.Contracts.Containers;

/// <summary>
/// إنشاء أو تعديل حاوية.
///
/// <para>🔴 <b>الرمز بيتستعمل وقت الإنشاء بس.</b> التعديل بيتجاهله
/// عن قصد — الرمز مطبوع على الشحنة وموجود على لابات اتفحصت خلاص،
/// فتغييره بيخلّي اللي ماسك ورقة الاستيراد مايلاقيش حاجة.</para>
/// </summary>
public sealed record SaveContainerRequest(
    string? Code,
    string? Name,
    int? SortOrder,
    bool? IsActive);
