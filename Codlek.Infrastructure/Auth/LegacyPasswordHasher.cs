using Codlek.Core.Entities.Auth;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// بيفهم الباسوردات القديمة <b>والجديدة</b>.
///
/// <para>🔴 <b>من غير الكلاس ده، كل مستخدم في النظام مايعرفش يدخل
/// بعد التحويل.</b> الباسوردات المخزّنة دلوقتي PBKDF2 ببصمة وملح في
/// عمودين منفصلين. وIdentity بتخزّن بصمة واحدة بشكل تاني خالص، ولما
/// تلاقي الشكل القديم بترفضه.</para>
///
/// <para>والحل الغلط اللي بيتعمل عادةً: إعادة تعيين كل الباسوردات.
/// يعني كل فني ومدير في الورشة يقف لحد ما حد يدّيله باسورد جديد.</para>
///
/// <para>⚠️ <b>والترقية بتحصل لوحدها.</b> أول ما المستخدم يدخل
/// بباسورده القديم، <see cref="VerifyHashedPassword"/> بترجّع
/// <c>SuccessRehashNeeded</c> — وIdentity ساعتها بتعيد تخزينه بالشكل
/// الجديد. يعني النظام بيتنضّف من نفسه من غير ما حد يعمل حاجة.</para>
///
/// <para>⚠️ <b>والراكة مابتتأثرش.</b> هي بتتحقق من الفنيين
/// (<c>Technicians</c>) بنفس الخوارزمية القديمة، وجدولهم مالوش علاقة
/// بـIdentity. اللي بيتغيّر هنا هو مستخدمو اللوحة بس.</para>
/// </summary>
public sealed class LegacyPasswordHasher : IPasswordHasher<ApplicationUser>
{
    /// <summary>
    /// طول بصمة النظام القديم بالبايت — ٣٢ (SHA-256).
    ///
    /// <para>⚠️ بصمة Identity أطول من كده بكتير: جوّاها رقم إصدار
    /// وعدد تكرارات وملح وبصمة، كلهم في نص واحد.</para>
    /// </summary>
    private const int LegacyHashBytes = 32;

    private readonly PasswordHasher<ApplicationUser> _modern = new();

    /// <summary>
    /// بيعمل بصمة بالشكل الجديد — <b>وبيشيل علامة الشكل القديم</b>.
    ///
    /// <para>🔴 <b>السطر اللي بيفضّي <c>LegacySalt</c> هو إصلاح باج
    /// حقيقي، مش تنضيف.</b></para>
    ///
    /// <para>العمودين دول بيوصفوا حاجة واحدة: «الباسورد ده بأي شكل».
    /// و<c>UserManager</c> لما بيترقّي بيكتب البصمة الجديدة
    /// <b>وبس</b> — <c>LegacySalt</c> بيفضل مليان. فتاني مرة المستخدم
    /// يدخل، الكلاس ده بيشوف الملح ويروح للفرع القديم، ويجرّب PBKDF2
    /// على بصمة Identity — وبيفشل.</para>
    ///
    /// <para>يعني <b>المستخدم بيدخل مرة واحدة بعد التحويل وبعد كده
    /// مايدخلش خالص</b>. وده أسوأ من إننا ماعملناش ترقية من الأول.</para>
    ///
    /// <para>⚠️ <b>والتفضية هنا بالتحديد لأنها بتتحفظ في نفس الحفظة.</b>
    /// <c>UserManager</c> بينده الدالة دي، بعدها بيحطّ البصمة، وبعدها
    /// بينده <c>UpdateUserAsync</c> مرة واحدة. فالبصمة الجديدة
    /// والملح الفاضي بيتكتبوا مع بعض — <b>ماينفعش واحد ينزل من غير
    /// التاني</b>. ولو كانت في مكان تاني، كان فيه لحظة الصف فيها
    /// بصمة جديدة وملح قديم.</para>
    /// </summary>
    public string HashPassword(ApplicationUser user, string password)
    {
        string hash = _modern.HashPassword(user, password);

        user.LegacySalt = "";

        return hash;
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        bool markedLegacy = !string.IsNullOrEmpty(user.LegacySalt);

        /*
          🔴 **وجود الملح القديم هو اللي بيقول ده أي شكل.**

          مش بنحاول نقرا البصمة ونخمّن — عمود `LegacySalt` موجود
          للغرض ده بالظبط. فاضي = الشكل الجديد، ومليان = القديم.

          ⚠️ وترتيب الفحص مهم: لو جرّبنا الشكل الجديد الأول على بصمة
          قديمة، المكتبة بترمي أو بترجّع فشل — والنتيجة واحدة بس
          الرسالة بتبقى مضلّلة في السجل.
        */
        if (markedLegacy && LooksLegacy(hashedPassword))
        {
            bool ok = PasswordHasher.Verify(providedPassword, hashedPassword, user.LegacySalt);

            // ⚠️ **SuccessRehashNeeded مش Success.** دي اللي بتخلّي
            // Identity تعيد تخزين الباسورد بالشكل الجديد. لو رجّعنا
            // Success، كل مستخدم هيفضل على الشكل القديم للأبد.
            return ok
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        var result = VerifyModern(user, hashedPassword, providedPassword);

        /*
          🔴 **ملح سايب: البصمة جديدة والعلامة لسه مليانة.**

          الصفوف دي موجودة في القاعدة فعلاً — اتعملت قبل ما
          `HashPassword` يفضّي الملح. وهي بتشتغل عادي بفضل
          `LooksLegacy`، بس العلامة الغلط بتفضل فيها للأبد.

          ⚠️ و`SuccessRehashNeeded` هنا بتخلّي Identity تعيد التخزين،
          يعني بتنده `HashPassword`، يعني **الملح بيتفضّى**. فالصف
          بينضّف نفسه أول مرة صاحبه يدخل — من غير سكريبت ومن غير ما
          حد يعمل حاجة.

          والتكلفة: بصمة واحدة زيادة، مرة واحدة، لكل حساب.
        */
        if (markedLegacy && result is PasswordVerificationResult.Success)
            return PasswordVerificationResult.SuccessRehashNeeded;

        return result;
    }

    /// <summary>
    /// التحقق بالشكل الجديد — <b>وبصمة تالفة بترجع «غلط» مش عطل</b>.
    ///
    /// <para>🔴 <b>`PasswordHasher` بترمي <c>FormatException</c> لو
    /// البصمة المخزّنة مش base64 سليم.</b> ومعناها إن الطلب بياخد
    /// <c>500</c> بدل «كلمة المرور غلط» — فالمستخدم بيشوف «في مشكلة
    /// في السيرفر» ومايعرفش يعمل إيه.</para>
    ///
    /// <para>⚠️ <b>والحالة دي واردة وقت نقل الحسابات:</b> سكريبت
    /// بيحطّ عمود في مكان عمود تاني، والنتيجة إن <b>كل</b> الناس
    /// بتاخد <c>500</c> على الدخول. وأسوأ حاجة إن السجل بيقول
    /// «FormatException» — كلام مالوش علاقة بالسبب.</para>
    ///
    /// <para>⚠️ <b>وبنمسك <c>FormatException</c> وبس.</b> أي استثناء
    /// تاني بيكمّل طريقه: إخفاء كل الأعطال هنا بيخلّي عطل حقيقي في
    /// التشفير يبان «الباسورد غلط».</para>
    /// </summary>
    private PasswordVerificationResult VerifyModern(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        try
        {
            return _modern.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
        catch (FormatException)
        {
            // ⚠️ الصف ده محتاج حد يبصّ عليه — مش محتاج المستخدم
            // يحاول تاني. والمعرّف في الرسالة عشان يتلاقى.
            Console.Error.WriteLine(
                $"🔴 بصمة الباسورد تالفة للمستخدم {user.Id} — الصف محتاج إعادة تعيين.");

            return PasswordVerificationResult.Failed;
        }
    }

    /// <summary>
    /// البصمة دي شكلها قديم؟ — <b>شبكة أمان، مش طريقة التفرقة</b>.
    ///
    /// <para>اللي بيفرّق هو <c>LegacySalt</c>. والفحص ده موجود لحالة
    /// واحدة: صف فيه <b>بصمة جديدة وملح قديم</b> مع بعض.</para>
    ///
    /// <para>🔴 والحالة دي مش نظرية — دي كانت بتحصل فعلاً قبل ما
    /// <see cref="HashPassword"/> يفضّي الملح. والفرق إنها بدل ما
    /// تقفل الحساب للأبد، بتعدّي على المتحقّق الصح.</para>
    ///
    /// <para>⚠️ وممكن يبقى فيه صفوف كده في القاعدة من قبل الإصلاح،
    /// فالشبكة دي بتفضل لازمة.</para>
    /// </summary>
    private static bool LooksLegacy(string hashedPassword)
    {
        Span<byte> buffer = stackalloc byte[64];

        return Convert.TryFromBase64String(hashedPassword ?? "", buffer, out int written)
               && written == LegacyHashBytes;
    }
}
