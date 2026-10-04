namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// إنشاء حساب فني.
///
/// <para>⚠️ <b>والكود مش في الطلب</b> — السيرفر بيولّده ويتأكد إنه
/// فاضي. تحديده من الطلب كان بيخلّي المدير يكتب كود موجود ويخلط شغل
/// فنيين.</para>
/// </summary>
public sealed record CreateTechnicianRequest(
    string? DisplayName,
    string? Username,
    string? Password,
    int? Specialty,
    Guid? DepartmentId);
