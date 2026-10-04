namespace Codlek.Application.Interfaces.Repositories;

/// <summary>فلاتر سجل المراجعة — متظبّطة خلاص.</summary>
public sealed record AuditFilter
{
    public DateTime? FromUtc { get; init; }

    /// <summary>
    /// 🔴 <b>خارج</b> — بداية اليوم اللي بعده. ولو كان شامل، كل
    /// سطور اليوم الأخير كانت تختفي.
    /// </summary>
    public DateTime? ToUtc { get; init; }

    /// <summary>
    /// ⚠️ <b>مقارنة مضبوطة مش بحث.</b> القيمة جاية من قايمة
    /// الفلاتر اللي النقطة نفسها بتبنيها، فهي كود موجود فعلاً.
    /// </summary>
    public string? Action { get; init; }

    public string? EntityType { get; init; }

    public string? ActorName { get; init; }

    /// <summary>
    /// نمط <c>LIKE</c> — بيتطبّق على الملخّص وكود الكيان واسم
    /// الفاعل مع بعض.
    ///
    /// <para>⚠️ ومفيش توحيد عربي عليه — القديم بيهرّب النص وبس،
    /// والنقل زي ما هو عشان نفس البحث يدّي نفس النتيجة من
    /// الشاشتين.</para>
    /// </summary>
    public string? SearchPattern { get; init; }

    public int Page { get; init; } = 1;

    /// <summary>
    /// ⚠️ <b>الافتراضي ٤٠ هنا، مش ٢٥ زي باقي القوايم.</b> ده سجل
    /// بيتقرا بالتمرير، والصفحة الصغيرة بتخلّي اللي بيراجع يدوس
    /// «بعده» عشرين مرة.
    /// </summary>
    public int PageSize { get; init; } = 40;
}
