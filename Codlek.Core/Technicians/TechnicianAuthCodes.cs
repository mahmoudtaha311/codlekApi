namespace Codlek.Core.Technicians;

/// <summary>
/// أكواد نتيجة دخول الفني — <b>والراكة بتتفرّع عليها</b>.
///
/// <para>🔴 <b>الكود نص مجمّد مش enum.</b> الراكة بتقرا
/// <c>code</c> من جسم الرد كنص وبتقارنه حرف بحرف، فتغيير حرف واحد
/// معناه فرع ميت في برنامج منزّل على محطات في الميدان — ومفيش طريق
/// يحدّثها غير إن حد يروح لكل واحدة.</para>
/// </summary>
public static class TechnicianAuthCodes
{
    public const string Ok = "Ok";

    /// <summary>
    /// ⚠️ <b>نفس الكود للاسم المش موجود وللباسورد الغلط.</b>
    /// التفرقة بينهم بتقول للي بيجرّب أسماء مين موجود.
    /// </summary>
    public const string InvalidCredentials = "InvalidCredentials";

    public const string Suspended = "Suspended";

    public const string TooManyAttempts = "TooManyAttempts";

    /// <summary>الباسورد الجديد قصير — خاص بمسار التغيير.</summary>
    public const string WeakPassword = "WeakPassword";

    public const string SameAsCurrent = "SameAsCurrent";

    public const string Mismatch = "Mismatch";

    /// <summary>
    /// كود حالة HTTP لرد الدخول.
    ///
    /// <para>🔴 <b>والقفل <c>429</c> — مش <c>401</c> ولا
    /// <c>403</c>.</b> الراكة بتحسب <c>401</c> و<c>403</c> «رفض
    /// قاطع» وبتوقف عندهم خالص؛ أما <c>429</c> بتحسبها «السيرفر
    /// تعبان» وبتسمح بالدخول من النسخة المحفوظة. يعني لو القفل رجع
    /// <c>401</c>، فني كتب باسورده غلط عشر مرات في ورشة نتها قاطع
    /// <b>مابيقدرش يدخل خالص</b> — ولا حتى بنسخته الصالحة.</para>
    /// </summary>
    public static int LoginStatus(string code) => code switch
    {
        Suspended => 403,
        TooManyAttempts => 429,

        // ⚠️ وكل اللي غيرهم ٤٠١ — بما فيه الكود المش معروف.
        _ => 401,
    };

    /// <summary>
    /// كود حالة HTTP لرد تغيير الباسورد.
    ///
    /// <para>🔴 <b>والافتراضي هنا <c>400</c> مش <c>401</c> — وده
    /// الفرق المهم عن الدخول.</b> «الباسورد الجديد قصير» سياسة مش
    /// رفض اعتماد؛ لو رجعت <c>401</c>، شاشة الراكة بتعرض «الباسورد
    /// الحالي غلط» على باسورد حالي صح — وده بالظبط العطل اللي النقطة
    /// دي اتعملت عشانه.</para>
    /// </summary>
    public static int ChangeStatus(string code) => code switch
    {
        Suspended => 403,
        TooManyAttempts => 429,
        InvalidCredentials => 401,
        _ => 400,
    };
}
