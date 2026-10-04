using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
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
