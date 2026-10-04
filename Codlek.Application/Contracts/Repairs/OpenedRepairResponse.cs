namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// رد فتح أمر صيانة.
///
/// <para>⚠️ الكود راجع عشان المدير يقدر يطبعه على طول — ومن غيره كان
/// لازم يعيد تحميل القايمة ويدوّر عليه.</para>
/// </summary>
public sealed record OpenedRepairResponse(Guid Id, string PublicCode);
