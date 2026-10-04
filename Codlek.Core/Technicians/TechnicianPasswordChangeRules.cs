namespace Codlek.Core.Technicians;

/// <summary>
/// قواعد «الفني بيغيّر باسورده بنفسه» — <b>دالة نقية</b>.
///
/// <para>🔴 <b>النقطة دي اتعملت عشان عطل حقيقي في الميدان.</b> شاشة
/// «حسابي» على الراكة كانت بترد «الباسورد الحالي غلط» على الباسورد
/// الصح، لأنها بتقارن ببصمة محلية والفني المركزي بصمته المحلية
/// <b>فاضية</b> عن قصد (باسورده على السيرفر). يعني مكانش فيه أي طريق
/// في النظام كله يغيّر بيه فني باسورده — وبالتالي علامة «لازم يغيّر
/// الباسورد» كانت متعلّمة على كل فني للأبد.</para>
///
/// <para>🔴 <b>والترتيب هنا هو الأمان نفسه:</b> القفل ← الباسورد
/// الحالي ← الإيقاف ← سياسة الجديد. لو الرد فرّق بين «الحالي غلط»
/// و«الجديد قصير» <b>قبل</b> ما يتأكد من الحالي، يبقى اللي مش عارف
/// الباسورد اتعلّم حاجة من الرد.</para>
///
/// <para>⚠️ <b>وده السبب إنها دالة نقية.</b> الترتيب ده هو كل
/// الحماية، والحاجة الوحيدة اللي تثبته فحص بيجرّب كل ترتيب ممكن من
/// غير قاعدة ولا شبكة.</para>
/// </summary>
public static class TechnicianPasswordChangeRules
{
    /// <summary>أقل طول للباسورد الجديد — نفس حد إعادة التعيين من الموقع.</summary>
    public const int MinLength = 4;

    /// <summary>القرار — <b>الكود للآلة والرسالة للبني آدم</b>.</summary>
    public sealed record Decision(bool Ok, string Code, string Message);

    /// <param name="throttled">العدّاد وصل الحد — بيتحسب من القاعدة قبل النداء.</param>
    /// <param name="found">فيه فني بالاسم ده في شركة المحطة دي.</param>
    /// <param name="currentOk">الباسورد الحالي اتأكد منه فعلاً.</param>
    /// <param name="isActive">الحساب مش موقوف.</param>
    /// <param name="next">الباسورد الجديد زي ما اتكتب.</param>
    /// <param name="confirm">تأكيد الباسورد الجديد.</param>
    /// <param name="sameAsCurrent">الجديد هو نفسه الحالي.</param>
    public static Decision Decide(
        bool throttled, bool found, bool currentOk, bool isActive,
        string? next, string? confirm, bool sameAsCurrent)
    {
        /*
          🔴 **القفل قبل أي حاجة تانية.**

          النقطة دي بتاخد باسورد وبترد «صح ولا غلط» — يعني هي
          **عرّافة باسوردات** بالظبط زي نقطة الدخول. لو العدّاد اتفحص
          بعد التحقق، اللي بيخمّن بياخد إجابته الأول والقفل بييجي بعد
          ما الضرر حصل.
        */
        if (throttled)
            return new Decision(false, TechnicianAuthCodes.TooManyAttempts,
                TechnicianLoginRules.TooManyMessage);

        /*
          ⚠️ **نفس الرسالة للاسم المش موجود وللباسورد الغلط** —
          منقولة بالحرف من مسار الدخول.
        */
        if (!found || !currentOk)
            return new Decision(false, TechnicianAuthCodes.InvalidCredentials,
                "الباسورد الحالي غلط");

        // ⚠️ **الإيقاف بعد التحقق، زي الدخول بالظبط.** الموقوف بيعرف
        //    إنه موقوف بعد ما يثبت إنه هو.
        if (!isActive)
            return new Decision(false, TechnicianAuthCodes.Suspended,
                "غير مسموح بالدخول حالياً — كلّم المدير");

        next ??= "";
        confirm ??= "";

        if (next.Length < MinLength)
            return new Decision(false, TechnicianAuthCodes.WeakPassword,
                $"الباسورد الجديد لازم يكون {MinLength} حروف على الأقل");

        if (!string.Equals(next, confirm, StringComparison.Ordinal))
            return new Decision(false, TechnicianAuthCodes.Mismatch,
                "التأكيد مش مطابق للباسورد الجديد");

        // ⚠️ **وده بعد التأكيد مش قبله.** لو جه قبله، الفني اللي كتب
        //    الجديد غلط في خانة التأكيد كان بيشوف «زي القديم»
        //    ويستغرب.
        if (sameAsCurrent)
            return new Decision(false, TechnicianAuthCodes.SameAsCurrent,
                "الباسورد الجديد زي القديم");

        return new Decision(true, TechnicianAuthCodes.Ok, "الباسورد اتغيّر");
    }

    /// <summary>
    /// المحاولة دي تتسجّل كمحاولة فاشلة في عدّاد الدخول؟
    ///
    /// <para>🔴 <b>الباسورد الحالي الغلط بس — مش أي رفض.</b> العدّاد
    /// ده هو نفسه عدّاد الدخول (محطة + اسم، ١٠ في ٥ دقايق). لو كل
    /// رفض اتسجّل فيه، الفني اللي كتب باسورد جديد قصير تلات مرات
    /// بيتقفل عليه <b>الدخول</b> — عقوبة على غلطة إملائية.</para>
    ///
    /// <para>🔴 <b>والنجاح مابيتسجّلش خالص.</b> صف ناجح في الجدول ده
    /// معناه حاجة تانية: السيرفر بيستعمله <b>كدليل إن الفني كان
    /// مصرّح له</b> وقت شغل أوفلاين. وتغيير باسورد مش دخول، وتسجيله
    /// كده بيمدّ تصريح مالوش أساس.</para>
    /// </summary>
    public static bool CountsAsFailedAttempt(string code) =>
        code == TechnicianAuthCodes.InvalidCredentials;
}
