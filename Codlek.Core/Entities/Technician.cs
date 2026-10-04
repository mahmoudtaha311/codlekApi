using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// فني — <b>إنسان</b> بيشتغل على محطات الفحص.
///
/// <para><b>ليه كيان مستقل مش دور على <see cref="WebUser"/>.</b>
/// الفني والمدير مجالين مختلفين في كل حاجة: المدير بيدخل الموقع
/// بكوكي، والفني بيدخل <b>محطة فحص</b> بمفتاح الآلة. لو خلّينا
/// الفني صف في <c>Users</c>، أي سياسة صلاحيات على الموقع تبقى
/// محتاجة تفتكر تستثنيه، ونسيان استثناء واحد معناه فني بيفتح لوحة
/// الإدارة. الفصل هنا بيخلّي المنع هو الأصل: مفيش كوكي بيتولد
/// لفني أبداً، لأن مفيش مسار بيعمل كده.</para>
///
/// <para><b>الفني مش محطة.</b> فني واحد بيشتغل على محطات كتير،
/// ومحطة واحدة بيتناوب عليها فنيين في ورديات. عشان كده مفيش أي
/// رابط بين الجدول ده وجدول <c>Racks</c>.</para>
///
/// <para>⚠️ البصمة بنفس صيغة الموقع والبرنامج المكتبي بالظبط
/// (PBKDF2-SHA256، ١٠٠ ألف دورة، ملح ١٦ بايت، ناتج ٣٢ بايت،
/// Base64) — مش صدفة: ده اللي بيخلّي الراكة تتحقق من نفس الباسورد
/// أوفلاين من غير ما أي بصمة سيرفر تنزل عليها.</para>
/// </summary>
public class Technician
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>
    /// كود الفني — ٦ أرقام، هو اللي بيتكتب على كل فحص.
    ///
    /// <para>⚠️ ثابت بعد الإنشاء. الفحوصات القديمة بتشاور عليه، فتغييره
    /// بيفصل الفني عن تاريخه.</para>
    /// </summary>
    [MaxLength(20)]
    public string Code { get; set; } = "";

    [MaxLength(120)]
    public string DisplayName { get; set; } = "";

    [MaxLength(60)]
    public string Username { get; set; } = "";

    /// <summary>
    /// الاسم بحروف صغيرة — ده اللي الفهرس الفريد والبحث بيشتغلوا عليه.
    ///
    /// <para>⚠️ المقارنة على عمود متطبّع مش على <c>ToLower()</c> في
    /// الاستعلام: الأخيرة بتلغي استخدام الفهرس، وبتخلّي تفرقة حالة
    /// الحروف تعتمد على ترتيب القاعدة (اللي هنا <c>Arabic_CI_AI</c>)
    /// بدل ما تبقى قاعدة معلنة في الكود.</para>
    /// </summary>
    [MaxLength(60)]
    public string NormalizedUsername { get; set; } = "";

    [MaxLength(200)]
    public string PasswordHash { get; set; } = "";

    [MaxLength(64)]
    public string Salt { get; set; } = "";

    public bool IsActive { get; set; } = true;

    // ===========================================================
    //  قدرات الفني
    // ===========================================================
    //
    // 🔴 **حساب واحد للإنسان الواحد.** الفني اللي بيفحص ويصلّح مش
    // حسابين — لو عملناله اتنين، إنتاجيته بتتقسم على اسمين والتحويل
    // من الفحص للصيانة بيبقى تحويل لشخص تاني وهو هو.
    //
    // ⚠️ الافتراضي CanTest=true عشان الخمس فنيين الموجودين في الإنتاج
    // يفضلوا يشتغلوا من غير أي تدخّل. CanRepair=false عشان الصلاحية
    // دي تتدّي بقرار مش بالوراثة.

    /// <summary>الفني ده بيفحص؟</summary>
    public bool CanTest { get; set; } = true;

    /// <summary>
    /// الفني ده بيصلّح؟
    ///
    /// <para>قايمة «اختيار فني صيانة» بتترشّح بده — وفني بيفحص
    /// ويصلّح بيظهر في القايمة لنفسه، وده مقصود.</para>
    /// </summary>
    public bool CanRepair { get; set; }

    /// <summary>
    /// آخر مرة اتغيّرت فيها <see cref="CanTest"/> أو <see cref="CanRepair"/>.
    ///
    /// <para>🔴 <b>ده اللي بيخلّي الشغل الأوفلاين يفضل صالح.</b> الراكة
    /// بتشتغل من غير نت، فممكن فني يعمل صيانة وهو مصرّح له، وبعدين
    /// المدير يسحب الصلاحية قبل ما الشغل يترفع. من غير الوقت ده،
    /// السيرفر بيشوف <c>CanRepair = false</c> وبس، فيرفض شغل حصل
    /// فعلاً وكان مصرّح به ساعته.</para>
    ///
    /// <para>⚠️ والقاعدة صريحة: سحب الصلاحية بيمنع الشغل <b>الجاي</b>،
    /// مابيلغيش اللي خلص. حركة تاريخها أقدم من الوقت ده بتتقبل؛ وأي
    /// حاجة بعده بتترفض. <c>null</c> معناها الصلاحية ماتغيّرتش من يوم
    /// ما الحساب اتعمل.</para>
    /// </summary>
    public DateTime? CapabilityChangedAtUtc { get; set; }

    [MaxLength(400)]
    public string SuspendedReason { get; set; } = "";

    [MaxLength(120)]
    public string SuspendedByName { get; set; } = "";

    public DateTime? SuspendedAtUtc { get; set; }

    /// <summary>الباسورد الأولي اللي المدير حطّه — الفني لازم يغيّره.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// بيزيد مع كل تغيير باسورد أو إيقاف أو إلغاء صلاحية.
    ///
    /// <para><b>ده هو مفتاح الإبطال.</b> الراكة بتخزّن الرقم ده مع
    /// النسخة المحفوظة محلياً؛ أول ما تتصل وتلاقي رقم أحدث، بتعرف إن
    /// النسخة اللي عندها بطلت وبتمسحها. من غيره، تغيير باسورد على
    /// الموقع مكانش هيوصل لراكة أوفلاين أبداً.</para>
    ///
    /// <para>⚠️ بيزيد، مابينقصش. أي إعادة استخدام لرقم قديم معناها
    /// نسخة ملغية بترجع صالحة.</para>
    /// </summary>
    public int CredentialVersion { get; set; } = 1;

    public TechnicianSpecialty Specialty { get; set; } = TechnicianSpecialty.None;

    public Guid? DepartmentId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>آخر دخول ناجح اتحقق منه السيرفر — مش الدخول الأوفلاين.</summary>
    public DateTime? LastSuccessfulLoginUtc { get; set; }
}
