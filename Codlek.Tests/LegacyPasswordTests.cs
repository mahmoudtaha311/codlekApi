using Codlek.Core.Entities.Auth;
using Codlek.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Tests;

/// <summary>
/// الباسوردات الموجودة لازم تفضل شغّالة بعد ما ندخل Identity.
///
/// <para>🔴 <b>ده أخطر فحص في الملف ده.</b> لو وقع، معناه إن كل
/// مستخدم في النظام — مديرين وفنيين — مش هيعرف يدخل بعد التحويل،
/// والحل الوحيد ساعتها إن حد يعيد تعيين الباسوردات كلها بالإيد.</para>
///
/// <para>⚠️ والفحوص دي مابتلمسش القاعدة: <c>LegacyPasswordHasher</c>
/// دالة نقية بتاخد البصمة والملح وترجّع نتيجة. يعني بيشتغلوا في
/// مللي ثانية وبيدوروا كل مرة.</para>
/// </summary>
public class LegacyPasswordTests
{
    private readonly LegacyPasswordHasher _hasher = new();

    /// <summary>
    /// بيعمل مستخدم بباسورد متخزّن <b>بالشكل القديم</b> — بصمة وملح
    /// في عمودين، زي اللي في القاعدة دلوقتي بالظبط.
    /// </summary>
    private static (ApplicationUser User, string Hash) LegacyUser(string password)
    {
        var (hash, salt) = PasswordHasher.Create(password);
        return (new ApplicationUser { LegacySalt = salt }, hash);
    }

    // =================================================================
    //  الشكل القديم
    // =================================================================

    /// <summary>
    /// 🔴 الباسورد القديم بيعدّي.
    ///
    /// <para>ولازم يرجّع <c>SuccessRehashNeeded</c> مش <c>Success</c>:
    /// دي اللي بتخلّي Identity تعيد تخزينه بالشكل الجديد. لو رجّعنا
    /// <c>Success</c>، النظام هيفضل على الشكل القديم للأبد — يشتغل،
    /// بس الترقية اللي كانت مجانية مابتحصلش.</para>
    /// </summary>
    [Fact]
    public void A_password_stored_by_the_old_system_still_works_and_gets_upgraded()
    {
        var (user, hash) = LegacyUser("MyS3cret!");

        var result = _hasher.VerifyHashedPassword(user, hash, "MyS3cret!");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void A_wrong_password_against_an_old_hash_is_rejected()
    {
        var (user, hash) = LegacyUser("MyS3cret!");

        var result = _hasher.VerifyHashedPassword(user, hash, "MyS3cret?");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    /// <summary>
    /// ⚠️ الباسورد القديم بيفرّق بين الكبير والصغير — فحص إن التطبيع
    /// مابيتسرّبش من البحث للباسوردات.
    /// </summary>
    [Fact]
    public void An_old_password_is_case_sensitive()
    {
        var (user, hash) = LegacyUser("MyS3cret!");

        Assert.Equal(
            PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(user, hash, "mys3cret!"));
    }

    // =================================================================
    //  الشكل الجديد
    // =================================================================

    /// <summary>
    /// 🔴 الباسورد اللي الكلاس ده بيعمله بيتقرا بالكلاس ده.
    ///
    /// <para>الحلقة دي هي اللي بتمنع أسوأ حالة ممكنة: المستخدم يغيّر
    /// باسورده بنجاح، وبعدين مايعرفش يدخل بيه.</para>
    /// </summary>
    [Fact]
    public void A_password_this_class_hashes_can_be_read_back_by_this_class()
    {
        var user = new ApplicationUser();

        string hash = _hasher.HashPassword(user, "NewPass123");

        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(user, hash, "NewPass123"));
    }

    [Fact]
    public void A_wrong_password_against_a_new_hash_is_rejected()
    {
        var user = new ApplicationUser();

        string hash = _hasher.HashPassword(user, "NewPass123");

        Assert.Equal(
            PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(user, hash, "NewPass124"));
    }

    /// <summary>
    /// ⚠️ البصمة الجديدة مابتبقاش فيها الباسورد، ولا بتتكرر لنفس
    /// الباسورد — الملح جوّاها عشوائي.
    /// </summary>
    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        var user = new ApplicationUser();

        Assert.NotEqual(
            _hasher.HashPassword(user, "NewPass123"),
            _hasher.HashPassword(user, "NewPass123"));
    }

    // =================================================================
    //  اللي بيحدد الشكل
    // =================================================================

    /// <summary>
    /// 🔴 <b><c>LegacySalt</c> هو اللي بيقول ده أي شكل — مش شكل البصمة.</b>
    ///
    /// <para>بصمة قديمة مع ملح فاضي لازم تترفض. لأن لو الكلاس حاول
    /// يخمّن الشكل من البصمة نفسها، كان ممكن يقرا بصمة قديمة كأنها
    /// جديدة ويرجّع نتيجة مالهاش معنى.</para>
    /// </summary>
    [Fact]
    public void An_old_hash_without_its_salt_is_rejected_not_guessed()
    {
        var (_, hash) = LegacyUser("MyS3cret!");
        var userWithNoSalt = new ApplicationUser { LegacySalt = "" };

        var result = _hasher.VerifyHashedPassword(userWithNoSalt, hash, "MyS3cret!");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    // =================================================================
    //  الترقية — وده الجزء اللي وقع فعلاً
    // =================================================================

    /// <summary>
    /// 🔴 <b>أهم فحص في الملف ده — وكان ناقص.</b>
    ///
    /// <para>الترقية بتكتب البصمة الجديدة، ولازم <b>تشيل علامة
    /// الشكل القديم</b> معاها. لأن العمودين بيوصفوا حاجة
    /// واحدة: «الباسورد ده بأي شكل».</para>
    ///
    /// <para>ولو الملح فضل مليان، <b>المستخدم بيدخل مرة واحدة
    /// بعد التحويل وبعد كده مايدخلش خالص</b> — وده أسوأ من
    /// إننا ماعملناش ترقية من الأول.</para>
    ///
    /// <para>⚠️ الباج ده عدّى من حوالين عشرة فحوص هنا، ومابانش
    /// غير لما شغّلنا الـAPI وسجّلنا دخول <b>مرتين</b>.</para>
    /// </summary>
    [Fact]
    public void Hashing_a_password_clears_the_legacy_marker()
    {
        var (user, _) = LegacyUser("MyS3cret!");
        Assert.NotEqual("", user.LegacySalt);

        _hasher.HashPassword(user, "MyS3cret!");

        Assert.Equal("", user.LegacySalt);
    }

    /// <summary>
    /// 🔴 <b>الدورة الكاملة: دخول بالقديم ← ترقية ← دخول تاني.</b>
    ///
    /// <para>ده اللي المستخدم بيعمله فعلاً — وكل الفحوص اللي فوق
    /// بتجرّب خطوة واحدة بس.</para>
    /// </summary>
    [Fact]
    public void A_user_can_log_in_again_after_the_automatic_upgrade()
    {
        var (user, legacyHash) = LegacyUser("MyS3cret!");

        // ١ · أول دخول — بالشكل القديم
        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            _hasher.VerifyHashedPassword(user, legacyHash, "MyS3cret!"));

        // ٢ · Identity بتعيد التخزين (نفس الترتيب اللي UserManager بتعمله)
        string upgraded = _hasher.HashPassword(user, "MyS3cret!");

        // ٣ · دخول تاني — لازم يعدّي
        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(user, upgraded, "MyS3cret!"));
    }

    /// <summary>
    /// ⚠️ وبصمة جديدة مع ملح قديم سايب — <b>بتعدّي</b>.
    ///
    /// <para>🔴 <b>كنت كاتب الفحص ده غلط.</b> كان بيتأكّد إن الحالة
    /// دي بتترفض، ووصفتها «حالة مش مفروض تحصل». وهي كانت بتحصل
    /// <b>مع كل ترقية</b> — يعني الفحص كان بيثبّت الباج.</para>
    /// </summary>
    [Fact]
    public void A_new_hash_with_a_stale_legacy_salt_still_verifies()
    {
        var user = new ApplicationUser();
        string newHash = _hasher.HashPassword(user, "NewPass123");

        user.LegacySalt = "c3RhbGVzYWx0MTIzNDU2";   // ملح سايب من قبل الترقية

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(user, newHash, "NewPass123"));
    }

    /// <summary>
    /// 🔴 <b>والصف المتسيّب بينضّف نفسه.</b>
    ///
    /// <para>الملح السايب لازم يطلّع <c>SuccessRehashNeeded</c> مش
    /// <c>Success</c> — لأن دي اللي بتخلّي Identity تنده
    /// <see cref="LegacyPasswordHasher.HashPassword"/>، واللي بيفضّي
    /// الملح. من غير كده، العلامة الغلط بتفضل في الصف للأبد.</para>
    /// </summary>
    [Fact]
    public void A_stale_legacy_salt_triggers_a_rehash_that_clears_it()
    {
        var user = new ApplicationUser();
        string newHash = _hasher.HashPassword(user, "NewPass123");
        user.LegacySalt = "c3RhbGVzYWx0MTIzNDU2";

        var result = _hasher.VerifyHashedPassword(user, newHash, "NewPass123");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);

        // واللي Identity بتعمله بعد النتيجة دي
        _hasher.HashPassword(user, "NewPass123");
        Assert.Equal("", user.LegacySalt);
    }

    /// <summary>⚠️ وباسورد نضيف من الأول بيرجّع <c>Success</c> مش ترقية.</summary>
    [Fact]
    public void A_clean_new_password_does_not_ask_for_a_rehash()
    {
        var user = new ApplicationUser();
        string hash = _hasher.HashPassword(user, "NewPass123");

        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(user, hash, "NewPass123"));
    }

    /// <summary>
    /// 🔴 <b>بصمة الراكة بالظبط.</b>
    ///
    /// <para>الملح والبصمة دول مكتوبين بالإيد، اتولدوا بنفس الخوارزمية
    /// (PBKDF2 · ١٠٠٬٠٠٠ · SHA-256 · ٣٢ بايت). لو حد غيّر أي رقم في
    /// <c>PasswordHasher</c>، الفحص ده بيقع — والفحوص اللي فوق مش
    /// هتقع، لأنها بتعمل البصمة وتقراها بنفس الكود المتغيّر.</para>
    ///
    /// <para>⚠️ يعني ده الفحص الوحيد هنا اللي بيثبت إن الأرقام
    /// نفسها ما اتحركتش — وهي <b>مشتركة مع الراكة</b>.</para>
    /// </summary>
    [Fact]
    public void A_hash_generated_outside_this_code_still_verifies()
    {
        const string password = "CodlekRack!2026";
        const string salt = "cXJzdHV2d3hRUlNUVVZXWA==";                  // ١٦ بايت
        const string hash = "tibUit5Rcp6WTNzw0WbCGbq+rhnenBrIoUIDq24zeJI=";

        var user = new ApplicationUser { LegacySalt = salt };

        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            _hasher.VerifyHashedPassword(user, hash, password));
    }

    // =================================================================
    //  بيانات تالفة في القاعدة
    // =================================================================

    /// <summary>
    /// 🔴 <b>بصمة تالفة بترجّع «غلط» — مش بترمي.</b>
    ///
    /// <para>اتكشفت على السيرفر الشغّال: صف فيه بصمة مش base64 سليم
    /// خلّى الدخول يرجّع <c>500</c>، والسجل قال «FormatException» —
    /// كلام مالوش علاقة بالسبب.</para>
    ///
    /// <para>⚠️ والحالة دي واردة وقت نقل الحسابات: سكريبت بيحطّ عمود
    /// في مكان عمود تاني، والنتيجة إن <b>كل</b> الناس بتاخد
    /// <c>500</c> على الدخول.</para>
    /// </summary>
    [Theory]
    [InlineData("ده مش base64 خالص")]
    [InlineData("!!!!")]
    [InlineData("AQAAAA")]
    [InlineData("====")]
    public void A_corrupt_stored_hash_fails_instead_of_throwing(string corrupt)
    {
        var user = new ApplicationUser();   // مفيش ملح قديم → المسار الجديد

        var result = _hasher.VerifyHashedPassword(user, corrupt, "AnyPassword1");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    /// <summary>
    /// ⚠️ وبصمة تالفة <b>مع</b> ملح قديم بتترفض كمان — المسار القديم
    /// بيمسك الاستثناء جوّاه أصلاً.
    /// </summary>
    [Fact]
    public void A_corrupt_hash_with_a_legacy_salt_also_fails_cleanly()
    {
        var user = new ApplicationUser { LegacySalt = "cXJzdHV2d3hRUlNUVVZXWA==" };

        Assert.Equal(
            PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(user, "مش بصمة", "AnyPassword1"));
    }

    /// <summary>⚠️ وبصمة فاضية برضه — صف ناقص مش عطل.</summary>
    [Fact]
    public void An_empty_stored_hash_fails_cleanly()
    {
        Assert.Equal(
            PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(new ApplicationUser(), "", "AnyPassword1"));
    }
}
