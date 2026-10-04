namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// رد أي إجراء على أمر صيانة.
///
/// <para>⚠️ متعمّد صغير: بعد كل إجراء الواجهة بتعيد تحميل الصف — ورد
/// شايل الأمر كامل كان بيخلّيها تفتكر إنها ماتحتاجش.</para>
/// </summary>
public sealed record RepairActionResponse(string Message);
