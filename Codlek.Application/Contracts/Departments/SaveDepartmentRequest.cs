namespace Codlek.Application.Contracts.Departments;

/// <summary>
/// إنشاء أو تعديل قسم.
///
/// <para>⚠️ <b>كله nullable ماعدا الاسم وقت الإنشاء.</b> التعديل بيبعت
/// الحقل اللي اتغيّر بس، و<c>false</c> جاي من حقل ناقص كان هيوقف أقسام
/// شغّالة.</para>
/// </summary>
public sealed record SaveDepartmentRequest(
    string? Name,
    string? Code = null,
    int? SortOrder = null,
    bool? IsActive = null);
