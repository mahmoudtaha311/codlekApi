using Codlek.Core.Enums;

namespace Codlek.Core.Repairs;

/// <summary>
/// انتقالات حالة أمر الصيانة — <b>جدول مكتوب، مش مقارنة أرقام</b>.
///
/// <para>🔴 <b>مفيش ترتيب بين قيم <see cref="RepairStatus"/>.</b>
/// <c>Cancelled = 5</c> رقمها أكبر من <c>InProgress = 2</c>، فأي شرط
/// زي <c>status &gt;= InProgress</c> بيحسب <b>الإلغاء</b> شغل اتعمل.
/// وإلغاء أمر مستني موافقة حاجة طبيعية تماماً، مالهاش أي علاقة بإن حد
/// فك لاب من غير إذن.</para>
///
/// <para>⚠️ والأرقام نفسها <b>عقد مجمّد</b> — الراكة بتقراها كأرقام
/// على السلك. راجع <c>Enums/README.md</c>.</para>
/// </summary>
public static class RepairStatusRules
{
    /// <summary>
    /// الحالة دي معناها إن حد فتح اللاب فعلاً؟
    ///
    /// <para>⚠️ <b>عضوية صريحة.</b> ودي مهمة لأن عليها بيتبنى
    /// «اتبدأ من غير موافقة» — وحساب الإلغاء كشغل اتعمل بيخلّي كل
    /// أمر ملغي يتسجّل كتجاوز.</para>
    /// </summary>
    public static bool IsBenchWork(RepairStatus status) =>
        status is RepairStatus.InProgress
               or RepairStatus.Completed
               or RepairStatus.UnableToRepair;

    /// <summary>
    /// الانتقالات المسموحة — <b>منقولة من المشروع القديم بالحرف</b>.
    ///
    /// <para>⚠️ <c>WaitingForRepair → WaitingForRepair</c> و
    /// <c>InProgress → InProgress</c> موجودين في الجدول عن قصد: إعادة
    /// الإسناد لفني تاني بتعيد كتابة نفس الحالة، ولو الانتقال ده
    /// اتمنع، المدير مايقدرش يغيّر الفني على أمر شغّال.</para>
    ///
    /// <para>🔴 والتلات حالات النهائية (<c>Completed</c> ·
    /// <c>UnableToRepair</c> · <c>Cancelled</c>) مالهاش ولا انتقال.
    /// أمر اتقفل مايرجعش يفتح — الفتح من جديد أمر جديد، وده اللي
    /// بيخلّي العدّ والتاريخ مفهومين.</para>
    /// </summary>
    private static readonly Dictionary<RepairStatus, RepairStatus[]> Allowed = new()
    {
        [RepairStatus.New] =
        [
            RepairStatus.WaitingForRepair, RepairStatus.InProgress, RepairStatus.Cancelled,
        ],

        [RepairStatus.WaitingForRepair] =
        [
            RepairStatus.InProgress, RepairStatus.Cancelled, RepairStatus.WaitingForRepair,
        ],

        [RepairStatus.InProgress] =
        [
            RepairStatus.Completed, RepairStatus.UnableToRepair, RepairStatus.InProgress,
        ],

        [RepairStatus.Completed] = [],
        [RepairStatus.UnableToRepair] = [],
        [RepairStatus.Cancelled] = [],
    };

    /// <summary>
    /// ⚠️ <c>from == to</c> بتعدّي دايماً — حتى للحالات النهائية.
    ///
    /// <para>ده سلوك القديم بالحرف، والسبب إن الكود بينده الدالة دي
    /// قبل ما يكتب نفس الحالة في حالات زي «قفل أمر مقفول خلاص» —
    /// واللي بيترد عليها بنجاح صامت مش بخطأ.</para>
    /// </summary>
    public static bool CanMove(RepairStatus from, RepairStatus to) =>
        from == to || (Allowed.TryGetValue(from, out var next) && next.Contains(to));

    /// <summary>
    /// الحالة بالعربي — <b>بتتعرض للمستخدم</b>.
    ///
    /// <para>🔴 النصوص دي عقد: الواجهة والراكة بيعرضوها، والرسايل
    /// بتتبني منها («الأمر ده اتقرر فيه خلاص — …»).</para>
    /// </summary>
    public static string Text(RepairStatus status) => status switch
    {
        RepairStatus.New => "جديدة",
        RepairStatus.WaitingForRepair => "بانتظار الصيانة",
        RepairStatus.InProgress => "قيد الصيانة",
        RepairStatus.Completed => "تمت الصيانة",
        RepairStatus.UnableToRepair => "تعذر الإصلاح",
        RepairStatus.Cancelled => "ملغاة",
        _ => "غير معروف",
    };

    /// <summary>
    /// قرار الموافقة بالعربي.
    ///
    /// <para>⚠️ بيتستعمل في رسالة «الأمر ده اتقرر فيه خلاص — {كذا}»
    /// لما محاسبين على شاشتين يدوسوا في نفس اللحظة.</para>
    /// </summary>
    public static string ApprovalText(RepairApproval approval) => approval switch
    {
        RepairApproval.Pending => "مستني الموافقة",
        RepairApproval.Approved => "اتوافق عليه",
        RepairApproval.Rejected => "اترفض",
        _ => "غير معروف",
    };
}
