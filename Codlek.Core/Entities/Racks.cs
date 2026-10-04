using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// راكة اختبار — الهارد اللي بيقلّع اللابات وبيرفع الفحوصات.
///
/// <para><b>الاسم اتغيّر من SyncDevice لـ Rack.</b> «Device» بقى معناه
/// اللاب نفسه (<c>LP-00018425</c>)، فوجود كلاس اسمه SyncDevice على جدول
/// اسمه Devices وهو يقصد الراكة كان هيولّد عطل حقيقي أول ما الاتنين
/// يبقوا موجودين مع بعض.</para>
///
/// <para><b>الهوية مربوطة بالقرص مش بالجهاز المضيف.</b> الراكة هارد
/// بيتنقل بين لابات كل شوية — ده شغلها الطبيعي. أي ربط بـ
/// <c>MachineIdentifier</c> كان هيلغي التسجيل في كل نقلة.
/// <see cref="InstallationId"/> بيتولّد مرة على القرص نفسه وبيعيش معاه؛
/// و<see cref="LastMachineIdentifier"/> بيانات متابعة وبس، مش بوابة دخول.</para>
/// </summary>
public class Rack
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>كود يقراه الموظف — RACK-001.</summary>
    [MaxLength(20)]
    public string RackCode { get; set; } = "";

    [MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(120)]
    public string Location { get; set; } = "";

    [MaxLength(200)]
    public string ApiKeyHash { get; set; } = "";

    [MaxLength(64)]
    public string Salt { get; set; } = "";

    /// <summary>أول ١٠ حروف من المفتاح — بتضيّق البحث من غير ما تكشفه.</summary>
    [MaxLength(12)]
    public string KeyPrefix { get; set; } = "";

    public RackStatus Status { get; set; } = RackStatus.PendingPairing;

    /// <summary>
    /// ⚠️ محسوبة مش عمود. كان فيه IsActive جنب Status، ودول نسختين من نفس
    /// الحقيقة وبيروحوا يختلفوا.
    /// </summary>
    [NotMapped]
    public bool IsActive => Status == RackStatus.Active;

    /// <summary>معرّف بيتولّد على قرص الراكة وبيعيش معاه — ده هو الثابت.</summary>
    [MaxLength(64)]
    public string InstallationId { get; set; } = "";

    /// <summary>آخر لاب الراكة اشتغلت عليه. متابعة بس — مش بيتحقق منها.</summary>
    [MaxLength(200)]
    public string LastMachineIdentifier { get; set; } = "";

    [MaxLength(40)]
    public string AppVersion { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RegisteredAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? KeyIssuedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    [MaxLength(400)]
    public string RevokedReason { get; set; } = "";

    public int ReportsReceived { get; set; }

    /// <summary>للتحديث المشروط — الاقتران وتدوير المفتاح لازم يبقوا ذرّيين.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// كود اقتران مؤقت — بيحوّل راكة جديدة لراكة مسجّلة.
///
/// <para>قبل كده المدير كان بينسخ المفتاح بإيده ويلزقه في ملف على
/// الراكة. ده معناه إن المفتاح بيعدّي على الحافظة وعلى واتساب أحياناً،
/// وإن الراكة المستنسخة بتورث مفتاح غيرها.</para>
///
/// <para>الكود ده بيتستهلك <b>مرة واحدة بالظبط</b> — التحديث مشروط
/// وبنعدّ الصفوف المتغيّرة، مش قراءة وبعدها كتابة.</para>
/// </summary>
public class RackPairingCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    /// <summary>الكود نفسه مبيتخزّنش — نفس مبدأ كلمات السر.</summary>
    [MaxLength(200)]
    public string CodeHash { get; set; } = "";

    [MaxLength(64)]
    public string Salt { get; set; } = "";

    [MaxLength(12)]
    public string CodePrefix { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    [MaxLength(120)]
    public string CreatedByName { get; set; } = "";

    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByRackId { get; set; }

    /// <summary>محاولات غلط. بعد حد معيّن الكود بيتقفل.</summary>
    public int FailedAttempts { get; set; }

    [MaxLength(120)]
    public string IntendedName { get; set; } = "";

    [MaxLength(120)]
    public string IntendedLocation { get; set; } = "";

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// عدّاد لكل شركة — مصدر الأرقام المتسلسلة (RACK-001، LP-00000001).
///
/// <para>⚠️ مش <c>MAX(code)+1</c>. ده بيتسابق: اتنين بيقروا نفس الرقم
/// وبيكتبوا نفس الكود. الزيادة بتتعمل في جملة تحديث واحدة.</para>
/// </summary>
public class TenantCounter
{
    public Guid TenantId { get; set; }

    [MaxLength(40)]
    public string CounterName { get; set; } = "";

    public int NextValue { get; set; } = 1;
}

/// <summary>
/// دفعة مزامنة اتستقبلت — <b>سجل عدم التكرار</b>.
///
/// <para><b>ليه ده مش زيادة على مطابقة معرّف الفحص.</b> الحالة الكلاسيكية:
/// السيرفر استقبل الدفعة وكتبها، وبعدين الرد ضاع في الشبكة. الراكة مش
/// عارفة إن الشغل وصل، فبتعيد. مطابقة معرّف الفحص بتمنع تكرار
/// <b>الصفوف</b> — بس مش بتعرف تقول للراكة <b>أنهي صفوف في طابورها</b>
/// اتقبلت، فالطابور بيفضل عالق.</para>
///
/// <para>بتخزين الرد نفسه، إعادة إرسال نفس الدفعة بترجّع نفس النتيجة
/// بالظبط — والراكة بتقفل صفوفها وتكمّل.</para>
/// </summary>
public class SyncBatch
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid RackId { get; set; }

    /// <summary>معرّف الدفعة اللي الراكة ولّدته.</summary>
    public Guid BatchId { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }

    /// <summary>الرد كامل — بيترجّع زي ما هو لو الدفعة اتعادت.</summary>
    public string ResponseJson { get; set; } = "";
}
