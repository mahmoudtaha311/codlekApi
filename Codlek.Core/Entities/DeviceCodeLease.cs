using System.ComponentModel.DataAnnotations;
using Codlek.Core.Enums;

namespace Codlek.Core.Entities;

/// <summary>
/// بلوك أرقام مؤجّر لراكة.
///
/// <para><b>المشكلة اللي بيحلّها.</b> السيرفر لازم يملك مساحة الأرقام
/// عشان مايحصلش تكرار. والفني ماسك اللاب على بنش <b>من غير نت</b> ومحتاج
/// يطبع الليبل دلوقتي. الاتنين مش ممكنين مع بعض إلا لو الأرقام اتحجزت
/// مقدّماً.</para>
///
/// <list type="bullet">
/// <item><b>البلوكات حصرية</b> — المدى بيتخصّص لراكة واحدة بس، فالتكرار
/// مستحيل بالتصميم مش «بعيد الاحتمال».</item>
/// <item><b>ممنوع إعادة استخدام المدى.</b> بلوك مستهلك أو ملغي عمره ما
/// بيرجع للمساحة. التدوير معناه كودين لجهازين مختلفين — وده بيهدّ تتبّع
/// العمر كله.</item>
/// <item><b>الفجوات مقبولة.</b> راكة اترمت وفي بلوكها ١٢٠ كود متستخدمش
/// = فجوة للأبد. الـ LP أرقام تعريف مش عدّاد مخزون.</item>
/// </list>
/// </summary>
public class DeviceCodeLease
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid RackId { get; set; }

    /// <summary>أول رقم في المدى (شامل).</summary>
    public int FromNumber { get; set; }

    /// <summary>آخر رقم في المدى (شامل). عمره ما بينزل.</summary>
    public int ToNumber { get; set; }

    /// <summary>آخر رقم الراكة قالت إنها استخدمته.</summary>
    public int ConsumedThrough { get; set; }

    public DeviceCodeLeaseStatus Status { get; set; } = DeviceCodeLeaseStatus.Open;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    [MaxLength(200)]
    public string ClosedReason { get; set; } = "";
}
