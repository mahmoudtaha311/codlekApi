namespace Codlek.Core.Racks;

/// <summary>
/// قواعد تسجيل محطة فحص — <b>الجزء النقي</b>.
///
/// <para>🔴 <b>ودي النقطة الوحيدة في سطح الراكة اللي بتقبل كتابة
/// من غير مفتاح</b> — لأن المحطة الجديدة <b>مالهاش مفتاح لسه</b>،
/// وده أصل المشكلة اللي بتحلّها. واللي بيحميها هو كود التفعيل:
/// قصير العمر، بيتستهلك مرة واحدة، وبيتقفل بعد خمس محاولات
/// غلط.</para>
/// </summary>
public static class RackRegistration
{
    /// <summary>
    /// أقصر كود يستاهل بحث.
    ///
    /// <para>⚠️ الكود تمن حروف وشرطة — والشرطة بتتشال قبل القياس في
    /// بعض العملاء، فالحد على <b>التمنية</b>.</para>
    /// </summary>
    public const int MinCodeLength = 8;

    /// <summary>
    /// بعد كام محاولة غلط الكود يتقفل.
    ///
    /// <para>🔴 <b>والمحاولة الغلط بتتعدّ على <u>كل</u> أكواد نفس
    /// البادئة</b> — وده اللي بيخلّي التخمين غير مجدي بدل ما يبقى
    /// مجرد إبطاء. والزيادة الجماعية دي <b>مقصودة ومش حاجة
    /// تتصلّح</b>: التصادم في البادئة (٤ حروف من ٣١) نادر لدرجة
    /// إن كود شرعي بيتأثر بالتخمين على كود تاني حالة بتحصل مرة كل
    /// مئتين ألف.</para>
    /// </summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>
    /// الكود بعد التنضيف — <b>كبير الحروف</b>.
    ///
    /// <para>⚠️ الفني بيكتبه من الشاشة، والكيبورد ممكن يكون على
    /// حروف صغيرة.</para>
    /// </summary>
    public static string Clean(string? raw) => (raw ?? "").Trim().ToUpperInvariant();

    /// <summary>الكود ده يستاهل بحث؟</summary>
    public static bool Usable(string? raw) => Clean(raw).Length >= MinCodeLength;

    /// <summary>
    /// الكود ده مقفول من كتر المحاولات؟
    ///
    /// <para>⚠️ <b>والفحص ده جوّه شرط المطابقة في القديم</b> — يعني
    /// الكود المقفول <b>مابيتطابقش</b> أصلاً، فالرد عليه «غلط أو
    /// اتستخدم» زي أي كود مش موجود. واللي بيحاول مايعرفش إنه لقى
    /// الكود الصح وقفله.</para>
    /// </summary>
    public static bool LockedOut(int failedAttempts) =>
        failedAttempts >= MaxFailedAttempts;

    /// <summary>
    /// اسم المحطة النهائي.
    ///
    /// <para>⚠️ <b>اللي الراكة بعتته يكسب، وبعده اللي المدير كتبه
    /// وقت عمل الكود، وبعده اسم عام.</b> والترتيب ده مقصود: الفني
    /// اللي بيسجّل قدام البنش عارف هو فين، والمدير كتب نيّته من
    /// أسبوع.</para>
    ///
    /// <para>⚠️ ومفيش <c>null</c> ومفيش فراغ — الاسم بيظهر جنب كل
    /// فحص رفعته المحطة.</para>
    /// </summary>
    public static string Name(string? sent, string? intended)
    {
        string fromRack = (sent ?? "").Trim();

        if (fromRack.Length > 0) return fromRack;

        string fromManager = (intended ?? "").Trim();

        return fromManager.Length > 0 ? fromManager : "راكة";
    }

    /// <summary>
    /// كود المحطة من رقمها — <c>RACK-001</c>.
    ///
    /// <para>⚠️ تلات خانات بأصفار. الورشة فيها عشرات المحطات، ورقم
    /// من غير حشو كان بيخلّي الترتيب الأبجدي يحط <c>RACK-10</c>
    /// قبل <c>RACK-2</c>.</para>
    /// </summary>
    public static string Code(int number) => "RACK-" + number.ToString("000");

    /// <summary>⚠️ اسم عدّاد المحطات — <b>مفتاح مخزّن</b>.</summary>
    public const string Counter = "rack";
}
