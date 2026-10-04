using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// فلاتر قايمة الصيانة — <b>كلها متظبّطة خلاص</b>.
///
/// <para>🔴 <b>الحاجز ده مقصود: المستودع مابيشوفش نص المستخدم
/// الخام.</b> الحالة بتتحوّل لـenum، والتاريخ بيتحوّل لـUTC، ونص
/// البحث بيتوحّد ويتهرّب (<c>LIKE</c>) — كل ده في الـHandler. ولو
/// المستودع عمل الكلام ده، كان لازم يستعمل
/// <c>ArabicText.Normalize</c> جوّه <c>Where</c> — وده بيترجم
/// وبيرمي على قاعدة حقيقية.</para>
///
/// <para>⚠️ <b>وكل فلتر فاضي معناه «مفيش فلتر»، مش «مفيش
/// نتايج»</b>. القيمة المش مفهومة (حالة اسمها غلط مثلاً) بتتجاهل
/// وبترجع القايمة كلها — ده سلوك القديم، والداش بورد معتمدة
/// عليه.</para>
/// </summary>
public sealed record RepairListFilter
{
    /// <summary>
    /// نص البحث الخام — للمقارنة <b>المضبوطة</b> مع كود الأمر.
    ///
    /// <para>⚠️ الكود مش عربي، فالتوحيد مايتطبّقش عليه.</para>
    /// </summary>
    public string? ExactCode { get; init; }

    /// <summary>
    /// نمط <c>LIKE</c> جاهز — موحّد ومهرّب ومحطوط بين
    /// <c>%</c>.
    /// </summary>
    public string? SearchPattern { get; init; }

    public RepairStatus? Status { get; init; }

    public RepairApproval? Approval { get; init; }

    public Guid? TechnicianId { get; init; }

    /// <summary>من (داخل) — بداية اليوم بتوقيت القاهرة بالـUTC.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>
    /// لـ (<b>خارج</b>) — بداية اليوم اللي بعده.
    ///
    /// <para>🔴 المدى نصف مفتوح: <c>&gt;= From</c> و<c>&lt; To</c>.
    /// ولو كان <c>&lt;=</c> على بداية اليوم، كل أوامر اليوم الأخير
    /// كانت تختفي من التقرير.</para>
    /// </summary>
    public DateTime? ToUtc { get; init; }

    /// <summary>الأقدم الأول؟ — الافتراضي الأحدث.</summary>
    public bool Oldest { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = Core.Paging.Paging.DefaultPageSize;
}
