using Codlek.Core.Enums;

namespace Codlek.Core.Repairs;

/// <summary>
/// حاجز الموافقة وقت البدء — <b>دالة نقية</b>.
///
/// <para>🔴 <b>ده كان أوسع باب في الميزة كلها.</b> المسار الوحيد
/// اللي بينده الحاجز ده هو «التجاوز الإداري» للمالك. ومن غيره
/// المالك بيقدر يبدأ أمر معلّق ويعدّي على المحاسب <b>في صمت</b>:
/// السجل بيكتب «تجاوز إداري» ومابيقولش إن الموافقة مااتخدتش
/// أصلاً.</para>
///
/// <para>⚠️ <b>ومفيش صلاحية بتتخسر.</b> المالك داخل في سياسة
/// <c>RepairApprover</c>، فهو بيوافق الأول وبعدين يتجاوز — قرارين
/// مكتوبين بدل واحد بيخبّي التاني.</para>
///
/// <para>⚠️ وده بيختلف عن مسار الرفع من الراكة: هناك الشغل
/// <b>اتعمل خلاص</b> على البنش، فبنقبله ونعلّمه
/// (<c>StartedWithoutApproval</c>). هنا الشغل لسه ماحصلش، والمنع
/// مالوش أي تكلفة.</para>
/// </summary>
public static class RepairApprovalGate
{
    /// <summary>سبب منع البدء — <c>None</c> يعني مسموح.</summary>
    public enum StartBlock
    {
        /// <summary>مفيش مانع.</summary>
        None = 0,

        /// <summary>المحاسب رفض الأمر.</summary>
        Rejected = 1,

        /// <summary>لسه مستني قرار المحاسب.</summary>
        AwaitingApproval = 2,
    }

    /// <summary>
    /// ⚠️ <b>الشرطين مع بعض.</b> المنع بيتطبّق بس لو الميزة شغّالة
    /// (<see cref="RepairPolicy.ApprovalEnforced"/>) <b>و</b> الأمر
    /// مش موافَق عليه. أي واحد ناقص = البدء مسموح.
    /// </summary>
    public static StartBlock CanStart(bool approvalEnforced, RepairApproval approval)
    {
        if (!approvalEnforced) return StartBlock.None;
        if (approval == RepairApproval.Approved) return StartBlock.None;

        return approval == RepairApproval.Rejected
            ? StartBlock.Rejected
            : StartBlock.AwaitingApproval;
    }
}
