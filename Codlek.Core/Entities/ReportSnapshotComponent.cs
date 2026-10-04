using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// مكوّن واحد جوّه لقطة الفحص — «كان جوّاه إيه يومها».
///
/// <para>جدول منفصل مش JSON عشان السؤال «القطعة اللي سيريالها كذا راحت
/// فين؟» يبقى استعلام، مش مسح لكل الفحوصات.</para>
/// </summary>
public class ReportSnapshotComponent
{
    public long Id { get; set; }

    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    public Guid TenantId { get; set; }

    public int Type { get; set; }
    public int InstanceIndex { get; set; }

    [MaxLength(60)] public string SlotOrPosition { get; set; } = "";

    [MaxLength(80)] public string Manufacturer { get; set; } = "";
    [MaxLength(160)] public string Model { get; set; } = "";
    [MaxLength(80)] public string PartNumber { get; set; } = "";

    /// <summary>السيريال زي ما العتاد قاله. فاضي = القطعة دي مالهاش هوية فردية.</summary>
    [MaxLength(120)] public string ManufacturerSerial { get; set; } = "";

    [MaxLength(120)] public string HardwareFingerprint { get; set; } = "";
    [MaxLength(200)] public string PnPDeviceId { get; set; } = "";

    /// <summary>
    /// قرينا الهوية دي منين — نفس ترقيم <c>IdentityMethod</c> على الراكة:
    /// 0 مفيش · 1 سيريال الشركة · 2 سيريال EDID · 3 سيريال SMART ·
    /// 4 بصمة · 5 مسار PnP · 6 كود متلزّق.
    /// </summary>
    public int IdentityMethod { get; set; }

    /// <summary>
    /// A سيريال حقيقي · B بصمة بتحدد الموديل مش القطعة · C كود متلزّق.
    ///
    /// <para>⚠️ «القطعة دي اتغيّرت» تتقال بس لما الطرفين A. لابين
    /// متطابقين من نفس الباليتة بيدّوا نفس بصمة B بالظبط.</para>
    /// </summary>
    public int IdentityConfidence { get; set; }

    public bool IsPresent { get; set; } = true;

    // ⚠️ nullable عن قصد: «مفيش قراءة» غير «صفر».
    public long? CapacityBytes { get; set; }
    public int? SpeedMhz { get; set; }
    public int? HealthPercent { get; set; }
    public int? PowerOnHours { get; set; }

    [MaxLength(80)] public string Source { get; set; } = "";

    /// <summary>تفاصيل إضافية زي ما القارئ رجّعها.</summary>
    public string AttributesJson { get; set; } = "";
}
