namespace Codlek.Application.Contracts.Reports;

/// <summary>
/// رد المسح والاسترجاع.
///
/// <para>⚠️ متعمّد صغير: بعد الإجراء الواجهة بتعيد تحميل الفحص — ورد
/// شايل الفحص كامل كان بيخلّيها تفتكر إنها ماتحتاجش. والعدّادات
/// (اللوحة والتقرير اليومي والجهاز) كلها اتغيّرت معاه.</para>
/// </summary>
public sealed record ReportActionResponse(Guid Id, bool IsDeleted, string Message);
