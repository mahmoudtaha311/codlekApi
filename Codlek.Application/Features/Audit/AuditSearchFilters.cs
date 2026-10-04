using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Paging;
using Codlek.Core.Text;
using Codlek.Core.Time;

namespace Codlek.Application.Features.Audit;

/// <summary>
/// بناء فلتر بحث سجل المراجعة — <b>مكان واحد للقايمة
/// وللتصدير</b>.
///
/// <para>⚠️ الاسم <c>AuditSearchFilters</c> مش <c>AuditFilters</c>:
/// التاني عقد موجود خلاص — قوايم الاختيارات اللي نقطة
/// <c>/audit/filters</c> بترجّعها للواجهة.</para>
/// </summary>
internal static class AuditSearchFilters
{
    /// <summary>
    /// ⚠️ <b>٤٠ مش ٢٥</b> — ده سجل بيتقرا بالتمرير، والصفحة
    /// الصغيرة بتخلّي اللي بيراجع يدوس «بعده» عشرين مرة.
    /// </summary>
    public const int DefaultPageSize = 40;

    public static AuditFilter Build(
        DateTime? from,
        DateTime? to,
        string? action,
        string? entityType,
        string? actor,
        string? search,
        int? page = null,
        int? pageSize = null)
    {
        var (p, size) = Paging.Clamp(page, pageSize, DefaultPageSize);

        string raw = (search ?? "").Trim();

        return new AuditFilter
        {
            // 🔴 الحدود بأيام القاهرة زي باقي الشاشات.
            FromUtc = CairoDay.StartUtc(from),
            ToUtc = CairoDay.AfterUtc(to),

            Action = (action ?? "").Trim(),
            EntityType = (entityType ?? "").Trim(),
            ActorName = (actor ?? "").Trim(),

            // ⚠️ ومفيش توحيد عربي — القديم بيهرّب وبس، والنقل زي ما
            // هو عشان نفس البحث يدّي نفس النتيجة من الشاشتين.
            SearchPattern = raw.Length == 0 ? null : SearchPattern.Contains(raw),

            Page = p,
            PageSize = size,
        };
    }
}
