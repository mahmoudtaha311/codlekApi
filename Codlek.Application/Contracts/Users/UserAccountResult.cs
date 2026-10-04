namespace Codlek.Application.Contracts.Users;

/// <summary>
/// نتيجة أي إجراء على حساب.
///
/// <para>⚠️ متعمّدة صغيرة: بعد كل إجراء الواجهة بتعيد تحميل القايمة
/// كلها عشان <c>CanManage</c> يتحسب من جديد. رد شايل صف كامل كان
/// بيخلّي الواجهة تفتكر إنها ماتحتاجش تعيد التحميل.</para>
/// </summary>
public sealed record UserAccountResult(Guid Id, string DisplayName, string Code);
