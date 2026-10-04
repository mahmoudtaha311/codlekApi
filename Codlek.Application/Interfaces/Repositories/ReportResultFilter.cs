namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// فلتر نتيجة الفحص.
///
/// <para>⚠️ <b>ودي على الفحص نفسه، مش على «آخر فحص» للاب.</b>
/// قايمة الفحوص بتعرض <b>أحداث</b>؛ وقايمة الأجهزة بتعرض
/// <b>حالات</b>. والفرق ده هو اللي بيخلّي نفس الكلمة
/// («سليم») تعني حاجتين في الشاشتين.</para>
/// </summary>
public enum ReportResultFilter
{
    Any,

    /// <summary><c>result=healthy</c> — مفيش فشل.</summary>
    Healthy,

    /// <summary><c>result=repair</c> — فيه فشل.</summary>
    NeedsRepair,

    /// <summary>
    /// <c>result=nohard</c> — مفيش هارد.
    ///
    /// <para>⚠️ المقارنة على النص <c>"No Hard"</c> بالحرف — ده اللي
    /// الراكة بتكتبه، ومفيش علم منفصل.</para>
    /// </summary>
    NoHard,

    /// <summary>
    /// 🔴 <c>result=unresolved</c> — <b>السبب التالت في جرس
    /// التحذيرات</b>.
    ///
    /// <para>كان بيتعدّ في عدّادات التحذيرات ومفيش أي طريقة توصل
    /// للفحوص دي — رقم في أيقونة من غير باب. وهو علم على
    /// <b>التقرير</b> مش على الجهاز، عشان كده مكانه هنا مش في قايمة
    /// الأجهزة.</para>
    /// </summary>
    NeedsDeviceResolution
}
