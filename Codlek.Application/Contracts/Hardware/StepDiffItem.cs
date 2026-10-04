namespace Codlek.Application.Contracts.Hardware;

/// <summary>
/// نتيجة مرحلة واحدة في الفحصين.
///
/// <para>⚠️ المراحل بتتطابق <b>بالاسم مش بالترتيب</b>: قايمة
/// المراحل بتتغيّر بين نسخ البرنامج، ومرحلة اتضافت في النص بتزحزح
/// كل اللي بعدها — والمطابقة بالفهرس كانت هتقول إن نص المراحل
/// اتغيّرت نتيجتها.</para>
/// </summary>
/// <param name="LeftStatus">
/// ⚠️ <c>-1</c> معناها «المرحلة دي مش موجودة في الجهة دي» —
/// والنص بيبقى «—».
/// </param>
public sealed record StepDiffItem(
    string Title,
    int LeftStatus,
    string LeftText,
    int RightStatus,
    string RightText,
    string Direction);
