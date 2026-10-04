using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

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
