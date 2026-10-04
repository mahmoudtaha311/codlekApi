namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// عطل متسجّل على أمر الصيانة — لقطة من الفحص.
///
/// <para>🔴 <b>أسماء الحقول هنا مش أسماء الكيان.</b>
/// <c>Code ← IssueCode</c> و<c>Title ← IssueTitleSnapshot</c>. والداش
/// بورد بتقرا <c>code</c> و<c>title</c>، فأي إعادة تسمية بتطلّع
/// عمود فاضي في الشاشة من غير أي خطأ.</para>
///
/// <para>⚠️ و«لقطة» معناها إن تعديل اسم العطل في الكتالوج
/// <b>مابيغيّرش</b> الأوامر القديمة — وده مقصود: سجل الصيانة لازم
/// يقول العطل كان اسمه إيه وقتها.</para>
/// </summary>
public sealed record RepairIssueItem(
    string Code,
    string Title,
    string Category,
    string CategoryText,
    bool Resolved);
