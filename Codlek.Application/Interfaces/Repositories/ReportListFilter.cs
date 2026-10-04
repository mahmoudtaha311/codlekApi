namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// فلاتر قايمة الفحوص — <b>متظبّطة خلاص</b>.
///
/// <para>🔴 <b>وده نفس الكائن اللي التصدير بيبنيه</b> — عشان الملف
/// يطلع زي الشاشة بالحرف.</para>
/// </summary>
public sealed record ReportListFilter
{
    /// <summary>
    /// 🔴 كود فني — <b>والتضييق مش اختياري للفني</b>.
    ///
    /// <para>الـHandler بيحطّه من المستخدم الحالي لو هو فني، ومن
    /// الفلتر لو هو مدير. فالفني مايقدرش يشوف شغل غيره بتغيير
    /// رابط.</para>
    /// </summary>
    public string? TechnicianCode { get; init; }

    /// <summary>كود الجهاز الخام — للمقارنة المضبوطة.</summary>
    public string? ExactDeviceCode { get; init; }

    /// <summary>نمط <c>LIKE</c> على نص البحث — موحّد ومهرّب.</summary>
    public string? SearchPattern { get; init; }

    public ReportResultFilter Result { get; init; } = ReportResultFilter.Any;

    /// <summary>
    /// ⚠️ <b>الحاوية على <u>الجهاز</u> مش على الفحص:</b> الشحنة
    /// بتوصف اللاب نفسه (جه في الشحنة دي)، والفحص بيشاور عليه.
    /// </summary>
    public Guid? ContainerId { get; init; }

    public DateTime? FromUtc { get; init; }

    /// <summary>🔴 <b>خارج</b> — بداية اليوم اللي بعده.</summary>
    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = Core.Paging.Paging.DefaultPageSize;
}
