using Codlek.Core.Handover;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// فلاتر قايمة المرشّحين — <b>متظبّطة خلاص</b>.
///
/// <para>🔴 <b>الفلتر ده بيتبعت لقايمة المرشّحين <u>ولقايمة
/// المعرّفات</u> — نفس الكائن.</b> ده اللي بيضمن إن «اختر كل اللي
/// طلع» بيختار بالظبط اللي الشاشة عارضاها. ولو كل نقطة بنت فلترها،
/// أول اختلاف بيخلّي الزرار يختار حاجة تانية خالص.</para>
/// </summary>
public sealed record HandoverCandidateFilter
{
    public ReviewFilter Review { get; init; } = ReviewFilter.Reviewed;

    public Guid? ContainerId { get; init; }

    /// <summary>نص البحث الخام — للمقارنة المضبوطة مع كود اللاب.</summary>
    public string? ExactCode { get; init; }

    /// <summary>نمط <c>LIKE</c> جاهز — موحّد ومهرّب.</summary>
    public string? SearchPattern { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = Core.Paging.Paging.DefaultPageSize;
}
