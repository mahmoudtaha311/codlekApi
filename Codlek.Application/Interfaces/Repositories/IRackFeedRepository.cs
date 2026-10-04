namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// التغذيات النازلة — <b>السيرفر بيكلّم الراكة</b>.
///
/// <para>🔴 <b>وكل دالة هنا بتاخد الشركة من المحطة المتحققة.</b> من
/// غير التقييد ده أي راكة بتقرا فنيي وحاويات وأوامر <b>كل</b>
/// الشركات على نفس السيرفر — وده الحاجز نفسه مش تحسين.</para>
///
/// <para>🔴 <b>وبترجّع صفوف ضيّقة مش كيانات.</b> صف الفني فيه
/// الاسم والكود وبس: لو رجّعنا كيان <c>Technician</c>، البصمة
/// والملح بيبقوا في الذاكرة جوّه المعالج — وسطر إسقاط واحد مكتوب
/// بسرعة بيحطّهم على السلك. الصف الضيّق بيخلّي التسريب
/// <b>مستحيل</b> مش «ممنوع».</para>
/// </summary>
public interface IRackFeedRepository
{
    /// <summary>
    /// فنيي الصيانة المتاحين للإسناد في شركة المحطة.
    ///
    /// <para>⚠️ <b>الشغّالين اللي بيعملوا صيانة بس</b> — القايمة دي
    /// بتظهر للفني وهو بيسند، والموقوف أو اللي مش بيصلّح مالوش شغل
    /// فيها.</para>
    /// </summary>
    Task<IReadOnlyList<RosterFeedRow>> RepairRosterAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>حاويات الاستيراد الشغّالة + عدد الأجهزة في كل واحدة.</summary>
    Task<IReadOnlyList<ContainerFeedRow>> ContainersAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// أوامر الصيانة <b>المفتوحة</b> بعد العلامة.
    ///
    /// <para>⚠️ <b>المفتوحة بس.</b> الأمر اللي خلص أو اتلغى مالوش أي
    /// شغل على راكة، وسحبه كان معناه إن كل راكة بتحمّل تاريخ الورشة
    /// كله مع الوقت.</para>
    /// </summary>
    /// <param name="sinceUtc">
    /// 🔴 <b>والمقارنة <c>&gt;</c> صارمة.</b> الراكة بتبعت آخر وقت
    /// شافته، فـ<c>&gt;=</c> كان بيرجّع آخر صف في كل سحبة للأبد.
    /// </param>
    /// <param name="take">
    /// ⚠️ بياخد <c>PageSize + 1</c> عشان نعرف لو فيه كمان من غير
    /// استعلام عدّ تاني.
    /// </param>
    Task<IReadOnlyList<AssignedRepairFeedRow>> AssignedRepairsAsync(
        Guid tenantId, DateTime? sinceUtc, int take, CancellationToken ct = default);
}

/// <summary>
/// 🔴 <b>فني للإسناد — ومفيش بصمة ولا ملح ولا اسم دخول.</b> ده عقد
/// «مين ينفع يتسند» مش عقد دخول.
/// </summary>
public sealed class RosterFeedRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string DisplayName { get; init; } = "";
}

public sealed class ContainerFeedRow
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int DeviceCount { get; init; }
}

/// <summary>
/// صف أمر صيانة خام من القاعدة.
///
/// <para>⚠️ <b>كلاس بخصائص <c>init</c> مش <c>record</c> موضعي.</b>
/// EF بيترجم ده لإسقاط حقيقي؛ الـ<c>record</c> الموضعي بيتحسب
/// «إسقاط في العميل» وبيفشل أول ما حد يعمل عليه <c>Concat</c> أو
/// <c>GroupBy</c> — وده اتكسر فعلاً في خط زمن الجهاز.</para>
/// </summary>
public sealed class AssignedRepairFeedRow
{
    public Guid Id { get; init; }
    public string PublicCode { get; init; } = "";

    public Guid DeviceId { get; init; }
    public string DeviceCode { get; init; } = "";

    /// <summary>الاسم التجاري لو موجود، وإلا الموديل الخام.</summary>
    public string DeviceName { get; init; } = "";

    public string DeviceManufacturer { get; init; } = "";

    public int Status { get; init; }

    public Guid? AssignedTechnicianId { get; init; }
    public string AssignedTechnicianName { get; init; } = "";

    public string OpenedByName { get; init; } = "";
    public DateTime OpenedAtUtc { get; init; }

    public string FaultSummary { get; init; } = "";

    public int Approval { get; init; }
    public string ApprovalNote { get; init; } = "";
    public string ApprovedByName { get; init; } = "";
    public DateTime? ApprovalDecidedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
