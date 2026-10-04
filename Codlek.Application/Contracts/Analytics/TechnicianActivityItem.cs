namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// نشاط فني في الفترة.
///
/// <para>⚠️ <b>التجميع بالكود مش بالهوية المركزية.</b> صفحة الفني
/// وكل الروابط في المنتج بتشتغل بالكود، والفحوص القديمة هويتها
/// المركزية فاضية — فالتجميع بالهوية كان هيخفي شغل حقيقي من
/// الجدول.</para>
///
/// <para>⚠️ والاسم <b>لقطة</b> متخزّنة على الفحص نفسه: المدير لما
/// يصحّح اسم فني، التقارير القديمة مالهاش تتغيّر.</para>
/// </summary>
public sealed record TechnicianActivityItem(
    string Code,
    string Name,
    int Reports,
    int NeedsReview,
    double AverageMinutes);
