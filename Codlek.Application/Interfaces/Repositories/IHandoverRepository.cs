using Codlek.Application.Contracts.Handover;
using Codlek.Core.Entities;
using Codlek.Core.Handover;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// التسليم.
///
/// <para>🔴 <b>شرط الأهلية عايش في مكان واحد هنا —
/// <see cref="EligibleIdsAsync"/> وقايمة المرشّحين بيقراوا نفس
/// الشرط.</b></para>
///
/// <para>في المشروع القديم كان مكتوب <b>مرتين</b> بنصّين مختلفين:
/// مرة في قايمة المرشّحين ومرة في التحقّق وقت التسليم — وواحدة منهم
/// كانت ناقصة «الصيانة المفتوحة» و«الفني الحائز». فالشاشة كانت
/// تعرض لاب والنقطة ترفضه، أو أسوأ: العكس، ولاب تحت التصليح يروح
/// المخزن.</para>
///
/// <para>⚠️ <b>والشرط نفسه:</b> اتفحص وآخر فحص مفيهوش فشل ولا خطأ
/// قراءة · مفيش أمر صيانة مفتوح · مش في إيد فني · ومرحلته مش
/// «محتاج صيانة» ولا «قيد الصيانة».</para>
///
/// <para>🔴 <b>و«سليم» هنا = صفر فشل <u>وصفر</u> خطأ قراءة.</b> فيه
/// تعريف تاني في التقارير بيتجاهل أخطاء القراءة؛ اتاخد الأشد هنا
/// عن قصد — التسليم مالوش رجعة، والقطعة اللي مااتقرتش مش «سليمة».
/// واللي عمره ما اتفحص مابيظهرش: مفيش فحص ≠ سليم.</para>
/// </summary>
public interface IHandoverRepository
{
    /// <summary>الجهات النشطة، مرتّبة بترتيب العرض.</summary>
    Task<IReadOnlyList<HandoverDestination>> DestinationsAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// الجهة بصفّها — <b>متتبّعة</b>، لأن التسليم بيقرا نوعها
    /// وحالتها ويكتب حركة عليها.
    /// </summary>
    Task<Location?> FindDestinationAsync(
        Guid tenantId, Guid destinationId, CancellationToken ct = default);

    /// <summary>صفحة من المرشّحين + العدد الكلي قبل التصفيح.</summary>
    Task<(IReadOnlyList<HandoverCandidateRow> Rows, int TotalItems)> CandidatesAsync(
        Guid tenantId, HandoverCandidateFilter filter, CancellationToken ct = default);

    /// <summary>
    /// معرّفات كل اللي طلع من <b>نفس</b> الفلتر + العدد الكلي.
    ///
    /// <para>🔴 <b>نفس شرط البحث بالحرف زي قايمة المرشّحين.</b> لو
    /// اتنين اختلفوا، «اختر كل اللي طلع» بيختار حاجة غير اللي
    /// الشاشة عارضاها — وده أسوأ من إنه مايشتغلش.</para>
    /// </summary>
    Task<(IReadOnlyList<Guid> Ids, int TotalItems)> CandidateIdsAsync(
        Guid tenantId, HandoverCandidateFilter filter, int cap,
        CancellationToken ct = default);

    /// <summary>
    /// أنهي معرّفات من دول <b>مؤهّلة</b> فعلاً.
    ///
    /// <para>🔴 الخدمة اللي بتكتب الحركات مافيهاش أي تحقّق من صحة
    /// الانتقال، والقايمة اللي في الشاشة مجرد اقتراح — اللي يبعت
    /// معرّفات بنفسه بيعدّي منها. فنفس الشرط بيتقاس هنا تاني.</para>
    /// </summary>
    Task<IReadOnlyList<Guid>> EligibleIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default);

    /// <summary>
    /// أكواد لابات بعينها — <b>لرسالة الرفض</b>.
    ///
    /// <para>⚠️ الرسالة بتقول أنهي لاب بالكود: «فيه لاب مش مؤهّل»
    /// بتخلّي اللي بيراجع يلغي الدفعة كلها وهو مش عارف ليه.</para>
    /// </summary>
    Task<IReadOnlyList<string>> CodesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, int take,
        CancellationToken ct = default);

    /// <summary>
    /// أكواد اللابات اللي <b>محدش راجعها</b> من القايمة دي.
    /// </summary>
    Task<IReadOnlyList<string>> UnreviewedCodesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, int take,
        CancellationToken ct = default);

    /// <summary>كام لاب من دول موجود في الشركة دي.</summary>
    Task<int> CountExistingAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default);

    /// <summary>
    /// اللابات للتعديل — <b>متتبّعة</b>.
    ///
    /// <para>🔴 <b>متتبّعة عن قصد.</b> القراية بـ<c>AsNoTracking</c>
    /// بترجّع صفوف بتتعدّل في الذاكرة و<c>SaveChanges</c> مابتكتبش
    /// حاجة — والنقطة كانت بترجّع «اتغيّر ٣» والقاعدة زي ما هي.
    /// نجاح كداب.</para>
    /// </summary>
    /// <param name="markedOnly">
    /// <c>true</c> = المعلّم بس (لشيل العلامة)، <c>false</c> = اللي
    /// مش معلّم (للتعليم).
    /// </param>
    Task<IReadOnlyList<Device>> TrackedForReviewAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, bool markedOnly,
        CancellationToken ct = default);

    /// <summary>المستلمون وعدد لاباتهم وآخر تسليم.</summary>
    Task<IReadOnlyList<HandoverRecipientItem>> RecipientsAsync(
        Guid tenantId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);

    /// <summary>صفحة من سجل التسليمات + العدد الكلي.</summary>
    Task<(IReadOnlyList<HandoverLogItem> Rows, int TotalItems)> LogAsync(
        Guid tenantId, HandoverLogFilter filter, CancellationToken ct = default);
}
