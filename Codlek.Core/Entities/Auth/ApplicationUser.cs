using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Core.Entities.Auth;

/// <summary>
/// مستخدم اللوحة — على جداول Identity.
///
/// <para>🔴 <b>المفتاح <c>Guid</c> مش <c>string</c>.</b> كل معرّفات
/// النظام دي <c>Guid</c>، ومستخدمين بمفتاح نصّي كان هيخلّي كل ربط
/// (<c>AuditEvent.ActorUserId</c>، <c>RepairWorkItem.ApprovedByUserId</c>،
/// وغيرهم) يحتاج تحويل في كل استعلام. والـIdentity بتقبل
/// <c>Guid</c> عادي.</para>
///
/// <para>⚠️ <b>والحقول اللي تحت مش زيادة — دي اللي النظام شغّال
/// بيها.</b> Identity بتدّي اسم وباسورد وبريد؛ والباقي (الشركة، الكود،
/// الدور، نسخة الاعتماد) حاجات موجودة في <c>WebUser</c> ولازم تعيش.</para>
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>الشركة — كل استعلام في النظام بيترشّح بيها.</summary>
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>الاسم اللي بيتعرض.</summary>
    [MaxLength(120)]
    public string DisplayName { get; set; } = "";

    /// <summary>كود المستخدم — بيظهر في السجلات.</summary>
    [MaxLength(20)]
    public string Code { get; set; } = "";

    /// <summary>
    /// الدور — <b>enum مش صف في جدول</b>.
    ///
    /// <para>🔴 <b>الرقم ده بيعدّي على السلك للراكة.</b> عشان كده
    /// بيفضل <c>enum</c> حتى مع وجود جداول الأدوار: جداول الأدوار
    /// للصلاحيات المرنة، والرقم ده عقد مجمّد مع الراكة.</para>
    ///
    /// <para>⚠️ وجدول <c>AspNetRoles</c> بيتزرع بأسماء الـenum، فاللي
    /// بيقرا الصلاحيات بالاسم يلاقيها — والرقم يفضل هو المرجع.</para>
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// نسخة بيانات الاعتماد — <b>أساس الإلغاء</b>.
    ///
    /// <para>بتزيد مع الإيقاف ومع إعادة تعيين الباسورد، فأي توكن
    /// اتعمل قبلهم بيترفض وقت التجديد.</para>
    /// </summary>
    public int CredentialVersion { get; set; }

    public bool MustChangePassword { get; set; }

    /// <summary>الحساب مفعّل؟ — الإيقاف بيمنع الدخول والتجديد.</summary>
    public bool IsActive { get; set; } = true;

    [MaxLength(400)]
    public string SuspendedReason { get; set; } = "";

    [MaxLength(120)]
    public string SuspendedByName { get; set; } = "";

    public DateTime? SuspendedAtUtc { get; set; }

    /// <summary>
    /// اتعمل إمتى — بيتعرض في صفحة «حسابي».
    ///
    /// <para>⚠️ موجود في <c>WebUser</c> وبيخرج في عقد
    /// <c>AccountResponse</c>، فلازم يعيش.</para>
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// آخر دخول — <c>null</c> يعني عمره ما دخل.
    ///
    /// <para>🔴 <b>وده مش للعرض بس.</b> حساب مادخلش من شهور
    /// ولسه مفعّل = موظف ساب الشغل ومحدش وقّف حسابه.</para>
    /// </summary>
    public DateTime? LastLoginUtc { get; set; }

    /// <summary>
    /// ملح الباسورد القديم — <b>عشان الباسوردات الموجودة تفضل شغّالة</b>.
    ///
    /// <para>🔴 النظام القديم بيخزّن البصمة والملح في عمودين منفصلين
    /// (PBKDF2 · ١٠٠٬٠٠٠ تكرار · SHA-256)، و<b>الراكة بتستعمل نفس
    /// الخوارزمية بالحرف</b>. وIdentity بتخزّن البصمة في عمود واحد
    /// بشكل مختلف.</para>
    ///
    /// <para>فلو تجاهلنا ده، <b>كل مستخدم في النظام مايعرفش يدخل بعد
    /// التحويل</b> — والحل الوحيد إعادة تعيين الباسوردات كلها بالإيد.
    /// العمود ده بيخلّي <c>LegacyPasswordHasher</c> يفهم الشكل القديم
    /// ويتحقق منه.</para>
    ///
    /// <para>⚠️ وفاضي معناه «الباسورد ده اتعمل بـIdentity» — يعني
    /// المستخدم غيّره بعد التحويل، والتحقق بيعدّي على الشكل الجديد.</para>
    /// </summary>
    [MaxLength(64)]
    public string LegacySalt { get; set; } = "";
}
