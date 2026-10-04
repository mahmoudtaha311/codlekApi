namespace Codlek.Application.Interfaces;

/// <summary>
/// مفاتيح محطات الفحص — <b>إصدار وتحقق</b>.
///
/// <para>🔴 <b>والتحقق موجود هنا لأن تسع نقط معتمدة عليه.</b> لو
/// كل نقطة تحققت بنفسها، أول واحدة تنسى شرط الحالة كانت بتفتح
/// محطة ملغية — والملغي غالباً اتلغى عشان مفتاحه اتسرق.</para>
///
/// <para>⚠️ <b>والمفتاح بيرجع <u>مرة واحدة</u> وقت التسجيل.</b>
/// بصمته هي اللي في القاعدة، فمفيش دالة هنا بترجّعه.</para>
/// </summary>
public interface IRackKeys
{
    /// <summary>مفتاح جديد + بصمته وملحه.</summary>
    (string Key, string Hash, string Salt) Issue();

    /// <summary>
    /// المفتاح ده بتاع البصمة دي؟
    ///
    /// <para>⚠️ <b>بالمعاملات المشتركة مع الراكة بايت ببايت.</b>
    /// نسخة تانية من البصمة هنا معناها إن كل راكة في الميدان
    /// بتفقد مفتاحها في لحظة واحدة.</para>
    /// </summary>
    bool Verify(string key, string storedHash, string storedSalt);
}
