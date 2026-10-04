namespace Codlek.Core.Technicians;

/// <summary>
/// قواعد دخول الفني من محطة فحص — <b>الجزء النقي</b>.
///
/// <para>🔴 <b>والترتيب هو الأمان نفسه مش تنظيم:</b> القفل ←
/// الباسورد ← الإيقاف. مكتوب هنا كدوال نقية عشان الترتيب يتجرّب من
/// غير قاعدة بيانات.</para>
/// </summary>
public static class TechnicianLoginRules
{
    /// <summary>
    /// عدد المحاولات الفاشلة المسموحة من <b>نفس المحطة على نفس
    /// الاسم</b>.
    ///
    /// <para>🔴 <b>والعدّاد في القاعدة مش في ذاكرة العملية.</b> الحد
    /// على مستوى HTTP بيقسّم بالمحطة بس — مايقدرش يقرا الاسم من جسم
    /// JSON من غير ما يستهلك المجرى. والقاعدة بتخلّي العدّ يعيش بعد
    /// إعادة تشغيل السيرفر.</para>
    /// </summary>
    public const int MaxAttemptsPerWindow = 10;

    /// <summary>نافذة العدّ.</summary>
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// عرض عمود <c>AttemptedUsername</c> — <b>حدّ قاعدة بيانات مش
    /// قاعدة شغل</b>.
    ///
    /// <para>🔴 <b>والرقم ده موجود كـ<u>حارس انحراف</u> بس.</b>
    /// الاسم اللي بيتسجّل بيجي من <c>LoginName.Normalize</c>، وهي
    /// بتقصّه على <c>LoginName.MaxLength</c> أصلاً — فمفيش قص تاني
    /// في المعالجات. (حاولنا نحطّ واحد، وتحوير شالّه ونجا: كان كود
    /// ميت.)</para>
    ///
    /// <para>⚠️ <b>واللي بيحمي فعلاً هو الفحص اللي بيقارن
    /// الاتنين.</b> لو حد وسّع <c>LoginName.MaxLength</c> لـ١٠٠ من
    /// غير هجرة على العمود، الحفظ بيقع بخطأ من القاعدة على كل محاولة
    /// دخول — يعني الورشة كلها بتقف. الفحص بيلقطها قبل النشر.</para>
    /// </summary>
    public const int MaxAttemptedUsername = 60;

    /// <summary>العدّاد وصل الحد؟</summary>
    public static bool Throttled(int recentFailures) =>
        recentFailures >= MaxAttemptsPerWindow;

    /// <summary>أول وقت بيدخل في نافذة العدّ.</summary>
    public static DateTime WindowStart(DateTime nowUtc) => nowUtc - AttemptWindow;

    /// <summary>
    /// آخر وقت تنفع الراكة تدخّل بيه الفني وهي أوفلاين.
    ///
    /// <para>🔴 <b>والسالب بيتصفّر.</b> إعداد بالغلط بسالب كان بيدّي
    /// مهلة في الماضي — يعني كل الفنيين مابيعرفوش يدخلوا أوفلاين
    /// والسبب مش باين في أي لوج.</para>
    /// </summary>
    public static DateTime OfflineUntil(DateTime nowUtc, int validityDays) =>
        nowUtc.AddDays(Math.Max(0, validityDays));

    /// <summary>
    /// رسالة الإيقاف — <b>ومعاها السبب ومين أوقفه</b>.
    ///
    /// <para>🔴 <b>والسبب بيوصل لشاشة المحطة عن قصد.</b> من غيره
    /// الفني بيقف قدام رسالة مقفولة ومش عارف يكلّم مين، فبيروح يجرّب
    /// حساب زميله — وده بالظبط اللي الإيقاف بيمنعه.</para>
    /// </summary>
    public static string SuspendedMessage(string? reason, string? byName)
    {
        string why = string.IsNullOrWhiteSpace(reason)
            ? ""
            : "\nالسبب: " + reason;

        string who = string.IsNullOrWhiteSpace(byName)
            ? ""
            : $" (أوقفه: {byName})";

        return "غير مسموح بالدخول حالياً — كلّم المدير" + why + who;
    }

    /// <summary>رسالة المحاولات الكتير.</summary>
    public const string TooManyMessage =
        "محاولات كتير في وقت قصير. استنى شوية وجرّب تاني.";

    /// <summary>
    /// ⚠️ <b>رسالة واحدة للاسم المش موجود وللباسورد الغلط.</b>
    /// </summary>
    public const string InvalidMessage = "اسم المستخدم أو كلمة المرور غلط";

    /// <summary>أسباب المحاولة المتسجّلة — بتتعرض للمدير في السجل.</summary>
    public const string ReasonTooMany = "محاولات كتير";

    public const string ReasonInvalid = "بيانات دخول غلط";

    public const string ReasonSuspended = "الحساب موقوف";

    public const string ReasonChangeWrongCurrent = "تغيير باسورد: الحالي غلط";
}
