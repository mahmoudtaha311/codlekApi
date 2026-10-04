using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Handover;
using Codlek.Core.Paging;
using Codlek.Core.Text;

namespace Codlek.Application.Features.Handover;

/// <summary>
/// بناء فلتر المرشّحين — <b>مكان واحد للنقطتين</b>.
///
/// <para>🔴 <b>وده مش ترتيب كود، ده صحّة.</b> قايمة المرشّحين
/// وقايمة المعرّفات لازم يبنوا <b>نفس</b> الفلتر بالحرف. ولو
/// اختلفوا، زرار «اختر كل اللي طلع» بيختار حاجة غير اللي الشاشة
/// عارضاها — وده أسوأ من إنه مايشتغلش، لأن المدير بيسلّم وهو
/// مطمّن.</para>
/// </summary>
internal static class HandoverFilters
{
    public static HandoverCandidateFilter Candidates(
        string? search, Guid? container, string? review, int? page = null, int? pageSize = null)
    {
        var (p, size) = Paging.Clamp(page, pageSize);

        string raw = (search ?? "").Trim();

        return new HandoverCandidateFilter
        {
            Review = HandoverPolicy.Review(review),
            ContainerId = container,

            // ⚠️ الكود لاتيني فالتوحيد العربي مالوش لازمة عليه؛
            // ونص البحث لازم يتوحّد عشان «أحمد» و«احمد» يلاقوا نفس
            // اللاب.
            ExactCode = raw.Length == 0 ? null : raw,

            SearchPattern = raw.Length == 0
                ? null
                : SearchPattern.Contains(ArabicText.Normalize(raw)),

            Page = p,
            PageSize = size,
        };
    }
}
