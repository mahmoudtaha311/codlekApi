namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// نتيجة حفظ ماركات الفني.
///
/// <para>⚠️ الرسالة بتفرّق بين «اتحفظت» و«مفيش تغيير» — المدير
/// بيدوس حفظ من غير ما يعدّل، والرد لازم يقوله إنه مااتغيّرش حاجة
/// بدل ما يفتكر إنه عمل حاجة.</para>
/// </summary>
public sealed record TechnicianBrandsResponse(
    IReadOnlyList<Guid> BrandIds,
    string Message);
