using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// قسم في الشركة — الوحدة اللي الجهاز بيتحرّك بينها.
///
/// <para>⚠️ <b>القسم ≠ التخصص.</b> القسم وحدة تنظيمية (التوجيه بيروح
/// لقسم)، والتخصص مهارة الشخص (الأمر بيتوجّه لتخصص). قسم واحد ممكن يبقى
/// فيه أكتر من تخصص. خلطهم في حقل واحد بيخلي التوجيه والتوزيع نفس
/// الحاجة، وهما مختلفين.</para>
///
/// <para>مركزي وقابل للتعديل من لوحة الإدارة — مش enum في الكود، لأن
/// الأقسام بتتغيّر والشركة المفروض تضيف قسم من غير نشر نسخة جديدة.</para>
/// </summary>
/// <summary>
/// ماركة لابات — <b>قايمة صاحب الشغل بيديرها بإيده</b>.
///
/// <para>🔴 <b>ليه قايمة مُدارة مش استنتاج من البيانات.</b> اسم
/// الماركة في جدول الأجهزة خام زي ما ويندوز قاله، ونفس الشركة بتيجي
/// <c>HP</c> و<c>Hewlett-Packard</c>. استنتاج القايمة لوحدها كان
/// هيدمج أسماء مالهاش علاقة ببعض (مثلاً سيرفرات
/// <c>Hewlett Packard Enterprise</c> مع لابات HP) ويفرّق أسماء
/// لنفس الشركة — وقاعدة منع مبنية على تخمين أسوأ من مفيش قاعدة.</para>
///
/// <para>⚠️ <b>والماركة بتتوقف مابتتمسحش.</b> نفس قاعدة الحاوية
/// والموقع: أمر صيانة قديم بيشاور على ماركة، ومسحها بيحوّل السجل
/// لصفوف يتيمة.</para>
/// </summary>
public class LaptopBrand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    /// <summary>الاسم زي ما صاحب الشغل كتبه — ده اللي بيتعرض.</summary>
    [MaxLength(80)]
    public string Name { get; set; } = "";

    /// <summary>
    /// الاسم بعد التوحيد — <b>ده اللي بيتقارن بيه</b>.
    ///
    /// <para>⚠️ متخزّن مش محسوب وقت الاستعلام: المقارنة بتحصل في
    /// قاعدة البيانات، ودالة C# مالهاش ترجمة SQL.</para>
    /// </summary>
    [MaxLength(80)]
    public string NormalizedName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<LaptopBrandAlias> Aliases { get; set; } = new List<LaptopBrandAlias>();
}

/// <summary>
/// اسم تاني لنفس الماركة — <c>Hewlett-Packard</c> ← <c>HP</c>.
///
/// <para>🔴 <b>جدول مش عمود بفواصل، عشان التفرّد يتفرض في قاعدة
/// البيانات.</b> الاسم البديل لازم يكون فريد على مستوى <b>الشركة
/// كلها</b> مش جوّه الماركة الواحدة: لو ماركتين ادّعوا «HP»، الحل
/// بيبقى معتمد على الترتيب — ونفس اللاب بيتحل لماركة مختلفة بعد ما
/// حد يغيّر ترتيب العرض. وعمود بفواصل مينفعش يتعمل عليه فهرس.</para>
/// </summary>
public class LaptopBrandAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid BrandId { get; set; }
    public LaptopBrand? Brand { get; set; }

    /// <summary>زي ما صاحب الشغل كتبه.</summary>
    [MaxLength(80)]
    public string RawValue { get; set; } = "";

    /// <summary>بعد التوحيد — ده اللي بيتقارن بيه.</summary>
    [MaxLength(80)]
    public string NormalizedValue { get; set; } = "";
}

/// <summary>
/// الماركات اللي الفني ده بيصلّحها.
///
/// <para>🔴 <b>فاضي معناه «كل الماركات» مش «ولا ماركة».</b> كل فني
/// في الشركة ماركاته فاضية لحظة ما الميزة دي تنزل — والقراية
/// التانية، وهي الأقرب للذهن، كانت هتقفل على <b>كل</b> الفنيين في
/// نفس اللحظة.</para>
///
/// <para>⚠️ نفس شكل <c>RepairWorkItem.RequiredSpecialty == None</c>
/// اللي معناه «معروض على كل فني صيانة».</para>
///
/// <para>⚠️ <b>ومفيش علامة «ممنوع من كل حاجة»</b>: «الفني ده
/// مايشتغلش على أي لاب لحد ما أقول» مش قابلة للتعبير هنا — دي
/// <c>IsActive = false</c> على الفني نفسه.</para>
/// </summary>
public class TechnicianBrand
{
    public Guid TenantId { get; set; }

    public Guid TechnicianId { get; set; }
    public Technician? Technician { get; set; }

    public Guid BrandId { get; set; }
    public LaptopBrand? Brand { get; set; }
}

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(20)]
    public string Code { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

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

/// <summary>
/// محاولة دخول فني من محطة فحص — للتدقيق ولتقييد التخمين.
///
/// <para>⚠️ الباسورد نفسه عمره ما بيتكتب هنا ولا في أي سجل. الصف ده
/// بيقول «مين حاول، من أنهي محطة، ونجح ولا لأ» وبس.</para>
///
/// <para>وبنسجّل الاسم اللي اتكتب حتى لو مش موجود: محاولات كتير على
/// أسماء مش موجودة من نفس المحطة دي إشارة تخمين، والإشارة دي
/// بتضيع لو سجّلنا الصفوف اللي لقينا لها حساب بس.</para>
/// </summary>
public class TechnicianLoginAttempt
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>المحطة اللي المحاولة جت منها — دايماً متحققة بمفتاحها.</summary>
    public Guid RackId { get; set; }

    /// <summary>ممكن يبقى فاضي لو الاسم مش موجود أصلاً.</summary>
    public Guid? TechnicianId { get; set; }

    [MaxLength(60)]
    public string AttemptedUsername { get; set; } = "";

    public bool Success { get; set; }

    /// <summary>سبب الرفض بالعربي — من غير أي تلميح عن الباسورد.</summary>
    [MaxLength(200)]
    public string Reason { get; set; } = "";

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
