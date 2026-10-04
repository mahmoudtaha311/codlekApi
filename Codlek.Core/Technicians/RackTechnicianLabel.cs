namespace Codlek.Core.Technicians;

/// <summary>
/// اسم فني المحطة لما الاسم مش متاح.
///
/// <para>🔴 <b>الفني هنا هوية <u>محطة</u>، مش حساب موقع.</b> الكود
/// بيتكتب على الفحص من الراكة، والاسم لقطة — وفيه فحوص قديمة اسمها
/// فاضي خالص. فالصفحة لازم تفضل تفتح بكود الفني.</para>
///
/// <para>⚠️ <b>و«فني محطة T001» أنفع من «الاسم غير متاح»:</b>
/// الأولانية بتقول للمدير يدوّر فين، والتانية بتقول إن فيه حاجة
/// باظت.</para>
/// </summary>
public static class RackTechnicianLabel
{
    public static string Neutral(string code) => "فني محطة " + code;
}
