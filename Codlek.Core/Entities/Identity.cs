using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// الشركة. النظام متعدد الشركات من أول يوم عشان لما تيجي شركة تانية
/// منغيّرش قاعدة البيانات — كل استعلام بيترشّح بـ TenantId.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<WebUser> Users { get; set; } = new();
    public List<Report> Reports { get; set; } = new();
}

/// <summary>
/// حساب على الموقع. البصمة والملح بنفس صيغة البرنامج المكتبي
/// (PBKDF2-SHA256، ١٠٠ ألف دورة، Base64) فنفس الباسورد بيشتغل في
/// الاتنين، والحسابات بتتنقل من ملف accounts.json من غير ما حد
/// يعيد ضبط باسوردات.
/// </summary>
public class WebUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [MaxLength(60)]
    public string Username { get; set; } = "";

    /// <summary>
    /// مفتاح الدخول — <b>فريد على مستوى النظام كله</b>، مش جوّه الشركة.
    ///
    /// <para><b>ليه عالمي والفني تنظيمه جوّه الشركة.</b> صفحة الدخول
    /// بتستلم اسم وباسورد وبس — مفيش كود شركة ولا نطاق فرعي ولا قايمة
    /// اختيار. يعني لازم الاسم لوحده يوصّل لصف واحد <b>قبل</b> ما
    /// <c>TenantId</c> يتحط في الكوكي. لو اتنين في شركتين مختلفتين
    /// شايلين نفس الاسم، السيرفر بيبقى بيخمّن هوية الشركة — والتخمين
    /// ده هو اللي بيحدد البيانات اللي هتتفتح. الفني بالعكس: بيدخل من
    /// محطة <b>متحققة بمفتاحها</b>، والشركة معروفة من المحطة قبل ما
    /// الاسم يتقرا أصلاً، فالاسم محتاج يبقى فريد جوّه الشركة وبس.</para>
    ///
    /// <para>⚠️ العمود ده بيتحسب لوحده في
    /// <see cref="AppDbContext.SaveChanges()"/> من <see cref="Username"/> —
    /// متكتبوش بالإيد. ده اللي بيمنع مكان واحد ينسى يملاه ويعدّي الفهرس
    /// بقيمة فاضية.</para>
    /// </summary>
    [MaxLength(60)]
    public string NormalizedUsername { get; set; } = "";

    [MaxLength(120)]
    public string DisplayName { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.Technician;

    /// <summary>كود الفني — ٦ أرقام. ده اللي بيربط الحساب بالفحوصات.</summary>
    [MaxLength(20)]
    public string Code { get; set; } = "";

    [MaxLength(200)]
    public string PasswordHash { get; set; } = "";

    [MaxLength(64)]
    public string Salt { get; set; } = "";

    public bool IsActive { get; set; } = true;

    [MaxLength(400)]
    public string SuspendedReason { get; set; } = "";

    [MaxLength(120)]
    public string SuspendedByName { get; set; } = "";

    public DateTime? SuspendedAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    /// <summary>
    /// بيزيد مع كل تغيير باسورد أو إيقاف — <b>وده اللي بيقتل الجلسة
    /// المفتوحة</b>.
    ///
    /// <para>🔴 <b>من غيره الإيقاف بيبان إنه اشتغل وهو مش شغّال.</b>
    /// كوكي الدخول بتتبني من <c>LoginSession.BuildPrincipal</c>
    /// مرة واحدة وقت الدخول، وبعد كده كل طلب بيتقرا من الادعاءات اللي
    /// جوّاها من غير ما حد يبص على الصف. يعني المالك يوقف مدير المخزن،
    /// وشاشة الدخول ترفضه فعلاً — والتاب المفتوح عنده يفضل يعمل
    /// حسابات ويصدّر المخزن بكل صلاحياته. والكوكي
    /// <c>SlidingExpiration</c> فبتتجدد مع كل طلب، يعني مالهاش نهاية
    /// طول ما هو بيستخدمها.</para>
    ///
    /// <para>🔴 <b>والزيادة لوحدها مش كفاية.</b> اللي بيقرا الرقم ده
    /// ويقارنه بالادعاء هو <c>CookieSessionGuard</c> — وهو
    /// المكان الوحيد اللي بيبص على القاعدة في مسار الطلب.</para>
    ///
    /// <para>⚠️ بيزيد، مابينقصش — زي
    /// <see cref="Technician.CredentialVersion"/> بالظبط. الفرق إن
    /// بتاع الفني بيوصل لراكة أوفلاين، وده بيقطع جلسة على النت.</para>
    /// </summary>
    public int CredentialVersion { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }

    /// <summary>
    /// تخصص الفني — منفصل عن الصلاحية.
    /// الصلاحية بتقول «بيشوف إيه»، والتخصص بيقول «بيستلم أنهي شغل».
    /// </summary>
    public TechnicianSpecialty Specialty { get; set; } = TechnicianSpecialty.None;

    /// <summary>القسم اللي الموظف تابع له. اختياري.</summary>
    public Guid? DepartmentId { get; set; }

    [NotMapped]
    public string RoleText => Role switch
    {
        UserRole.Owner => "مدير عام",
        UserRole.Manager => "مدير المخزن",
        UserRole.FloorManager => "مدير الدور",
        UserRole.Accountant => "محاسب",
        _ => "فني"
    };
}

/// <summary>
/// سجل الإجراءات المهمة.
///
/// <para><b>حدود واضحة عشان منعملش أربع جداول مراجعة:</b>
/// <c>LoginEvent</c> بيفضل للدخول وبس، و<c>ReportEdit</c> بيفضل لتعديلات
/// الفحوصات وبيسافر جوّه <c>RawJson</c>. الجدول ده للإجراءات الإدارية
/// وإجراءات النظام — اقتران راكة، إلغاء مفتاح، شك في استنساخ، تعارض هوية
/// جهاز. ومفيش جدول خامس.</para>
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>معرّف بيتولّد على الراكة لما الحدث أصله من هناك — بيمنع التكرار.</summary>
    public Guid? EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>User · Rack · System</summary>
    [MaxLength(20)]
    public string ActorType { get; set; } = "System";

    public Guid? ActorUserId { get; set; }
    public Guid? ActorRackId { get; set; }

    [MaxLength(120)]
    public string ActorName { get; set; } = "";

    /// <summary>rack.paired · rack.revoked · rack.clone_suspected …</summary>
    [MaxLength(60)]
    public string Action { get; set; } = "";

    [MaxLength(40)]
    public string EntityType { get; set; } = "";

    public Guid? EntityId { get; set; }

    [MaxLength(30)]
    public string EntityCode { get; set; } = "";

    [MaxLength(400)]
    public string Summary { get; set; } = "";

    public string DataJson { get; set; } = "";

    [MaxLength(60)]
    public string Ip { get; set; } = "";
}

/// <summary>
/// سجل دخول. بيخلّي المدير يشوف مين فتح الموقع وإمتى ومن فين —
/// نفس منطق «الراكة بتتشارك»، بس هنا للموقع.
/// </summary>
public class LoginEvent
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }

    [MaxLength(60)] public string Username { get; set; } = "";
    [MaxLength(120)] public string DisplayName { get; set; } = "";
    public bool Success { get; set; }
    [MaxLength(200)] public string Reason { get; set; } = "";
    [MaxLength(60)] public string Ip { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
