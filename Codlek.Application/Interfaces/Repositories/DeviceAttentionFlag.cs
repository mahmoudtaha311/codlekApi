namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// سبب التحذير اللي القايمة مفلترة عليه.
///
/// <para>🔴 <b>الجرس بيعدّ تلات أسباب</b> — وعشان كده
/// <see cref="Attention"/> موجودة: دي اللي الجرس نفسه بيبعتها،
/// وبتجمع الاتنين.</para>
///
/// <para>⚠️ <b>والسبب التالت («فحص من غير جهاز») مش هنا عن
/// قصد</b> — ده علم على <b>التقرير</b> مش على الجهاز، والتقارير دي
/// أصلاً مالهاش جهاز مربوط (ودي المشكلة نفسها). مكانه فلتر في قايمة
/// الفحوص.</para>
///
/// <para>⚠️ والقيم دي بتتحوّل من نص
/// (<c>partchanged|duplicate|attention</c>) في الـHandler — فإعادة
/// تسميتها هنا مابتكسرش الرابط.</para>
/// </summary>
public enum DeviceAttentionFlag
{
    Any,

    /// <summary>قطعة اتغيّرت — <c>flag=partchanged</c>.</summary>
    PartChanged,

    /// <summary>مشكوك إنه مكرّر — <c>flag=duplicate</c>.</summary>
    DuplicateSuspected,

    /// <summary>الاتنين مع بعض — <c>flag=attention</c>، وده اللي الجرس بيبعته.</summary>
    Attention
}
